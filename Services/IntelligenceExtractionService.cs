using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Api.Models;

namespace Aegis.Api.Services;

public sealed class IntelligenceExtractionService
{
    private readonly IGroqService _groq;
    private readonly ConversationStore _store;

    private static readonly Regex UpiRegex =
        new(@"\b[\w.\-]{2,}@(?!.*\b(ybl|apl)\b)[A-Za-z]{2,}\b", RegexOptions.Compiled);

    private static readonly Regex UpiIdRegex =
        new(@"\b[\w.\-]+@(ybl|apl)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex =
        new(@"(?<!\d)(?:\+?91[\s-]?)?(?:\d[\s-]?){10,14}(?!\d)", RegexOptions.Compiled);
    
    private static readonly Regex UrlRegex =
        new(@"https?://\S+|www\.\S+|\b(?:[\w-]+\.)+[A-Za-z]{2,}\S*\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex IfscRegex =
        new(@"\b[A-Za-z]{4}0[A-Za-z0-9]{6}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex AccountNumberRegex =
        new(@"(?<!\d)\d{9,18}(?!\d)", RegexOptions.Compiled);
    
    public IntelligenceExtractionService(IGroqService groq, ConversationStore store)
    {
        _groq = groq;
        _store = store;
    }
    
    /// <summary>
    /// Extract intelligence from message and MERGE INTO SESSION.
    /// Always returns the session's aggregated intelligence.
    /// Never replaces objects. Never throws.
    /// </summary>
    public async Task<ExtractedIntelligence> ExtractAsync(
        string sessionId,
        string message,
        bool useLLMFallback = true,
        CancellationToken ct = default)
    {
        var session = _store.GetOrCreateSession(sessionId);
        var target = session.AggregatedIntelligence;

        if (string.IsNullOrWhiteSpace(message))
            return target;

        try
        {
            // --------------------
            // REGEX EXTRACTION
            // --------------------
            
            foreach (Match m in UpiIdRegex.Matches(message))
            {
                var v = m.Value.Trim();
                if (!target.UpiIds.Contains(v))
                    target.UpiIds.Add(v);
            }

            foreach (Match m in UpiRegex.Matches(message))
            {
                var v = m.Value.Trim();
                if (!target.UpiIds.Contains(v))
                    target.UpiIds.Add(v);
            }

            foreach (Match m in PhoneRegex.Matches(message))
            {
                var v = Regex.Replace(m.Value, @"[\s-]", "");
                if (!target.PhoneNumbers.Contains(v))
                    target.PhoneNumbers.Add(v);
            }

            foreach (Match m in UrlRegex.Matches(message))
            {
                var v = m.Value.Trim();
                if (!target.Urls.Contains(v))
                    target.Urls.Add(v);
            }

            var ifscs = IfscRegex.Matches(message)
                .Select(x => x.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var accounts = AccountNumberRegex.Matches(message)
                .Select(x => x.Value)
                .Distinct()
                .ToArray();

            if (accounts.Length > 0)
            {
                var ifsc = ifscs.FirstOrDefault();
                foreach (var acc in accounts)
                {
                    if (!target.BankAccounts.Any(b => b.AccountNumber == acc))
                    {
                        target.BankAccounts.Add(new BankAccount
                        {
                            AccountNumber = acc,
                            Ifsc = ifsc
                        });
                    }
                }
            }

            // --------------------
            // LLM FALLBACK
            // --------------------

            var anyFound =
                target.UpiIds.Count > 0 ||
                target.PhoneNumbers.Count > 0 ||
                target.Urls.Count > 0 ||
                target.BankAccounts.Count > 0;

            if (!anyFound && useLLMFallback)
            {
                var messages = new[]
                {
                    new ChatMessage("system",
                        "You are an intelligence extraction agent. From the user's message, find all UPI IDs, Indian phone numbers, URLs, and bank account numbers with IFSC codes. " +
                        "Return ONLY a single, valid JSON object with the following structure. Do not include any other text, just the JSON. " +
                        "Example: {\"upi_ids\": [\"name@bank\"], \"phone_numbers\": [\"919876543210\"], \"urls\": [\"http://example.com\"], \"bank_accounts\": [{\"account_number\": \"123456789012\", \"ifsc\": \"BANK0123456\"}]}."),
                    new ChatMessage("user", message)
                };

                var llm = await _groq.CreateChatCompletionAsync(
                    "llama-3.1-8b-instant",
                    messages,
                    ct);

                if (llm?.Success == true && !string.IsNullOrWhiteSpace(llm.Content))
                {
                    using var doc = JsonDocument.Parse(llm.Content);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("upi_ids", out var upiArr))
                    {
                        foreach (var e in upiArr.EnumerateArray())
                        {
                            var s = e.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && !target.UpiIds.Contains(s))
                                target.UpiIds.Add(s);
                        }
                    }

                    if (root.TryGetProperty("phone_numbers", out var phoneArr))
                    {
                        foreach (var e in phoneArr.EnumerateArray())
                        {
                            var s = e.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                            {
                                var cleaned = Regex.Replace(s, @"[\s-]", "");
                                if (!target.PhoneNumbers.Contains(cleaned))
                                    target.PhoneNumbers.Add(cleaned);
                            }
                        }
                    }

                    if (root.TryGetProperty("urls", out var urlArr))
                    {
                        foreach (var e in urlArr.EnumerateArray())
                        {
                            var s = e.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && !target.Urls.Contains(s))
                                target.Urls.Add(s);
                        }
                    }

                    if (root.TryGetProperty("bank_accounts", out var bankArr))
                    {
                        foreach (var e in bankArr.EnumerateArray())
                        {
                            var acc = e.GetProperty("account_number").GetString();
                            var ifsc = e.TryGetProperty("ifsc", out var i) ? i.GetString() : null;

                            if (!string.IsNullOrWhiteSpace(acc) &&
                                !target.BankAccounts.Any(b => b.AccountNumber == acc))
                            {
                                target.BankAccounts.Add(new BankAccount
                                {
                                    AccountNumber = acc,
                                    Ifsc = ifsc
                                });
                            }
                        }
                    }
                }
            }
        }
        catch
        {
            // swallow everything — stability > extraction
        }

        return target;
    }
}
