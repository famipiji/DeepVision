namespace DeepVision.Api.Services;

public interface IImageProcessingService
{
    Task<ImageProcessingResult> ProcessImageAsync(IFormFile file);
}

public class ImageProcessingResult
{
    /// <summary>First page (or only image) in original form — used for the "Original" tab in the UI.</summary>
    public byte[] OriginalBytes { get; set; } = [];

    /// <summary>First page (or only image) after cleaning — used for the "Cleaned" tab in the UI.</summary>
    public byte[] ProcessedBytes { get; set; } = [];

    /// <summary>All cleaned pages (1 element for images, N for PDFs). Used for per-page DeepSeek calls.</summary>
    public List<byte[]> AllProcessedPageBytes { get; set; } = [];

    public int PageCount { get; set; } = 1;
    public int ProcessedPageCount { get; set; } = 1;

    public int OriginalWidth { get; set; }
    public int OriginalHeight { get; set; }
    public int ProcessedWidth { get; set; }
    public int ProcessedHeight { get; set; }
    public string Format { get; set; } = string.Empty;
    public string MimeType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public List<string> ProcessingSteps { get; set; } = [];
}
