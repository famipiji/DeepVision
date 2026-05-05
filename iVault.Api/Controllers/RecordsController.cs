using Elastic.Clients.Elasticsearch;
using iVault.Api.Data;
using iVault.Api.DTOs;
using iVault.Api.Models;
using iVault.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace iVault.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class RecordsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEncryptionService _encryptionService;
        private readonly IFileStorageService _fileStorageService;
        private readonly ElasticsearchClient _elasticsearchClient;
        private readonly iVault.Api.Interfaces.IMessageProducer _messageProducer;

        public RecordsController(
            AppDbContext context,
            IEncryptionService encryptionService,
            IFileStorageService fileStorageService,
            ElasticsearchClient elasticsearchClient,
            iVault.Api.Interfaces.IMessageProducer messageProducer)
        {
            _context = context;
            _encryptionService = encryptionService;
            _fileStorageService = fileStorageService;
            _elasticsearchClient = elasticsearchClient;
            _messageProducer = messageProducer;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadRecord([FromForm] RecordUploadDto dto)
        {
            try
            {
                if (dto.File == null || dto.File.Length == 0)
                {
                    return BadRequest("No file was received. Please check your form-data key name.");
                }


                var rawMetadata2 = Request.Form["metadata"].ToString();
                Console.WriteLine($"RAW FORM DATA: {rawMetadata2}");


                // 1. Setup IDs and Metadata Storage
                // 1. Setup IDs and Metadata Storage
                var recordId = Guid.NewGuid();
                var contentId = Guid.NewGuid();

                var encryptedMetadataDict = new Dictionary<string, string>();
                var searchMetadataDict = new Dictionary<string, object>();

                // New logic: Parse the JSON string manually
                if (!string.IsNullOrEmpty(dto.Metadata))
                {
                    try
                    {
                        var rawMetadata = JsonSerializer.Deserialize<Dictionary<string, string>>(dto.Metadata);
                        if (rawMetadata != null)
                        {
                            foreach (var field in rawMetadata)
                            {
                                encryptedMetadataDict[field.Key] = _encryptionService.Encrypt(field.Value);
                                searchMetadataDict[field.Key] = field.Value;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        //_logger.LogError("Metadata Deserialization Error: {Msg}", ex.Message);
                    }
                }

                var record = new Record
                {
                    Id = recordId,
                    RecordDefinitionId = dto.RecordDefinitionId,
                    CreatedAt = DateTime.UtcNow,
                    Metadata = JsonSerializer.Serialize(encryptedMetadataDict)
                };

                // 2. File Storage with Hex Sharding[cite: 2]
                string filePath = "no-file";
                if (dto.File != null)
                {
                    string idString = contentId.ToString();
                    string level1 = idString.Substring(0, 2);
                    string level2 = idString.Substring(2, 2);
                    string shardedPath = $"/ivault/{level1}/{level2}/{idString}_{dto.File.FileName}";

                    filePath = await _fileStorageService.UploadFileAsync(dto.File, shardedPath);
                }

                var content = new Content
                {
                    Id = contentId,
                    RecordId = record.Id,
                    VersionNumber = 1,
                    FileId = contentId.ToString(),
                    FilePath = filePath,
                    FileName = dto.File?.FileName ?? "unknown",
                    OcrStatus = "Pending",
                    IsLatest = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Records.Add(record);
                _context.Contents.Add(content);
                await _context.SaveChangesAsync();

                // 3. Sync to Elasticsearch
                var searchDto = new SearchRecordDto
                {
                    // CRITICAL: Must match Worker's naming convention for Page 0[cite: 8]
                    Id = $"{record.Id}_0",
                    RecordId = record.Id,
                    RecordDefinitionId = record.RecordDefinitionId,
                    FileName = content.FileName,
                    Metadata = searchMetadataDict,
                    PageNumber = 0,
                    Content = "" // Empty until Worker finishes[cite: 8]
                };

                var elasticResponse = await _elasticsearchClient.IndexAsync(searchDto, idx => idx
                    .Index("ivault-records")
                    .Id(searchDto.Id)
                    .Refresh(Refresh.True) // Ensure searchable immediately[cite: 8]
                );

                if (!elasticResponse.IsSuccess())
                {
                    throw new Exception($"Elasticsearch Sync Error: {elasticResponse.DebugInformation}");
                }

                // 4. Trigger OCR Worker[cite: 8]
                var ocrEvent = new iVault.Api.Events.RecordIngestedEvent
                {
                    RecordId = record.Id,
                    FileId = content.FileId,
                    FileName = content.FileName,
                    OccurredOn = DateTime.UtcNow
                };

                await _messageProducer.PublishRecordIngestedAsync(ocrEvent);

                return Ok(new { id = record.Id, message = "Record processed and queued for OCR." });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? ex.Message;
                return StatusCode(500, $"Vault Error: {inner}");
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetRecord(Guid id)
        {
            try
            {
                var record = await _context.Records.FirstOrDefaultAsync(r => r.Id == id);
                if (record == null) return NotFound("Record not found.");

                var encryptedData = JsonSerializer.Deserialize<Dictionary<string, string>>(record.Metadata);
                var decryptedMetadata = new Dictionary<string, string>();

                if (encryptedData != null)
                {
                    foreach (var entry in encryptedData)
                    {
                        decryptedMetadata[entry.Key] = _encryptionService.Decrypt(entry.Value);
                    }
                }

                var content = await _context.Contents.FirstOrDefaultAsync(c => c.RecordId == id && c.IsLatest);

                return Ok(new
                {
                    record.Id,
                    record.CreatedAt,
                    Metadata = decryptedMetadata,
                    FileUrl = content != null ? $"http://localhost:9000{content.FilePath}" : null,
                    FileName = content?.FileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Vault Decryption Error: {ex.Message}");
            }
        }

        [HttpGet("file/{id}")]
        public async Task<IActionResult> GetFile(Guid id)
        {
            try
            {
                var content = await _context.Contents.FirstOrDefaultAsync(c => c.RecordId == id && c.IsLatest);
                if (content == null) return NotFound("File not found.");

                using var client = new HttpClient();
                var response = await client.GetAsync($"http://localhost:9000{content.FilePath}");
                if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, "Storage error.");

                var stream = await response.Content.ReadAsStreamAsync();
                return File(stream, "application/octet-stream", content.FileName);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal Proxy Error: {ex.Message}");
            }
        }
    }
}