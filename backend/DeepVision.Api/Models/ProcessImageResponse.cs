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
    public DocumentDetails? DocumentDetails { get; set; }
}

public class DocumentDetails
{
    public string? DocumentType { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? InvoiceDate { get; set; }
    public string? DueDate { get; set; }
    public string? VendorName { get; set; }
    public string? CustomerName { get; set; }
    public string? SubTotal { get; set; }
    public string? TaxAmount { get; set; }
    public string? TotalAmount { get; set; }
    public string? Currency { get; set; }
    public string? PaymentTerms { get; set; }
    public Dictionary<string, string>? AdditionalFields { get; set; }
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
