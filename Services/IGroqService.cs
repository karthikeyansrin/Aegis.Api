using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Api.Services;

public record ChatMessage(string Role, string Content);

public sealed class ChatCompletionResult
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? RawJson { get; init; }
}

public interface IGroqService
{
    /// <summary>
    /// Create a chat-style completion using an OpenAI-compatible messages array.
    /// </summary>
    Task<ChatCompletionResult> CreateChatCompletionAsync(string model, IEnumerable<ChatMessage> messages, CancellationToken ct = default);
}
