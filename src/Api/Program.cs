using Aegis.Domain.Entities;
using Aegis.Application.DTOs;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Aegis.Api.Middleware;
using Aegis.Application.Interfaces;
using Aegis.Application.Services;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.AI;
using Aegis.Application.Extensions;
using Aegis.Infrastructure.Extensions;
using Aegis.Api.Extensions;
using System.Diagnostics;
using Microsoft.OpenApi.Models;
using Serilog;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

// Configure Kestrel timeouts and limits (reasonable defaults)
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(30);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

// Configure JSON (System.Text.Json)
builder.Services
    .AddControllers(options =>
    {
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    })
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

// Delegate service registrations to extension methods
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddSecurity(builder.Configuration);

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
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    try
    {
        // Log minimal request info to Console for now (can integrate ILogger later)
        logger.LogInformation($"[Request] {context.Request.Method} {context.Request.Path}");
        await next();
    }
    finally
    {
        sw.Stop();
        logger.LogInformation($"[Request] {context.Request.Method} {context.Request.Path} completed {context.Response.StatusCode} in {sw.ElapsedMilliseconds}ms");
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
