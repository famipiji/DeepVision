using Elastic.Clients.Elasticsearch;
using System.Net.Http.Headers;
using System.IO;

public class PaddleOcrService
{
    private readonly HttpClient _httpClient;
    // We explicitly include TIFF and PDF here
    private readonly string[] _supportedExtensions =
        { ".jpg", ".jpeg", ".png", ".bmp", ".pdf", ".tif", ".tiff", ".webp" };

    public PaddleOcrService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        // This must match your docker-compose port
        _httpClient.BaseAddress = new Uri("http://ivault-ocr:8000");
        // Multi-page PDFs take time, so we set a long timeout
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
    }

    public async Task<string?> RecognizeAsync(byte[] fileBytes, string fileName)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(fileBytes);

            var extension = Path.GetExtension(fileName).ToLower();
            string mimeType = extension switch
            {
                ".pdf" => "application/pdf",
                ".tif" or ".tiff" => "image/tiff",
                ".png" => "image/png",
                _ => "image/jpeg"
            };

            fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

            content.Add(fileContent, "file", fileName);

            var response = await _httpClient.PostAsync("/ocr", content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"--- OCR Success for {fileName} ---");
                return jsonResponse;
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"OCR Server Error: {response.StatusCode} - {error}");
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Exception during OCR Processing: {ex.Message}");
            return null;
        }
    }
}