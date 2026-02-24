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
        try
        {
            var apiKey = _configuration["DeepSeek:ApiKey"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_DEEPSEEK_API_KEY_HERE")
                return new DocumentDetailsResult { Success = false, ErrorMessage = "DeepSeek API key not configured." };

            var baseUrl = _configuration["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com";
            var model = _configuration["DeepSeek:Model"] ?? "deepseek-chat";

            const string systemPrompt =
                "You are an expert document analyst specializing in invoices, receipts, and business documents. " +
                "Analyze the provided document text and extract key information. " +
                "Return ONLY a valid JSON object — no markdown, no code fences, no explanation. " +
                "Use null for any field not found in the document. " +
                "Use this exact structure:\n" +
                "{\n" +
                "  \"documentType\": \"Invoice|Receipt|PurchaseOrder|Statement|CreditNote|DeliveryNote|Other\",\n" +
                "  \"invoiceNumber\": null,\n" +
                "  \"invoiceDate\": null,\n" +
                "  \"dueDate\": null,\n" +
                "  \"vendorName\": null,\n" +
                "  \"customerName\": null,\n" +
                "  \"subTotal\": null,\n" +
                "  \"taxAmount\": null,\n" +
                "  \"totalAmount\": null,\n" +
                "  \"currency\": null,\n" +
                "  \"paymentTerms\": null,\n" +
                "  \"additionalFields\": {}\n" +
                "}\n" +
                "Place any other important key-value pairs found in the document into additionalFields.";

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

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("DeepSeek details extraction failed {Status}: {Body}", httpResponse.StatusCode, responseJson);
                return new DocumentDetailsResult { Success = false, ErrorMessage = "DeepSeek request failed." };
            }

            var deepSeekResponse = JsonSerializer.Deserialize<DeepSeekResponse>(responseJson, JsonOptions);
            var content = deepSeekResponse?.Choices?.FirstOrDefault()?.Message?.Content;

            if (string.IsNullOrWhiteSpace(content))
                return new DocumentDetailsResult { Success = false, ErrorMessage = "Empty response from DeepSeek." };

            // Strip markdown code fences if the model includes them despite instructions
            content = content.Trim();
            if (content.StartsWith("```"))
            {
                var firstNewLine = content.IndexOf('\n');
                var lastFence = content.LastIndexOf("```");
                if (firstNewLine >= 0 && lastFence > firstNewLine)
                    content = content[(firstNewLine + 1)..lastFence].Trim();
            }

            var parseOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var details = JsonSerializer.Deserialize<DocumentDetails>(content, parseOptions);

            _logger.LogInformation("Document details extracted: type={Type}, invoice={Invoice}",
                details?.DocumentType, details?.InvoiceNumber);

            return new DocumentDetailsResult { Success = true, Details = details };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting document details");
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
