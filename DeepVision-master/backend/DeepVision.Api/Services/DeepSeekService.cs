using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepVision.Api.Models;
using Tesseract;

namespace DeepVision.Api.Services;

/// <summary>
/// Single-stage extraction:
///   1. Tesseract OCR  → raw text from the image
///   2. One LLM call   → cleaned text + document details in one JSON response
/// </summary>
public class DeepSeekService : IDeepSeekService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DeepSeekService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public DeepSeekService(HttpClient httpClient, IConfiguration configuration, ILogger<DeepSeekService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CombinedExtractionResult> ExtractAllAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            // Stage 1: Tesseract OCR (local, fast)
            var rawText = await RunTesseractAsync(imageBytes);
            _logger.LogInformation("Tesseract extracted {Chars} chars", rawText.Length);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new CombinedExtractionResult
                {
                    ExtractedText = "[No text detected in this image]",
                    TokensUsed = 0,
                    Success = true
                };
            }

            // Stage 2: Single LLM call → clean text + document details
            var apiKey = _configuration["Groq:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                _logger.LogWarning("API key not configured — returning raw Tesseract output");
                return new CombinedExtractionResult { ExtractedText = rawText, TokensUsed = 0, Success = true };
            }

            // Truncate to 1500 chars — enough for any invoice, reduces LLM processing time
            var trimmedText = rawText.Length > 1500 ? rawText[..1500] : rawText;

            return await ExtractAllWithLlmAsync(trimmedText, apiKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during extraction");
            return new CombinedExtractionResult { Success = false, ErrorMessage = $"Extraction error: {ex.Message}" };
        }
    }

    // ── Tesseract ─────────────────────────────────────────────────────────────

    private Task<string> RunTesseractAsync(byte[] imageBytes)
    {
        return Task.Run(() =>
        {
            var tessDataPath = _configuration["Tesseract:DataPath"]
                ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
            var language = _configuration["Tesseract:Language"] ?? "eng";

            using var engine = new TesseractEngine(tessDataPath, language, EngineMode.Default);
            using var pix = Pix.LoadFromMemory(imageBytes);
            using var page = engine.Process(pix);
            return page.GetText() ?? string.Empty;
        });
    }

    // ── Single combined LLM call ──────────────────────────────────────────────

    private async Task<CombinedExtractionResult> ExtractAllWithLlmAsync(string rawOcrText, string apiKey)
    {
        var baseUrl = _configuration["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1";
        var model = _configuration["Groq:Model"] ?? "llama-3.3-70b-versatile";
        var maxTokens = int.Parse(_configuration["Groq:MaxTokens"] ?? "1024");

        const string systemPrompt =
            "You are a document analyst. Extract key fields from the text. " +
            "Return ONLY a raw JSON object (no markdown, no explanation, no extra text): " +
            "{" +
            "\"documentType\": \"Invoice|Receipt|PurchaseOrder|Statement|CreditNote|DeliveryNote|Other\", " +
            "\"invoiceNumber\": \"string or null\", " +
            "\"invoiceDate\": \"string or null\", " +
            "\"dueDate\": \"string or null\", " +
            "\"vendorName\": \"string or null\", " +
            "\"customerName\": \"string or null\", " +
            "\"subTotal\": \"string or null\", " +
            "\"taxAmount\": \"string or null\", " +
            "\"totalAmount\": \"string or null\", " +
            "\"currency\": \"string or null\", " +
            "\"paymentTerms\": \"string or null\", " +
            "\"additionalFields\": {} " +
            "}";

        var request = new DeepSeekRequest
        {
            Model = model,
            MaxTokens = maxTokens,
            Temperature = 0.1,
            ResponseFormat = new ResponseFormat { Type = "json_object" },
            Messages =
            [
                new DeepSeekMessage { Role = "system", Content = systemPrompt },
                new DeepSeekMessage { Role = "user", Content = rawOcrText }
            ]
        };

        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

        var httpResponse = await _httpClient.SendAsync(httpRequest);
        var responseJson = await httpResponse.Content.ReadAsStringAsync();

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Groq API call failed {Status}. Body: {Body}", httpResponse.StatusCode, responseJson);
            return new CombinedExtractionResult { ExtractedText = rawOcrText, TokensUsed = 0, Success = true };
        }

        _logger.LogDebug("Groq raw response: {Response}", responseJson);

        var response = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson, JsonOptions);
        var content = response?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(content))
            return new CombinedExtractionResult { ExtractedText = rawOcrText, TokensUsed = 0, Success = true };

        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var firstNewLine = content.IndexOf('\n');
            var lastFence = content.LastIndexOf("```");
            if (firstNewLine >= 0 && lastFence > firstNewLine)
                content = content[(firstNewLine + 1)..lastFence].Trim();
        }

        try
        {
            using var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;
            var props = root.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

            static string? ToStr(JsonElement el) =>
                el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString(),
                    JsonValueKind.Null or JsonValueKind.Undefined => null,
                    _ => el.GetRawText()
                };

            string? Get(string name) => props.TryGetValue(name, out var el) ? ToStr(el) : null;

            var cleanedText = rawOcrText;

            Dictionary<string, string>? additionalFields = null;
            if (props.TryGetValue("additionalFields", out var afEl) && afEl.ValueKind == JsonValueKind.Object)
            {
                additionalFields = [];
                foreach (var prop in afEl.EnumerateObject())
                {
                    var v = ToStr(prop.Value);
                    if (v != null) additionalFields[prop.Name] = v;
                }
            }

            var details = new DocumentDetails
            {
                DocumentType   = Get("documentType"),
                InvoiceNumber  = Get("invoiceNumber"),
                InvoiceDate    = Get("invoiceDate"),
                DueDate        = Get("dueDate"),
                VendorName     = Get("vendorName"),
                CustomerName   = Get("customerName"),
                SubTotal       = Get("subTotal"),
                TaxAmount      = Get("taxAmount"),
                TotalAmount    = Get("totalAmount"),
                Currency       = Get("currency"),
                PaymentTerms   = Get("paymentTerms"),
                AdditionalFields = additionalFields?.Count > 0 ? additionalFields : null
            };

            _logger.LogInformation("Extraction OK: type={Type}, vendor={Vendor}, total={Total}",
                details.DocumentType, details.VendorName, details.TotalAmount);

            return new CombinedExtractionResult
            {
                Success = true,
                ExtractedText = cleanedText,
                DocumentDetails = details,
                TokensUsed = response?.Usage?.TotalTokens ?? 0
            };
        }
        catch (JsonException jex)
        {
            _logger.LogWarning("JSON parse failed: {Message}. Content was: {Content}", jex.Message, content);
            return new CombinedExtractionResult { ExtractedText = rawOcrText, Success = true };
        }
    }
}
