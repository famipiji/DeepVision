using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Npgsql;
using Dapper;

namespace iVault.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _configuration;
    private readonly PaddleOcrService _ocrService;
    private readonly string _queueName = "ivault.ocr.queue";
    private readonly string _connectionString;

    public Worker(ILogger<Worker> logger, IConfiguration configuration, PaddleOcrService ocrService)
    {
        _logger = logger;
        _configuration = configuration;
        _ocrService = ocrService;
        _connectionString = _configuration.GetConnectionString("DefaultConnection")
                            ?? throw new InvalidOperationException("Connection string not found.");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var rabbitHost = _configuration["RabbitMQ:HostName"] ?? "localhost";
        var factory = new ConnectionFactory
        {
            HostName = rabbitHost,
            UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
            Password = _configuration["RabbitMQ:Password"] ?? "guest"
        };

        using var connection = await factory.CreateConnectionAsync(stoppingToken);
        using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(queue: _queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            _logger.LogInformation(" [x] Received Task: {Message}", message);

            try
            {
                var taskData = JsonSerializer.Deserialize<OcrTask>(message, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (taskData != null)
                {
                    await ProcessOcrTask(taskData);
                }
                await channel.BasicAckAsync(ea.DeliveryTag, false, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing OCR task");
            }
        };

        await channel.BasicConsumeAsync(_queueName, false, consumer, cancellationToken: stoppingToken);
        _logger.LogInformation(" Worker waiting for messages...");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private async Task ProcessOcrTask(OcrTask task)
    {
        // 1. Fetch the file bytes from SeaweedFS
        byte[] fileBytes = await GetFileFromStorage(task);

        // 2. Call the OCR service to get the Raw JSON[cite: 14]
        string? rawJsonFromOcr = await _ocrService.RecognizeAsync(fileBytes, task.FileName);

        if (string.IsNullOrEmpty(rawJsonFromOcr))
        {
            _logger.LogError("OCR failed or returned empty for Record {Id}", task.RecordId);
            return;
        }

        var ocrResponse = JsonSerializer.Deserialize<PaddleOcrResponse>(rawJsonFromOcr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (ocrResponse?.Data == null || ocrResponse.Data.Count == 0)
        {
            _logger.LogWarning("No OCR data found for Record {Id}", task.RecordId);
            return;
        }

        var pageGroups = ocrResponse.Data.GroupBy(d => d.Page).ToList();
        string fullDocumentText = string.Join(" ", ocrResponse.Data.Select(p => p.Text));
        double documentAvgConfidence = ocrResponse.Data.Average(p => p.Confidence);

        // 1. Update SQL Database (Contents Table)
        using (var db = new NpgsqlConnection(_connectionString))
        {
            const string sql = @"
        UPDATE contents 
        SET raw_ocr_content = @CleanText, 
            ocr_coordinates_json = @RawJson, 
            ocr_status = 'Completed',
            ocr_completed_at = @Now
        WHERE record_id = @RecordId AND is_latest = true";

            await db.ExecuteAsync(sql, new
            {
                CleanText = fullDocumentText, // The extracted text only
                RawJson = rawJsonFromOcr,     // The full JSON with coordinates
                Now = DateTime.UtcNow,
                RecordId = task.RecordId
            });
        }

        // Index to Elasticsearch
        foreach (var pageGroup in pageGroups)
        {
            int pageNum = pageGroup.Key;
            string pageText = string.Join(" ", pageGroup.Select(p => p.Text));
            double pageAvgConfidence = pageGroup.Average(p => p.Confidence);

            // CRITICAL: Ensure these three variables are passed in this order
            await IndexToElastic(task.RecordId, pageNum, pageText, pageAvgConfidence);
}
    }

    private async Task<byte[]> GetFileFromStorage(OcrTask task)
    {
        using var client = new HttpClient();
        var seaweedUri = _configuration["SeaweedFS:BaseAddress"] ?? "http://ivault-filer:8888";

        // 1. Extract sharding levels from the FileId string
        string fileIdStr = task.FileId.ToString().Replace("-", "").ToLower();
        string level1 = fileIdStr.Substring(0, 2);
        string level2 = fileIdStr.Substring(2, 2);

        // 2. Construct the exact path seen in your SeaweedFS Filer screenshot
        // Pattern: /ivault/a6/8c/a68c8061..._5 pages.pdf
        var fullUrl = $"{seaweedUri.TrimEnd('/')}/ivault/{level1}/{level2}/{task.FileId}_{task.FileName}";

        _logger.LogInformation("Downloading from SeaweedFS: {Url}", fullUrl);

        var response = await client.GetAsync(fullUrl);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("SeaweedFS Download Failed: {Status} - {Error}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Could not fetch file from SeaweedFS at {fullUrl}");
        }

        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task IndexToElastic(Guid recordId, int pageNumber, string content, double confidence)
    {
        using var httpClient = new HttpClient();
        var elasticBase = _configuration["Elasticsearch:Uri"] ?? "http://search:9200";

        // Use the ID pattern seen in your search result: {recordId}_{pageNumber}
        var elasticUri = $"{elasticBase}/ivault-records/_doc/{recordId}_{pageNumber}";

        var doc = new
        {
            id = $"{recordId}_{pageNumber}", // Matches the ID format in your ES hit
            recordId = recordId,             // This is your "Link" for the UI
            pageNumber = pageNumber,
            content = content,             // Ensure this is the 'pageText' from ProcessOcrTask
            averageConfidence = confidence, // Ensure this is the 'pageAvgConfidence'
            timestamp = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(doc);
        var data = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogInformation("Indexing Page {Page} for Record {Id} to Elasticsearch...", pageNumber, recordId);

        var response = await httpClient.PutAsync(elasticUri, data);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.LogError("Elasticsearch Indexing Failed: {Error}", error);
        }
    }
}

// Updated Task class to match your API payload
// These must be present at the bottom of Worker.cs
public class OcrTask
{
    public Guid RecordId { get; set; }
    public Guid FileId { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public class PaddleOcrResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public List<OcrDataItem>? Data { get; set; }
}

public class OcrDataItem
{
    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}