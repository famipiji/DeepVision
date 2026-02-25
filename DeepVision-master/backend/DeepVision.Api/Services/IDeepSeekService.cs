using DeepVision.Api.Models;

namespace DeepVision.Api.Services;

public interface IDeepSeekService
{
    Task<CombinedExtractionResult> ExtractAllAsync(byte[] imageBytes, string mimeType);
}

public class CombinedExtractionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string ExtractedText { get; set; } = string.Empty;
    public DocumentDetails? DocumentDetails { get; set; }
    public int TokensUsed { get; set; }
}
