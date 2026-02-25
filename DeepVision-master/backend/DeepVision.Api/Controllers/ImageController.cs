using DeepVision.Api.Models;
using DeepVision.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace DeepVision.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageController : ControllerBase
{
    private readonly IImageProcessingService _imageProcessingService;
    private readonly IDeepSeekService _deepSeekService;
    private readonly ILogger<ImageController> _logger;

    private const long MaxFileSizeBytes = 20 * 1024 * 1024; // 20 MB

    public ImageController(
        IImageProcessingService imageProcessingService,
        IDeepSeekService deepSeekService,
        ILogger<ImageController> logger)
    {
        _imageProcessingService = imageProcessingService;
        _deepSeekService = deepSeekService;
        _logger = logger;
    }

    /// <summary>
    /// Upload an image or PDF to clean and extract text using DeepSeek AI.
    /// For PDFs, up to 5 pages are processed and text is combined.
    /// </summary>
    [HttpPost("process")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProcessImageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProcessImageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProcessImageResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProcessImageResponse>> ProcessImage(IFormFile image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new ProcessImageResponse { Success = false, ErrorMessage = "No file provided." });

        if (image.Length > MaxFileSizeBytes)
            return BadRequest(new ProcessImageResponse
            {
                Success = false,
                ErrorMessage = $"File exceeds the {MaxFileSizeBytes / 1024 / 1024} MB limit."
            });

        _logger.LogInformation("Processing file: {FileName} ({Size} bytes, {Type})",
            image.FileName, image.Length, image.ContentType);

        try
        {
            // Step 1 — Clean / render
            ImageProcessingResult processed;
            try
            {
                processed = await _imageProcessingService.ProcessImageAsync(image);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ProcessImageResponse { Success = false, ErrorMessage = ex.Message });
            }

            // Step 2 — Extract text + document details in a single LLM call per page
            var pageTexts = new List<string>(processed.AllProcessedPageBytes.Count);
            int totalTokens = 0;
            bool allFailed = true;
            string? lastDeepSeekError = null;
            DocumentDetails? documentDetails = null;

            for (int i = 0; i < processed.AllProcessedPageBytes.Count; i++)
            {
                var pageResult = await _deepSeekService.ExtractAllAsync(
                    processed.AllProcessedPageBytes[i], processed.MimeType);

                if (pageResult.Success)
                {
                    allFailed = false;
                    totalTokens += pageResult.TokensUsed;

                    var text = processed.ProcessedPageCount > 1
                        ? $"=== Page {i + 1} of {processed.ProcessedPageCount} ===\n{pageResult.ExtractedText}"
                        : pageResult.ExtractedText;

                    pageTexts.Add(text);

                    // Use document details from the first successful page
                    documentDetails ??= pageResult.DocumentDetails;
                }
                else
                {
                    lastDeepSeekError = pageResult.ErrorMessage;
                    _logger.LogWarning("DeepSeek failed on page {Page}: {Error}", i + 1, pageResult.ErrorMessage);
                    if (processed.ProcessedPageCount > 1)
                        pageTexts.Add($"=== Page {i + 1} — extraction failed: {pageResult.ErrorMessage} ===");
                }
            }

            if (allFailed)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new ProcessImageResponse
                {
                    Success = false,
                    ErrorMessage = lastDeepSeekError ?? "DeepSeek could not extract text from any page.",
                    OriginalImageBase64 = Convert.ToBase64String(processed.OriginalBytes),
                    ProcessedImageBase64 = Convert.ToBase64String(processed.ProcessedBytes),
                    ImageMimeType = processed.MimeType
                });
            }

            var combinedText = string.Join("\n\n", pageTexts);

            return Ok(new ProcessImageResponse
            {
                Success = true,
                OriginalImageBase64 = Convert.ToBase64String(processed.OriginalBytes),
                ProcessedImageBase64 = Convert.ToBase64String(processed.ProcessedBytes),
                ImageMimeType = processed.MimeType,
                ExtractedText = combinedText,
                DocumentDetails = documentDetails,
                Metadata = new ImageMetadata
                {
                    OriginalWidth = processed.OriginalWidth,
                    OriginalHeight = processed.OriginalHeight,
                    ProcessedWidth = processed.ProcessedWidth,
                    ProcessedHeight = processed.ProcessedHeight,
                    Format = processed.Format,
                    FileSizeBytes = processed.FileSizeBytes,
                    ProcessingSteps = processed.ProcessingSteps,
                    TokensUsed = totalTokens,
                    PageCount = processed.PageCount,
                    ProcessedPageCount = processed.ProcessedPageCount
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error processing {FileName}", image.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, new ProcessImageResponse
            {
                Success = false,
                ErrorMessage = $"An unexpected error occurred: {ex.Message}"
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok", timestamp = DateTime.UtcNow });
}
