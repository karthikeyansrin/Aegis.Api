using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.Interfaces;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Application.Services;

/// <summary>
/// Replaces IntelligenceEngine. Runs all five extraction responsibilities as
/// discrete private methods and returns typed ThreatIndicator objects.
///
/// Pipeline:
///   ExtractWithRegex()  → raw indicators from pattern matching
///   ExtractWithLLM()    → supplementary indicators from the LLM (when regex finds nothing)
///   NormalizeEntities() → trim / strip whitespace / sanitize values in-place
///   Deduplicate()       → remove items already present in the session
///   (Persistence)       → caller merges result into ConversationSession via MergeThreatIndicators()
/// </summary>
public sealed class ThreatIndicatorEngine : IThreatIndicatorEngine
{
    private readonly ILLMProvider _llm;

    // ── Compiled regex patterns (identical to previous IntelligenceEngine) ──────

    private static readonly Regex UpiRegex =
        new(@"\b[a-zA-Z0-9._-]{2,}@[a-zA-Z]{2,15}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex =
        new(@"(?<!\d)(?:\+?91[\s-]?)?(?:\d[\s-]?){10}(?!\d)",
            RegexOptions.Compiled);

    private static readonly Regex UrlRegex =
        new(@"https?://\S+|www\.\S+|\b(?:[\w-]+\.)+[A-Za-z]{2,}\S*\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IfscRegex =
        new(@"\b[A-Za-z]{4}0[A-Za-z0-9]{6}\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex AccountNumberRegex =
        new(@"(?<!\d)\d{11,18}(?!\d)",
            RegexOptions.Compiled);

    public ThreatIndicatorEngine(ILLMProvider llm) =>
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));

    // ── Public entry point ────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ThreatIndicator>> ExtractAsync(
        string sessionId,
        string message,
        IReadOnlyList<ThreatIndicator> existingIndicators,
        bool useLlmFallback = true,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(message))
            return Array.Empty<ThreatIndicator>();

