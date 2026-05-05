using Elastic.Clients.Elasticsearch;
using iVault.Api.Data; // Ensure this points to your API's Data folder
using iVault.Worker;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console()
    .CreateLogger();

try
{
    Log.Information("Starting iVault.Worker...");

    var builder = Host.CreateApplicationBuilder(args);

    // 2. Tell the Host to use Serilog
    builder.Services.AddSerilog();

    // ... your existing DB registration ...
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString));

    // ... your existing RabbitMQ registration ...
    var rabbitMQSettings = builder.Configuration.GetSection("RabbitMQ");
    builder.Services.AddSingleton<IConnection>(sp =>
    {
        var factory = new ConnectionFactory()
        {
            HostName = rabbitMQSettings["HostName"],
            Port = int.Parse(rabbitMQSettings["Port"] ?? "5672"),
            UserName = rabbitMQSettings["UserName"],
            Password = rabbitMQSettings["Password"],

            AutomaticRecoveryEnabled = true
        };
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    });
    // Add this to your Worker's Program.cs
    builder.Services.AddSingleton<iVault.Api.Services.IEncryptionService, iVault.Api.Services.EncryptionService>();
    builder.Services.AddHttpClient();

    builder.Services.AddHostedService<Worker>();

    // Register PaddleOcrService with HttpClient support
    builder.Services.AddHttpClient<PaddleOcrService>();

    // Register Elasticsearch client
    var settings = new ElasticsearchClientSettings(new Uri("http://search:9200"));
    builder.Services.AddSingleton(new ElasticsearchClient(settings));

    // Register the Named HttpClient for SeaweedFS (used in your Worker)
    builder.Services.AddHttpClient("SeaweedFS", client => {
        client.BaseAddress = new Uri("http://ivault-filer:8888"); // Adjust to your Seaweed port
    });

    builder.Services.AddHostedService<Worker>();
    var host = builder.Build();
    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Worker terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}


