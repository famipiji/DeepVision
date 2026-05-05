using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace iVault.Api.DTOs
{
    public class SearchRecordDto
    {
        // 1. The Unique ID for Elasticsearch (e.g., "RecordGUID_Page1")
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        // 2. Link back to the main SQL record
        [JsonPropertyName("recordId")]
        public Guid RecordId { get; set; }

        [JsonPropertyName("fileName")]
        public string FileName { get; set; } = string.Empty;

        // 3. Keep your Metadata for the UI to show details
        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();

        [JsonPropertyName("recordDefinitionId")]
        public Guid RecordDefinitionId { get; set; }

        // 4. NEW: The core parts for the multi-page fix
        [JsonPropertyName("pageNumber")]
        public int PageNumber { get; set; }

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("averageConfidence")]
        public double AverageConfidence { get; set; }

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("fieldSchema")]
        public object? FieldSchema { get; set; }
    }
}