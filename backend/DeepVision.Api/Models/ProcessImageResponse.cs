namespace DeepVision.Api.Models;

public class ProcessImageResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? OriginalImageBase64 { get; set; }
    public string? ProcessedImageBase64 { get; set; }
    public string? ImageMimeType { get; set; }
    public string? ExtractedText { get; set; }
    public ImageMetadata? Metadata { get; set; }
}

public class ImageMetadata
{
    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int ProcessedWidth { get; set; }
    public int ProcessedHeight { get; set; }
    public string Format { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public List<string> ProcessingSteps { get; set; } = [];
    public int TokensUsed { get; set; }
    public int PageCount { get; set; } = 1;
    public int ProcessedPageCount { get; set; } = 1;
}
