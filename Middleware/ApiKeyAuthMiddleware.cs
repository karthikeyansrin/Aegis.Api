using System.Net;

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
        var expectedKey = Environment.GetEnvironmentVariable("HONEYPOT_API_KEY");

        if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Missing Authorization header" });
            return;
        }

        var headerValue = authHeader.ToString();

        if (!headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid Authorization scheme" });
            return;
        }

        var token = headerValue.Substring("Bearer ".Length).Trim();

        if (string.IsNullOrWhiteSpace(expectedKey) ||
            !string.Equals(token, expectedKey.Trim(), StringComparison.Ordinal))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid API key" });
            return;
        }

        await _next(context);
    }
}
