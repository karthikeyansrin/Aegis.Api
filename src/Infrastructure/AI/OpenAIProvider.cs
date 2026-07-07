using Aegis.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Infrastructure.AI;

/// <summary>
/// Placeholder ILLMProvider implementation for the OpenAI API.
/// Wire up with a real API key and HttpClient to activate.
/// </summary>
public sealed class OpenAIProvider : ILLMProvider
{
    public string ProviderName => "openai";

    private readonly ILogger<OpenAIProvider> _logger;

    public OpenAIProvider(ILogger<OpenAIProvider> logger)
    {
        _logger = logger;
    }

    public Task<ChatCompletionResult> CreateChatCompletionAsync(
        string model,
        IEnumerable<ChatMessage> messages,
        CancellationToken ct = default)
    {
        _logger.LogWarning("OpenAIProvider is a placeholder and has not been configured. Returning empty result.");
        return Task.FromResult(new ChatCompletionResult { Success = false, Content = null });
    }
}