        try
        {
            // 1. Regex extraction
            var rawIndicators = ExtractWithRegex(sessionId, message);

            // 2. LLM fallback when regex finds nothing
            if (rawIndicators.Count == 0 && useLlmFallback)
                rawIndicators = await ExtractWithLLM(sessionId, message, ct);

            // 3. Normalize values
            NormalizeEntities(rawIndicators);

            // 4. Deduplicate against existing session indicators
            var newIndicators = Deduplicate(rawIndicators, existingIndicators);

            // 5. Persistence: caller calls session.MergeThreatIndicators(result)
            return newIndicators;
        }
        catch
        {
            // Stability > extraction — swallow and return empty
            return Array.Empty<ThreatIndicator>();
        }
    }

    // ── Step 1: Regex extraction ──────────────────────────────────────────────

    private static List<ThreatIndicator> ExtractWithRegex(string sessionId, string message)
    {
        var results = new List<ThreatIndicator>();

        foreach (Match m in UpiRegex.Matches(message))
        {
            results.Add(Make(sessionId, IndicatorType.UpiId, m.Value,
                "Regex-detected UPI ID", "regex", weight: 1.0));
        }

        foreach (Match m in PhoneRegex.Matches(message))
        {
            results.Add(Make(sessionId, IndicatorType.PhoneNumber, m.Value,
                "Regex-detected Indian phone number", "regex", weight: 1.0));
        }

        foreach (Match m in UrlRegex.Matches(message))
        {
            results.Add(Make(sessionId, IndicatorType.Url, m.Value,
                "Regex-detected URL", "regex", weight: 0.9));
        }

        var ifscs    = IfscRegex.Matches(message).Select(x => x.Value).ToArray();
        var accounts = AccountNumberRegex.Matches(message).Select(x => x.Value).ToArray();

        if (accounts.Length > 0)
        {
            var ifsc = ifscs.FirstOrDefault();
            foreach (var acc in accounts)
            {
                results.Add(Make(sessionId, IndicatorType.BankAccount, acc,
                    "Regex-detected bank account number", "regex", weight: 1.0,
                    metadata: ifsc));
            }
        }

        foreach (var ifsc in ifscs)
        {
            results.Add(Make(sessionId, IndicatorType.IfscCode, ifsc,
                "Regex-detected IFSC code", "regex", weight: 0.8));
        }

        return results;
    }

    // ── Step 2: LLM extraction ────────────────────────────────────────────────

    private async Task<List<ThreatIndicator>> ExtractWithLLM(
        string sessionId, string message, CancellationToken ct)
    {
        var results = new List<ThreatIndicator>();

        var messages = new[]
        {
            new ChatMessage("system",
                "You are an intelligence extraction agent. From the user's message, find all UPI IDs, " +
                "Indian phone numbers, URLs, and bank account numbers with IFSC codes. " +
                "Return ONLY a single, valid JSON object. Do not include any other text. " +
                "Format: {\"upi_ids\":[\"name@bank\"],\"phone_numbers\":[\"919876543210\"]," +
                "\"urls\":[\"http://example.com\"],\"bank_accounts\":[{\"account_number\":\"123\",\"ifsc\":\"BANK0123456\"}]}"),
            new ChatMessage("user", message)
        };

        var llmResult = await _llm.CreateChatCompletionAsync("llama-3.1-8b-instant", messages, ct);

        if (llmResult?.Success != true || string.IsNullOrWhiteSpace(llmResult.Content))
            return results;

        try
        {
            using var doc = JsonDocument.Parse(llmResult.Content);
            var root = doc.RootElement;

            if (root.TryGetProperty("upi_ids", out var upiArr))
                foreach (var e in upiArr.EnumerateArray())
                    AddLlmString(results, sessionId, IndicatorType.UpiId, e.GetString(), "LLM-extracted UPI ID");

            if (root.TryGetProperty("phone_numbers", out var phoneArr))
                foreach (var e in phoneArr.EnumerateArray())
                    AddLlmString(results, sessionId, IndicatorType.PhoneNumber, e.GetString(), "LLM-extracted phone number");

            if (root.TryGetProperty("urls", out var urlArr))
                foreach (var e in urlArr.EnumerateArray())
                    AddLlmString(results, sessionId, IndicatorType.Url, e.GetString(), "LLM-extracted URL");

            if (root.TryGetProperty("bank_accounts", out var bankArr))
            {
                foreach (var e in bankArr.EnumerateArray())
                {
                    var acc  = e.TryGetProperty("account_number", out var an) ? an.GetString() : null;
                    var ifsc = e.TryGetProperty("ifsc",           out var ic) ? ic.GetString() : null;
                    AddLlmString(results, sessionId, IndicatorType.BankAccount, acc,
                        "LLM-extracted bank account", metadata: ifsc);
                }
            }
        }
        catch { /* malformed JSON — return what we have */ }

        return results;
    }

    // ── Step 3: Normalization ─────────────────────────────────────────────────

    private static void NormalizeEntities(List<ThreatIndicator> indicators)
    {
        for (int i = 0; i < indicators.Count; i++)
        {
            var ind = indicators[i];
            var normalized = ind.Type switch
            {
                IndicatorType.PhoneNumber =>
                    Regex.Replace(ind.Value.Trim(), @"[\s-]", ""),
                _ => ind.Value.Trim()
            };

            if (!string.Equals(normalized, ind.Value, StringComparison.Ordinal))
            {
                // Swap with normalized version (init properties require recreation)
                indicators[i] = new ThreatIndicator
                {
                    Id = ind.Id,
                    SessionId = ind.SessionId,
                    Type = ind.Type,
                    Code = ind.Code,
                    Value = normalized,
                    Description = ind.Description,
                    Source = ind.Source,
                    Weight = ind.Weight,
                    Metadata = ind.Metadata
                };
            }
        }
    }

    // ── Step 4: Deduplication ─────────────────────────────────────────────────

    private static IReadOnlyList<ThreatIndicator> Deduplicate(
        List<ThreatIndicator> candidates,
        IReadOnlyList<ThreatIndicator> existing)
    {
        var seenKeys = new HashSet<string>(
            existing.Select(i => $"{i.Type}:{i.Value}"),
            StringComparer.OrdinalIgnoreCase);

        var result = new List<ThreatIndicator>();
        foreach (var ind in candidates)
        {
            if (string.IsNullOrWhiteSpace(ind.Value)) continue;
            var key = $"{ind.Type}:{ind.Value}";
            if (seenKeys.Add(key))
                result.Add(ind);
        }
        return result.AsReadOnly();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static ThreatIndicator Make(
        string sessionId, IndicatorType type, string value,
        string description, string source, double weight, string? metadata = null)
    {
        return new ThreatIndicator
        {
            SessionId   = sessionId,
            Type        = type,
            Code        = type.ToString().ToUpperInvariant(),
            Value       = value,
            Description = description,
            Source      = source,
            Weight      = weight,
            Metadata    = metadata
        };
    }

    private static void AddLlmString(
        List<ThreatIndicator> target, string sessionId,
        IndicatorType type, string? value, string description,
        string? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        target.Add(Make(sessionId, type, value, description, "llm", weight: 0.7, metadata: metadata));
    }
}
