using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Domain.Entities;
using Aegis.Application.DTOs;
using Aegis.Application.Services;
using Aegis.Application.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Aegis.Infrastructure.AI;

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public GroqService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;

        var apiKey = config["GROQ_API_KEY"];
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
                
                Console.WriteLine($"[DEBUG] FINAL GROQ URL = {_httpClient.BaseAddress}v1/chat/completions");

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

                // fallback but still successful call
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
