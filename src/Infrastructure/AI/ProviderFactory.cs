using Aegis.Application.Interfaces;
using Aegis.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;

namespace Aegis.Infrastructure.AI;

/// <summary>
/// Resolves the active ILLMProvider by reading DefaultProvider from OpenAIOptions.
/// Falls back to Groq if the configured name is unknown.
/// </summary>
public sealed class ProviderFactory
{
    private readonly IReadOnlyDictionary<string, ILLMProvider> _providers;
    private readonly string _defaultProviderName;

    public ProviderFactory(IEnumerable<ILLMProvider> providers, IOptions<OpenAIOptions> options)
    {
        var dict = new Dictionary<string, ILLMProvider>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in providers)
            dict[p.ProviderName] = p;

        _providers = dict;
        _defaultProviderName = options.Value.DefaultProvider ?? "groq";
    }

    /// <summary>Returns the configured default provider.</summary>
    public ILLMProvider GetDefault()
    {
        if (_providers.TryGetValue(_defaultProviderName, out var provider))
            return provider;

        // Hard fallback to groq if the configured name is not registered
        if (_providers.TryGetValue("groq", out var groq))
            return groq;

        throw new InvalidOperationException(
            $"No LLM provider registered for '{_defaultProviderName}' and no groq fallback found.");
    }

    /// <summary>Retrieves a provider by explicit name.</summary>
    public ILLMProvider GetByName(string name)
    {
        if (_providers.TryGetValue(name, out var provider))
            return provider;

        throw new InvalidOperationException($"No LLM provider registered with name '{name}'.");
    }
}
