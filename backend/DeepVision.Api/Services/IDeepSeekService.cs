using DeepVision.Api.Models;

namespace DeepVision.Api.Services;

public interface IDeepSeekService
{
    Task<DeepSeekExtractionResult> ExtractTextFromImageAsync(byte[] imageBytes, string mimeType);
    Task<DocumentDetailsResult> ExtractDocumentDetailsAsync(string cleanedText);
}

public class DeepSeekExtractionResult
{
    public string ExtractedText { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DocumentDetailsResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DocumentDetails? Details { get; set; }
}
