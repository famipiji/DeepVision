using iVault.Api.Data;
using iVault.Api.DTOs;
using iVault.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace iVault.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SearchController : ControllerBase
    {
        private readonly IElasticsearchService _elasticsearchService;
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;

        public SearchController(
        IElasticsearchService elasticsearchService,
        AppDbContext context,
        IEncryptionService encryptionService)
        {
            _elasticsearchService = elasticsearchService;
            _context = context;
            _encryptionService = encryptionService;
        }

        
        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                    return Ok(new List<SearchRecordDto>());

                // 1. Get collapsed results from Elasticsearch (Text/Confidence/Page info)
                var results = await _elasticsearchService.SearchRecordsAsync(q);
                var resultsList = results.ToList();

                // 2. Hydrate missing data from SQL
                foreach (var record in resultsList)
                {
                    // Fetch the main Record to get the Metadata
                    var sqlRecord = await _context.Records
                        .FirstOrDefaultAsync(r => r.Id == record.RecordId);

                    if (sqlRecord != null)
                    {
                        var mainContent = await _context.Contents.FirstOrDefaultAsync(c => c.RecordId == sqlRecord.Id && c.IsLatest);

                        if (mainContent != null)
                        {
                            record.FileName = mainContent.FileName;
            
                            // 2. LOGIC: If this hit is Page 0 (No Content), steal text from the actual document[cite: 9]
                            if (record.PageNumber == 0 && string.IsNullOrEmpty(record.Content))
                            {
                                // Fallback to SQL text so the snippet logic has something to work with[cite: 9]
                                var ExtractedText = mainContent.RawOcrContent;
                                if (!string.IsNullOrEmpty(ExtractedText))
                                {
                                    record.Content = ExtractedText;
                                }
                                 else
                                {
                                     record.Content = "No text content available.";
                                }
                                
                            }
                        }

                        record.Metadata ??= new Dictionary<string, object>();
                        // If ES metadata is empty, decrypt and populate from SQL
                        if (record.Metadata == null || record.Metadata.Count == 0)
                        {
                            var encryptedData = JsonSerializer.Deserialize<Dictionary<string, string>>(sqlRecord.Metadata);
                            if (encryptedData != null)
                            {
                                foreach (var entry in encryptedData)
                                {
                                    // Decrypt before sending to UI
                                    record.Metadata[entry.Key] = _encryptionService.Decrypt(entry.Value);
                                }
                            }
                        }

                        // Attach the FieldSchema so the UI knows how to display these fields
                        var definition = await _context.RecordDefinitions
                            .FirstOrDefaultAsync(d => d.Id == sqlRecord.RecordDefinitionId);

                        if (definition != null)
                        {
                            record.FieldSchema = definition.FieldSchema;
                            record.RecordDefinitionId = sqlRecord.RecordDefinitionId;
                        }

                        // ADD THIS: Fetch the filename from the associated Content record
                        var content = await _context.Contents
                            .FirstOrDefaultAsync(c => c.RecordId == sqlRecord.Id && c.IsLatest);

                        if (content != null)
                        {
                            record.FileName = content.FileName; // Hydrate the empty ES field
                        }

                        // ADD CONTENT FOR SEARCH SNIPPET
                        if (!string.IsNullOrEmpty(record.Content) && !string.IsNullOrEmpty(q))
                        {
                            // Find keyword, create window, and apply <mark> tags
                            int index = record.Content.IndexOf(q, StringComparison.OrdinalIgnoreCase);
                            if (index != -1)
                            {
                                int start = Math.Max(0, index - 40);
                                int length = Math.Min(record.Content.Length - start, 140);
                                string snippet = record.Content.Substring(start, length);

                                record.Content = Regex.Replace(
                                    (start > 0 ? "..." : "") + snippet + (start + length < record.Content.Length ? "..." : ""),
                                    Regex.Escape(q),
                                    match => $"<mark>{match.Value}</mark>",
                                    RegexOptions.IgnoreCase
                                ); 
                            }
                        }
                    }
                }

                return Ok(resultsList);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Search Error: {ex.Message}");
            }
        }

        [HttpPut("update-metadata/{recordId}")]
        public async Task<IActionResult> UpdateMetadata(Guid recordId, [FromBody] Dictionary<string, object> updatedMetadata)
        {
            try
            {
                // 1. Update SQL Database
                var sqlRecord = await _context.Records.FirstOrDefaultAsync(r => r.Id == recordId);
                if (sqlRecord == null) return NotFound("Record not found in database.");

                // Re-encrypt the data before saving to SQL
                var encryptedData = new Dictionary<string, string>();
                foreach (var entry in updatedMetadata)
                {
                    encryptedData[entry.Key] = _encryptionService.Encrypt(entry.Value.ToString());
                }

                sqlRecord.Metadata = JsonSerializer.Serialize(encryptedData);
                await _context.SaveChangesAsync();

                // 2. Update Elasticsearch (Page 0)
                // We only update Page 0 because that is where we store the metadata for the record
                var esId = $"{recordId}_0";
                var updateSuccess = await _elasticsearchService.UpdateMetadataAsync(esId, updatedMetadata);

                if (!updateSuccess)
                {
                    return StatusCode(500, "Metadata saved to DB, but failed to update Elasticsearch index.");
                }

                return Ok(new { message = "Metadata updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

    }
}