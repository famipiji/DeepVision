using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DeepVision.Api.Models;
using Tesseract;

namespace DeepVision.Api.Services;

/// <summary>
/// Two-stage text extraction:
///   1. Tesseract OCR  → raw text from the image
///   2. DeepSeek text API → clean and structure the raw OCR output
///
/// DeepSeek's public API (deepseek-chat) is text-only and does not accept image
/// inputs, so Tesseract handles the vision step.
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

    public async Task<DeepSeekExtractionResult> ExtractTextFromImageAsync(byte[] imageBytes, string mimeType)
    {
        try
        {
            // ── Stage 1: Tesseract OCR ────────────────────────────────────────
            var rawText = await RunTesseractAsync(imageBytes);
            _logger.LogInformation("Tesseract extracted {Chars} chars", rawText.Length);

            if (string.IsNullOrWhiteSpace(rawText))
            {
                return new DeepSeekExtractionResult
                {
                    ExtractedText = "[No text detected in this image]",
                    TokensUsed = 0,
                    Success = true
                };
            }

            // ── Stage 2: DeepSeek text post-processing ────────────────────────
            var apiKey = _configuration["DeepSeek:ApiKey"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_DEEPSEEK_API_KEY_HERE")
            {
                _logger.LogWarning("DeepSeek API key not configured — returning raw Tesseract output");
                return new DeepSeekExtractionResult { ExtractedText = rawText, TokensUsed = 0, Success = true };
            }

            return await PostProcessWithDeepSeekAsync(rawText, apiKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during text extraction");
            return new DeepSeekExtractionResult { Success = false, ErrorMessage = $"Extraction error: {ex.Message}" };
        }
    }

    public async Task<DocumentDetailsResult> ExtractDocumentDetailsAsync(string cleanedText)
    {
        _logger.LogInformation("=== Starting document details extraction ({Length} chars) ===", cleanedText.Length);
        try
        {
            var apiKey = _configuration["DeepSeek:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_DEEPSEEK_API_KEY_HERE")
            {
                _logger.LogWarning("API key not configured — skipping details extraction");
                return new DocumentDetailsResult { Success = false, ErrorMessage = "API key not configured." };
            }

            var baseUrl = _configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com";
            var model = _configuration["DeepSeek:Model"] ?? "deepseek-chat";
            _logger.LogInformation("Calling {BaseUrl} with model {Model}", baseUrl, model);

            const string systemPrompt =
                "You are an expert document analyst. Extract key information from the document text and return ONLY a " +
                "raw JSON object (no markdown, no explanation). Use null for missing fields. " +
                "Schema: {" +
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
                "\"additionalFields\": {\"key\": \"value\"} }";

            var request = new DeepSeekRequest
            {
                Model = model,
                MaxTokens = 1024,
                Temperature = 0.1,
                Messages =
                [
                    new DeepSeekMessage { Role = "system", Content = systemPrompt },
                    new DeepSeekMessage { Role = "user", Content = cleanedText }
                ]
            };

            var requestJson = JsonSerializer.Serialize(request, JsonOptions);
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var httpResponse = await _httpClient.SendAsync(httpRequest);
            var responseJson = await httpResponse.Content.ReadAsStringAsync();

            _logger.LogInformation("Details API response status: {Status}", httpResponse.StatusCode);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Details extraction FAILED {Status} — full body: {Body}", httpResponse.StatusCode, responseJson);
                return new DocumentDetailsResult { Success = false, ErrorMessage = $"API returned {(int)httpResponse.StatusCode}" };
            }

            var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson, JsonOptions);
            var content = deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            _logger.LogInformation("Raw details content (first 400 chars): {Content}",
                content?.Substring(0, Math.Min(400, content?.Length ?? 0)));

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Empty content in details response");
                return new DocumentDetailsResult { Success = false, ErrorMessage = "Empty response from API." };
            }

            // Strip markdown code fences if the model wraps JSON in them
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var firstNewLine = content.IndexOf('\n');
                var lastFence = content.LastIndexOf("```");
                if (firstNewLine >= 0 && lastFence > firstNewLine)
                    content = content[(firstNewLine + 1)..lastFence].Trim();
            }

            // Robust parsing: handle non-string values and any property casing the LLM may use
            DocumentDetails details;
            try
            {
                using var jsonDoc = JsonDocument.Parse(content);
                var root = jsonDoc.RootElement;

                // Build a case-insensitive lookup of all top-level properties
                var props = root.EnumerateObject()
                    .ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

                // Safely read any element as a string (handles numbers, booleans, etc.)
                static string? ToStr(JsonElement el) =>
                    el.ValueKind switch
                    {
                        JsonValueKind.String => el.GetString(),
                        JsonValueKind.Null or JsonValueKind.Undefined => null,
                        _ => el.GetRawText()
                    };

                string? Get(string name) =>
                    props.TryGetValue(name, out var el) ? ToStr(el) : null;

                // Parse additionalFields — values may be strings, numbers, etc.
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

                details = new DocumentDetails
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
            }
            catch (JsonException jex)
            {
                _logger.LogWarning("JSON parse failed: {Message} — content was: {Content}", jex.Message, content);
                return new DocumentDetailsResult { Success = false, ErrorMessage = $"JSON parse failed: {jex.Message}" };
            }

            _logger.LogInformation("=== Details extracted OK: type={Type}, invoice={Invoice}, vendor={Vendor}, total={Total} ===",
                details.DocumentType, details.InvoiceNumber, details.VendorName, details.TotalAmount);

            return new DocumentDetailsResult { Success = true, Details = details };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during document details extraction");
            return new DocumentDetailsResult { Success = false, ErrorMessage = ex.Message };
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

    // ── DeepSeek text post-processing ─────────────────────────────────────────

    private async Task<DeepSeekExtractionResult> PostProcessWithDeepSeekAsync(string rawOcrText, string apiKey)
    {
        var baseUrl = _configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com";
        var model = _configuration["DeepSeek:Model"] ?? "deepseek-chat";
        var maxTokens = int.Parse(_configuration["DeepSeek:MaxTokens"] ?? "4096");

        var request = new DeepSeekRequest
        {
            Model = model,
            MaxTokens = maxTokens,
            Temperature = 0.1,
            Messages =
            [
                new DeepSeekMessage
                {
                    Role = "system",
                    Content = "You are an expert OCR text formatter. " +
                              "You receive raw text produced by Tesseract OCR which may contain noise, " +
                              "broken words, or mis-recognized characters. " +
                              "Clean the text: fix obvious OCR errors, restore correct spacing and line breaks, " +
                              "and preserve the original structure (tables, lists, headings). " +
                              "Return ONLY the corrected text with no preamble, explanation, or markdown wrapper."
                },
                new DeepSeekMessage
                {
                    Role = "user",
                    Content = rawOcrText
                }
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
            _logger.LogWarning("DeepSeek post-processing failed {Status} — falling back to raw OCR text. Body: {Body}",
                httpResponse.StatusCode, responseJson);

            // Graceful fallback: return raw OCR text rather than failing completely
            return new DeepSeekExtractionResult
            {
                ExtractedText = rawOcrText,
                TokensUsed = 0,
                Success = true
            };
        }

        var response = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson, JsonOptions);
        if (response?.Choices is not { Count: > 0 })
        {
            return new DeepSeekExtractionResult { ExtractedText = rawOcrText, TokensUsed = 0, Success = true };
        }

        return new DeepSeekExtractionResult
        {
            ExtractedText = response.Choices[0].Message.Content,
            TokensUsed = response.Usage?.TotalTokens ?? 0,
            Success = true
        };
    }
}
