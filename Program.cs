using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Api.Middleware;
using Aegis.Api.Services;
using System.Diagnostics;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel timeouts and limits (reasonable defaults)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

// Configure JSON (System.Text.Json)
builder.Services
    .AddControllers()
    .AddJsonOptions(opts =>
    {
        opts.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opts.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        opts.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        // Add converters if needed, e.g. for enums:
        opts.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    });

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Aegis API",
        Version = "v1",
        Description = "Simple scam-analysis API"
    });

    // Include XML comments if the .xml file exists (optional)
    try
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            c.IncludeXmlComments(xmlPath);
        }
    }
    catch
    {
        // ignore if reflection or file access fails
    }
});

// Configuration: read environment variables (e.g. AEGIS_API_KEY)
builder.Configuration.AddEnvironmentVariables();
var apiKey = builder.Configuration["AEGIS_API_KEY"] ?? "dev-secret-key";
builder.Services.AddSingleton(new ApiKeyOptions(apiKey));

// Register application services
// Keep SimpleScamAnalysisService as a concrete fallback and register ScamDetectionService as the primary IScamAnalysisService
builder.Services.AddSingleton<SimpleScamAnalysisService>();
builder.Services.AddSingleton<IScamAnalysisService, ScamDetectionService>();

// Register a named HttpClient for Groq and then register IGroqService via factory to pass the API key
var groqBase = builder.Configuration["GROQ_BASE_URL"] ?? "https://api.groq.com/openai/";
var groqApiKey = builder.Configuration["GROQ_API_KEY"];

if (string.IsNullOrWhiteSpace(groqApiKey))
{
    throw new InvalidOperationException("GROQ_API_KEY is not configured");
}

builder.Services.AddHttpClient<IGroqService, GroqService>(client =>
{
    client.BaseAddress = new Uri(groqBase);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler());

// Conversation store and agent/extraction services
builder.Services.AddSingleton(new ConversationStore(TimeSpan.FromMinutes(45)));
builder.Services.AddSingleton<HoneypotAgentService>();
builder.Services.AddSingleton<IntelligenceExtractionService>();

var app = builder.Build();

// Middleware / pipeline
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Aegis API v1");
    c.RoutePrefix = "swagger"; // UI available at /swagger
});

// Minimal health endpoint (anonymous) returning only a safe status
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

// Global request logging middleware (lightweight)
app.Use(async (context, next) =>
{
    var sw = Stopwatch.StartNew();
    try
    {
        // Log minimal request info to Console for now (can integrate ILogger later)
        Console.WriteLine($"[Request] {context.Request.Method} {context.Request.Path}");
        await next();
    }
    finally
    {
        sw.Stop();
        Console.WriteLine($"[Request] {context.Request.Method} {context.Request.Path} completed {context.Response.StatusCode} in {sw.ElapsedMilliseconds}ms");
    }
});

// Global JSON exception handler
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        var payload = JsonSerializer.Serialize(new { error = "internal_server_error", detail = ex.Message });
        await context.Response.WriteAsync(payload);
    }
});

app.UseRouting();

// API-key middleware (leaves /swagger and /health anonymous)
app.UseMiddleware<ApiKeyAuthMiddleware>();

app.MapControllers();

// Ensure graceful shutdown disposes store
var store = app.Services.GetService<ConversationStore>();
app.Lifetime.ApplicationStopping.Register(() => store?.Dispose());

app.Run();