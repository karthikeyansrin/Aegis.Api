using Aegis.Infrastructure.AI;
using Aegis.Application.Interfaces;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Domain.Entities;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Application.Services;

public sealed class IntelligenceEngine : IIntelligenceEngine
{
    private readonly ILLMProvider _groq;
    private readonly IConversationRepository _store;

    private static readonly Regex UpiRegex =
    new("\b[a-zA-Z0-9._-]{2,}@[a-zA-Z]{2,15}\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex =
        new("(?<!\d)(?:\+?91[\s-]?)?(?:\d[\s-]?){10}(?!\d)", RegexOptions.Compiled);
    
    private static readonly Regex UrlRegex =
        new("https?://\S+|www\.\S+|\b(?:[\w-]+\.)+[A-Za-z]{2,}\S*\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex IfscRegex =
        new("\b[A-Za-z]{4}0[A-Za-z0-9]{6}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Regex AccountNumberRegex =
        new("(?<!\d)\d{11,18}(?!\d)", RegexOptions.Compiled);
    
    public IntelligenceEngine(ILLMProvider groq, IConversationRepository store)
    {
        _groq = groq;
        _store = store;
    }
    
    public async Task<ExtractedIntelligence> ExtractAsync(
        string sessionId,
        string message,
        bool useLLMFallback = true,
        CancellationToken ct = default)
    {
        var session = await _store.GetOrCreateSessionAsync(sessionId, ct);
        var target = session.AggregatedIntelligence;

        if (string.IsNullOrWhiteSpace(message))
            return target;

        try
        {
            var extracted = ExtractWithRegex(message);
            NormalizeEntities(extracted);
            var deduped = Deduplicate(extracted, target);

            MergeEntities(target, deduped);

            var anyFound = target.UpiIds.Count > 0 || target.PhoneNumbers.Count > 0 || target.Urls.Count > 0 || target.BankAccounts.Count > 0;

            if (!anyFound && useLLMFallback)
            {
                var llmExtracted = await ExtractWithLLM(message, ct);
                NormalizeEntities(llmExtracted);
                var llmDeduped = Deduplicate(llmExtracted, target);
                MergeEntities(target, llmDeduped);
            }
        }
        catch
        {
            // swallow everything — stability > extraction
        }

        return target;
    }

    private ExtractedIntelligence ExtractWithRegex(string message)
    {
        var result = new ExtractedIntelligence();

        foreach (Match m in UpiRegex.Matches(message))
        {
            result.UpiIds.Add(m.Value);
        }

        foreach (Match m in PhoneRegex.Matches(message))
        {
            result.PhoneNumbers.Add(m.Value);
        }

        foreach (Match m in UrlRegex.Matches(message))
        {
            result.Urls.Add(m.Value);
        }

        var ifscs = IfscRegex.Matches(message)
            .Select(x => x.Value)
            .ToArray();

        var accounts = AccountNumberRegex.Matches(message)
            .Select(x => x.Value)
            .ToArray();

        if (accounts.Length > 0)
        {
            var ifsc = ifscs.FirstOrDefault();
            foreach (var acc in accounts)
            {
                result.BankAccounts.Add(new BankAccount
                {
                    AccountNumber = acc,
                    Ifsc = ifsc
                });
            }
        }

        return result;
    }

    private async Task<ExtractedIntelligence> ExtractWithLLM(string message, CancellationToken ct)
    {
        var result = new ExtractedIntelligence();

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
                    if (!string.IsNullOrWhiteSpace(s))
                        result.UpiIds.Add(s);
                }
            }

            if (root.TryGetProperty("phone_numbers", out var phoneArr))
            {
                foreach (var e in phoneArr.EnumerateArray())
                {
                    var s = e.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        result.PhoneNumbers.Add(s);
                }
            }

            if (root.TryGetProperty("urls", out var urlArr))
            {
                foreach (var e in urlArr.EnumerateArray())
                {
                    var s = e.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                        result.Urls.Add(s);
                }
            }

            if (root.TryGetProperty("bank_accounts", out var bankArr))
            {
                foreach (var e in bankArr.EnumerateArray())
                {
                    var acc = e.GetProperty("account_number").GetString();
                    var ifsc = e.TryGetProperty("ifsc", out var i) ? i.GetString() : null;

                    if (!string.IsNullOrWhiteSpace(acc))
                    {
                        result.BankAccounts.Add(new BankAccount
                        {
                            AccountNumber = acc,
                            Ifsc = ifsc
                        });
                    }
                }
            }
        }

        return result;
    }

    private void NormalizeEntities(ExtractedIntelligence entities)
    {
        for (int i = 0; i < entities.UpiIds.Count; i++)
        {
            entities.UpiIds[i] = entities.UpiIds[i].Trim();
        }

        for (int i = 0; i < entities.PhoneNumbers.Count; i++)
        {
            entities.PhoneNumbers[i] = Regex.Replace(entities.PhoneNumbers[i], @"[\s-]", "");
        }

        for (int i = 0; i < entities.Urls.Count; i++)
        {
            entities.Urls[i] = entities.Urls[i].Trim();
        }
    }

    private ExtractedIntelligence Deduplicate(ExtractedIntelligence source, ExtractedIntelligence existing)
    {
        var deduped = new ExtractedIntelligence();

        foreach (var upi in source.UpiIds)
        {
            if (!existing.UpiIds.Contains(upi) && !deduped.UpiIds.Contains(upi))
                deduped.UpiIds.Add(upi);
        }

        foreach (var phone in source.PhoneNumbers)
        {
            if (!existing.PhoneNumbers.Contains(phone) && !deduped.PhoneNumbers.Contains(phone))
                deduped.PhoneNumbers.Add(phone);
        }

        foreach (var url in source.Urls)
        {
            if (!existing.Urls.Contains(url) && !deduped.Urls.Contains(url))
                deduped.Urls.Add(url);
        }

        foreach (var acc in source.BankAccounts)
        {
            if (!existing.BankAccounts.Any(b => b.AccountNumber == acc.AccountNumber) &&
                !deduped.BankAccounts.Any(b => b.AccountNumber == acc.AccountNumber))
            {
                // distinct logic for IFSC fallback from original code
                deduped.BankAccounts.Add(acc);
            }
        }

        return deduped;
    }

    private void MergeEntities(ExtractedIntelligence target, ExtractedIntelligence source)
    {
        target.UpiIds.AddRange(source.UpiIds);
        target.PhoneNumbers.AddRange(source.PhoneNumbers);
        target.Urls.AddRange(source.Urls);
        target.BankAccounts.AddRange(source.BankAccounts);
    }
}