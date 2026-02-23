using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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
