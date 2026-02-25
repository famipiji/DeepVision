using PDFtoImage;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace DeepVision.Api.Services;

public class ImageProcessingService : IImageProcessingService
{
    private static readonly string[] AllowedImageMimeTypes =
        ["image/jpeg", "image/jpg", "image/png", "image/webp", "image/bmp", "image/tiff"];

    private const int MaxPdfPages = 5;
    private const int PdfDpi = 150;

    public async Task<ImageProcessingResult> ProcessImageAsync(IFormFile file)
    {
        var mimeType = file.ContentType.ToLowerInvariant();

        using var rawStream = new MemoryStream();
        await file.CopyToAsync(rawStream);
        var rawBytes = rawStream.ToArray();

        if (mimeType == "application/pdf")
            return await ProcessPdfAsync(rawBytes, file.Length);

        if (!AllowedImageMimeTypes.Contains(mimeType))
            throw new InvalidOperationException($"Unsupported file format: {mimeType}. Supported: JPEG, PNG, WebP, BMP, TIFF, PDF.");

        return await ProcessSingleImageAsync(rawBytes, mimeType, file.Length);
    }

    // ── PDF ───────────────────────────────────────────────────────────────────

    private async Task<ImageProcessingResult> ProcessPdfAsync(byte[] pdfBytes, long fileSize)
    {
        var steps = new List<string>();

        int totalPages = Conversion.GetPageCount(pdfBytes, password: null);
        int pagesToProcess = Math.Min(totalPages, MaxPdfPages);
        steps.Add($"PDF detected: {totalPages} total page(s) — processing {pagesToProcess} (max {MaxPdfPages})");

        // Render & clean all pages
        var allCleanedPages = new List<byte[]>(pagesToProcess);
        for (int i = 0; i < pagesToProcess; i++)
        {
            var pagePng = RenderPdfPageToPng(pdfBytes, i);
            steps.Add($"Rendered page {i + 1}/{pagesToProcess} to PNG at {PdfDpi} DPI");
            var cleaned = await CleanImageBytesAsync(pagePng, steps, pageIndex: i);
            allCleanedPages.Add(cleaned);
        }

        // First page as the "original" display image (rendered but not cleaned)
        var firstPageOriginal = RenderPdfPageToPng(pdfBytes, 0);

        // Capture dimensions from first cleaned page
        using var cleanedStream = new MemoryStream(allCleanedPages[0]);
        using var cleanedImage = await Image.LoadAsync<Rgba32>(cleanedStream);
        int processedW = cleanedImage.Width;
        int processedH = cleanedImage.Height;

        using var origStream = new MemoryStream(firstPageOriginal);
        using var origImage = await Image.LoadAsync<Rgba32>(origStream);
        int origW = origImage.Width;
        int origH = origImage.Height;

        steps.Add("Encoded all pages as PNG (lossless)");

        return new ImageProcessingResult
        {
            OriginalBytes = firstPageOriginal,
            ProcessedBytes = allCleanedPages[0],
            AllProcessedPageBytes = allCleanedPages,
            PageCount = totalPages,
            ProcessedPageCount = pagesToProcess,
            OriginalWidth = origW,
            OriginalHeight = origH,
            ProcessedWidth = processedW,
            ProcessedHeight = processedH,
            Format = "PNG",
            MimeType = "image/png",
            FileSizeBytes = fileSize,
            ProcessingSteps = steps
        };
    }

    private static byte[] RenderPdfPageToPng(byte[] pdfBytes, int pageIndex)
    {
        using var stream = new MemoryStream();
        Conversion.SavePng(stream, pdfBytes, pageIndex, password: null, new RenderOptions(Dpi: PdfDpi));
        return stream.ToArray();
    }

    // ── Single image ──────────────────────────────────────────────────────────

    private async Task<ImageProcessingResult> ProcessSingleImageAsync(byte[] rawBytes, string mimeType, long fileSize)
    {
        var steps = new List<string>();

        using var stream = new MemoryStream(rawBytes);
        using var image = await Image.LoadAsync<Rgba32>(stream);
        int origW = image.Width;
        int origH = image.Height;
        steps.Add($"Loaded image: {origW}x{origH}");

        image.Mutate(ctx => ctx.AutoOrient());
        steps.Add("Auto-oriented (EXIF correction)");

        const int maxDimension = 2048;
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));
            steps.Add($"Resized to fit within {maxDimension}x{maxDimension}");
        }

        image.Mutate(ctx => ctx
            .Contrast(1.15f)
            .Brightness(1.05f)
            .GaussianSharpen(1.2f));
        steps.Add("Applied contrast enhancement (contrast +15%, brightness +5%, sharpen)");

        image.Mutate(ctx => ctx.GaussianBlur(0.4f));
        steps.Add("Applied mild denoising (Gaussian blur σ=0.4)");

        image.Mutate(ctx => ctx.GaussianSharpen(0.8f));
        steps.Add("Re-sharpened after denoising");

        int processedW = image.Width;
        int processedH = image.Height;

        using var outStream = new MemoryStream();
        await image.SaveAsPngAsync(outStream, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed });
        var processedBytes = outStream.ToArray();
        steps.Add("Encoded as PNG (lossless output)");

        return new ImageProcessingResult
        {
            OriginalBytes = rawBytes,
            ProcessedBytes = processedBytes,
            AllProcessedPageBytes = [processedBytes],
            PageCount = 1,
            ProcessedPageCount = 1,
            OriginalWidth = origW,
            OriginalHeight = origH,
            ProcessedWidth = processedW,
            ProcessedHeight = processedH,
            Format = "PNG",
            MimeType = "image/png",
            FileSizeBytes = fileSize,
            ProcessingSteps = steps
        };
    }

    // ── Shared cleaning pipeline ──────────────────────────────────────────────

    private static async Task<byte[]> CleanImageBytesAsync(byte[] imageBytes, List<string> steps, int pageIndex)
    {
        using var inStream = new MemoryStream(imageBytes);
        using var image = await Image.LoadAsync<Rgba32>(inStream);

        const int maxDimension = 2048;
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(maxDimension, maxDimension),
                Mode = ResizeMode.Max,
                Sampler = KnownResamplers.Lanczos3
            }));
        }

        image.Mutate(ctx => ctx
            .AutoOrient()
            .Contrast(1.15f)
            .Brightness(1.05f)
            .GaussianSharpen(1.2f));

        image.Mutate(ctx => ctx.GaussianBlur(0.4f));
        image.Mutate(ctx => ctx.GaussianSharpen(0.8f));

        steps.Add($"Cleaned page {pageIndex + 1} (contrast, sharpen, denoise)");

        using var outStream = new MemoryStream();
        await image.SaveAsPngAsync(outStream, new PngEncoder { CompressionLevel = PngCompressionLevel.BestSpeed });
        return outStream.ToArray();
    }
}
