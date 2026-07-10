using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Application.Interfaces;

public record ChatMessage(string Role, string Content);

public sealed class ChatCompletionResult
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? RawJson { get; init; }
}

/// <summary>
/// Abstraction over any OpenAI-compatible chat-completion provider.
/// </summary>
public interface ILLMProvider
{
    /// <summary>The logical name of this provider (e.g. "groq", "openai").</summary>
    string ProviderName { get; }

    Task<ChatCompletionResult> CreateChatCompletionAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        CancellationToken ct = default);
}
