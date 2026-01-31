using System.Text.Json;

namespace Aegis.Api.Middleware;

public class ApiKeyOptions
{
    public string Key { get; }
    public ApiKeyOptions(string key) => Key = key;
}

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _options;

    private const string AuthorizationHeader = "Authorization";
    private const string BearerPrefix = "Bearer ";

    public ApiKeyAuthMiddleware(RequestDelegate next, ApiKeyOptions options)
    {
        _next = next;
        _options = options;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();

        // ✅ Allow unauthenticated access to health & swagger
        if (path == "/health" || path.StartsWith("/swagger"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(AuthorizationHeader, out var authHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Missing Authorization header" })
            );
            return;
        }

        var headerValue = authHeader.ToString();

        if (!headerValue.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Invalid Authorization scheme" })
            );
            return;
        }

        var token = headerValue.Substring(BearerPrefix.Length).Trim();

        if (string.IsNullOrWhiteSpace(_options.Key) ||
            !string.Equals(token, _options.Key, StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(new { error = "Invalid API key" })
            );
            return;
        }

        await _next(context);
    }
}
