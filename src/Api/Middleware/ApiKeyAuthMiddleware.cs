using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Domain.Entities;
using Aegis.Application.DTOs;
using Aegis.Application.Services;
using Aegis.Application.Interfaces;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Aegis.Shared.Options;
using Microsoft.AspNetCore.Http;

namespace Aegis.Api.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SecurityOptions _options;

    public ApiKeyAuthMiddleware(RequestDelegate next, IOptions<SecurityOptions> options)
    {
        _next = next;
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();

        // Allow unauthenticated access to health & swagger
        if (path == "/health" || (path != null && path.StartsWith("/swagger")))
        {
            await _next(context);
            return;
        }

        // 1️⃣ Try x-api-key
        if (context.Request.Headers.TryGetValue("x-api-key", out var xApiKey))
        {
            if (IsValidKey(xApiKey))
            {
                await _next(context);
                return;
            }
        }

        // 2️⃣ Try Authorization: Bearer <key>
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var value = authHeader.ToString();
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var token = value.Substring("Bearer ".Length).Trim();
                if (IsValidKey(token))
                {
                    await _next(context);
                    return;
                }
            }
        }

        // ❌ If neither worked → unauthorized
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { error = "Invalid or missing API key" })
        );
    }

    private bool IsValidKey(string provided)
    {
        return !string.IsNullOrWhiteSpace(provided)
            && !string.IsNullOrWhiteSpace(_options.ApiKey)
            && string.Equals(provided.Trim(), _options.ApiKey, StringComparison.Ordinal);
    }
}