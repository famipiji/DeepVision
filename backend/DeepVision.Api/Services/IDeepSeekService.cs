namespace DeepVision.Api.Services;

public interface IDeepSeekService
{
    Task<DeepSeekExtractionResult> ExtractTextFromImageAsync(byte[] imageBytes, string mimeType);
}

public class DeepSeekExtractionResult
{
    public string ExtractedText { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}
