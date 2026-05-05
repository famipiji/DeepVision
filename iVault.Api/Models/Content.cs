
using System.ComponentModel.DataAnnotations.Schema;

[Table("contents")]
public class Content
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("record_id")]
    public Guid RecordId { get; set; }

    [Column("version_number")]
    public int VersionNumber { get; set; } = 1;

    [Column("file_id")]
    public string FileId { get; set; } = string.Empty;

    [Column("file_path")]
    public string FilePath { get; set; } = string.Empty;

    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;

    [Column("is_latest")]
    public bool IsLatest { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("ocr_status")]
    public string OcrStatus { get; set; } = "Pending"; // Pending, Processing, Completed, Failed

    // To store the structured OCR text for previewing later
    [Column("raw_ocr_content")]
    public string? RawOcrContent { get; set; }

    // To store the coordinate JSON from PaddleOCR
    [Column("ocr_coordinates_json")]
    public string? OcrCoordinatesJson { get; set; }

    [Column("ocr_completed_at")]
    public DateTime? OcrCompletedAt { get; set; }
}