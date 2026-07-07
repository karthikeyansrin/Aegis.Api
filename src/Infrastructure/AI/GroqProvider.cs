using Aegis.Application.Interfaces;
using Aegis.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Infrastructure.AI;

/// <summary>
/// ILLMProvider implementation backed by the Groq API (OpenAI-compatible endpoint).
/// </summary>
public sealed class GroqProvider : ILLMProvider
{
    public string ProviderName => "groq";

    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqProvider> _logger;

    public GroqProvider(HttpClient httpClient, IOptions<OpenAIOptions> options, ILogger<GroqProvider> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var apiKey = options.Value.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("GROQ_API_KEY is missing");

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<ChatCompletionResult> CreateChatCompletionAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content })
        };

        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                var content = new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json");

                _logger.LogInformation("[DEBUG] FINAL GROQ URL = {BaseAddress}v1/chat/completions", _httpClient.BaseAddress);

                using var resp = await _httpClient.PostAsync("v1/chat/completions", content, ct);
                var raw = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode >= 500 && attempt == 1)
                    {
                        await Task.Delay(200, ct);
                        continue;
                    }

                    return new ChatCompletionResult { Success = false, Content = raw, RawJson = raw };
                }

                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var choice = choices[0];
                    if (choice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentTxt))
                    {
                        var text = contentTxt.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return new ChatCompletionResult { Success = true, Content = text, RawJson = raw };
                        }
                    }
                }

                return new ChatCompletionResult { Success = true, Content = raw, RawJson = raw };
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                if (attempt == 1) await Task.Delay(200, ct);
            }
            catch (HttpRequestException) when (attempt == 1)
            {
                await Task.Delay(200, ct);
            }
        }

        return new ChatCompletionResult { Success = false };
    }
}
