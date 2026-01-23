using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Aegis.Api.Services;

public class GroqService : IGroqService
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };

    public GroqService(HttpClient httpClient, string apiKey)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentNullException(nameof(apiKey));

        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<ChatCompletionResult> CreateChatCompletionAsync(string model, IEnumerable<ChatMessage> messages, CancellationToken ct = default)
    {
        var payload = new
        {
            model,
            messages = messages.Select(m => new { role = m.Role, content = m.Content })
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        // basic retry: try up to 2 times for transient errors
        for (int attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                using var resp = await _httpClient.PostAsync("/v1/chat/completions", content, ct);
                var raw = await resp.Content.ReadAsStringAsync(ct);

                if (!resp.IsSuccessStatusCode)
                {
                    // transient 5xx → retry
                    if ((int)resp.StatusCode >= 500 && attempt == 1)
                    {
                        await Task.Delay(200 * attempt, ct);
                        continue;
                    }

                    return new ChatCompletionResult { Success = false, RawJson = raw };
                }

                // try to extract a simple text response from common OpenAI-compatible shape
                try
                {
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var first = choices[0];
                        if (first.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentElem))
                        {
                            var text = contentElem.GetString();
                            return new ChatCompletionResult { Success = true, Content = text, RawJson = raw };
                        }

                        if (first.TryGetProperty("text", out var textElem))
                        {
                            var text = textElem.GetString();
                            return new ChatCompletionResult { Success = true, Content = text, RawJson = raw };
                        }
                    }
                }
                catch
                {
                    // fall through to return raw
                }

                return new ChatCompletionResult { Success = true, RawJson = raw };
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout
                if (attempt == 1) await Task.Delay(200 * attempt, ct);
            }
            catch (HttpRequestException) when (attempt == 1)
            {
                await Task.Delay(200 * attempt, ct);
            }
        }

        return new ChatCompletionResult { Success = false };
    }
}
