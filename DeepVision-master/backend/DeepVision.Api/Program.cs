using DeepVision.Api.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DeepVision API",
        Version = "v1",
        Description = "Image cleaning and text extraction powered by DeepSeek AI"
    });
});

// HttpClient for DeepSeek API calls
builder.Services.AddHttpClient<IDeepSeekService, DeepSeekService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
});

builder.Services.AddScoped<IImageProcessingService, ImageProcessingService>();

// CORS — allow the Angular dev server
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// Increase max request body size (default is 30 MB but set explicitly)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 20 * 1024 * 1024; // 20 MB
});

// ── Build ─────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Tesseract language data — auto-download on first run ──────────────────────
await EnsureTessDataAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeepVision API v1");
        c.RoutePrefix = "swagger";
    });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ── Tesseract setup helper ─────────────────────────────────────────────────────
static async Task EnsureTessDataAsync(WebApplication app)
{
    var config = app.Configuration;
    var tessDataPath = config["Tesseract:DataPath"]
        ?? Path.Combine(AppContext.BaseDirectory, "tessdata");
    var language = config["Tesseract:Language"] ?? "eng";
    var langFile = Path.Combine(tessDataPath, $"{language}.traineddata");

    if (File.Exists(langFile))
    {
        app.Logger.LogInformation("Tesseract data found at {Path}", langFile);
        return;
    }

    app.Logger.LogInformation("Tesseract language data not found — downloading {Lang}.traineddata ...", language);
    Directory.CreateDirectory(tessDataPath);

    // tessdata_fast is smaller (~4 MB for eng) and accurate enough for this use case
    var url = $"https://github.com/tesseract-ocr/tessdata_fast/raw/main/{language}.traineddata";

    using var http = new HttpClient();
    http.Timeout = TimeSpan.FromMinutes(2);

    try
    {
        var bytes = await http.GetByteArrayAsync(url);
        await File.WriteAllBytesAsync(langFile, bytes);
        app.Logger.LogInformation("Tesseract data saved ({Size} KB)", bytes.Length / 1024);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex,
            "Could not download Tesseract data. " +
            "Download manually from https://github.com/tesseract-ocr/tessdata_fast " +
            "and place {File} in {Dir}", $"{language}.traineddata", tessDataPath);
    }
}
