using Serilog;
using Serilog.Sinks.Elasticsearch;
using iVault.Api.Data;
using Microsoft.EntityFrameworkCore;
using iVault.Api.Services;
using Elastic.Clients.Elasticsearch;
using RabbitMQ.Client;
using iVault.Api.Interfaces;

// --- STEP 1: INITIALIZE SERILOG ---
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9201"))
    {
        AutoRegisterTemplate = true,
        IndexFormat = $"ivault-logs-{DateTime.UtcNow:yyyy-MM}"
    })
    .CreateLogger();

try
{
    Log.Information("Starting iVault Plus ECM API...");
    var builder = WebApplication.CreateBuilder(args);

    // --- STEP 2: SERVICES CONFIGURATION ---
    builder.Host.UseSerilog();
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Database Setup[cite: 16]
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    builder.Services.AddDbContext<AppDbContext>(options =>
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

    // Infrastructure Services
    builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
    builder.Services.AddHttpClient<IFileStorageService, SeaweedStorageService>();

    // --- STEP 3: ELASTICSEARCH SETUP ---
    var esSettings = new ElasticsearchClientSettings(new Uri("http://localhost:9201"))
        .DefaultIndex("ivault-records");
    var esClient = new ElasticsearchClient(esSettings);
    builder.Services.AddSingleton(esClient);
    builder.Services.AddScoped<IElasticsearchService, ElasticsearchService>();

    // --- STEP 4: RABBITMQ SETUP ---
    var rabbitMQSettings = builder.Configuration.GetSection("RabbitMQ");
    builder.Services.AddSingleton<IConnection>(sp =>
    {
        var factory = new ConnectionFactory()
        {
            HostName = rabbitMQSettings["HostName"],
            Port = int.Parse(rabbitMQSettings["Port"] ?? "5672"),
            UserName = builder.Configuration["RabbitMQ:UserName"] ?? "guest",
            Password = builder.Configuration["RabbitMQ:Password"] ?? "guest"
        };
        // Use sync for DI registration safely
        return factory.CreateConnectionAsync().GetAwaiter().GetResult();
    });
    builder.Services.AddScoped<IMessageProducer, RabbitMQProducer>();

    // --- STEP 5: CORS ---
    builder.Services.AddCors(options =>
    {
        //options.AddPolicy("AllowAll", policy =>
        options.AddPolicy("AllowReactApp", policy =>
        {
            policy.WithOrigins("http://localhost:3000") // Or your frontend URL
                  .AllowAnyMethod()
                  .AllowAnyHeader();
                  //.AllowCredentials();
        });
    });

    

    var app = builder.Build();

    // --- STEP 6: MIDDLEWARE & ENV SETUP ---
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        
        // Optional: Run ES Index check safely
        try
        {
            var client = app.Services.GetRequiredService<ElasticsearchClient>();
            if (!client.Indices.Exists("ivault-records").Exists)
            {
                client.Indices.Create("ivault-records");
            }
        }
        catch (Exception ex)
        {
            Log.Warning("Could not auto-create ES index: {Message}", ex.Message);
        }
    }
    app.UseRouting();
    app.UseCors("AllowReactApp");
    app.UseAuthorization();
    app.UseSerilogRequestLogging();
    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "The application failed to start correctly.");
}
finally
{
    Log.CloseAndFlush();
}