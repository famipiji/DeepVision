using System.Text.Json.Serialization;

namespace iVault.Api.Events;

public record RecordIngestedEvent
{
    // The Primary Key from your PostgreSQL 'Records' table
    [JsonPropertyName("recordId")]
    public Guid RecordId { get; init; }

    // The File Handle returned by SeaweedFS
    [JsonPropertyName("fileId")]
    public required string FileId { get; init; }

    // Helpful for logging and debugging in the OCR logs
    [JsonPropertyName("fileName")]
    public required string FileName { get; init; }

    // Track when the message was born
    [JsonPropertyName("occurredOn")]
    public DateTime OccurredOn { get; init; } = DateTime.UtcNow;
}