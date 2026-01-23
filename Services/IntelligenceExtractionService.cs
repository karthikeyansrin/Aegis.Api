using System.Text.Json;
using System.Text.RegularExpressions;
using Aegis.Api.Models;

namespace Aegis.Api.Services;

public class IntelligenceExtractionService
{
    private readonly IGroqService _groq;
    private readonly ConversationStore _store;

    private static readonly Regex UpIRegex = new(@"\b[\w.\-]{2,}@[A-Za-z]{2,}\b", RegexOptions.Compiled);
    private static readonly Regex PhoneRegex = new(@"(?<!\d)(?:\+?91[\s-]?)?(?:\d[\s-]?){10,14}(?!\d)", RegexOptions.Compiled);
    private static readonly Regex UrlRegex = new(@"https?://\S+|www\.\S+|\b(?:[\w-]+\.)+[A-Za-z]{2,}\S*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex IfscRegex = new(@"\b[A-Za-z]{4}0[A-Za-z0-9]{6}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex AccountNumberRegex = new(@"(?<!\d)\d{9,18}(?!\d)", RegexOptions.Compiled);

    public IntelligenceExtractionService(IGroqService groq, ConversationStore store)
    {
        _groq = groq ?? throw new ArgumentNullException(nameof(groq));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Extract intelligence from a message. Merge results into the conversation session.
    /// If no regex matches are found and useLLMFallback is true, an LLM is queried for structured extraction.
    /// This method always returns an ExtractedIntelligence object (possibly empty) and swallows exceptions.
    /// </summary>
    public async Task<ExtractedIntelligence> ExtractAsync(string sessionId, string message, bool useLLMFallback = true, CancellationToken ct = default)
    {
        var intel = new ExtractedIntelligence
        {
            UpiIds = new List<string>(),
            PhoneNumbers = new List<string>(),
            Urls = new List<string>(),
            BankAccounts = new List<BankAccount>()
        };

        try
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                // UPI IDs
                foreach (Match m in UpIRegex.Matches(message))
                {
                    var v = m.Value.Trim();
                    if (!intel.UpiIds.Contains(v)) intel.UpiIds.Add(v);
                }

                // Phone numbers
                foreach (Match m in PhoneRegex.Matches(message))
                {
                    var v = Regex.Replace(m.Value, "[\s-]", string.Empty);
                    if (!intel.PhoneNumbers.Contains(v)) intel.PhoneNumbers.Add(v);
                }

                // URLs
                foreach (Match m in UrlRegex.Matches(message))
                {
                    var v = m.Value.Trim();
                    if (!intel.Urls.Contains(v)) intel.Urls.Add(v);
                }

                // IFSC codes
                var ifscs = IfscRegex.Matches(message).Select(x => x.Value.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                // Account numbers
                var accounts = AccountNumberRegex.Matches(message).Select(x => x.Value.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();

                // Pair account numbers with IFSCs if both present; otherwise store account numbers as bank accounts without IFSC
                if (accounts.Length > 0)
                {
                    if (ifscs.Length > 0)
                    {
                        // pair first IFSC with each account (best-effort)
                        var firstIfsc = ifscs[0];
                        foreach (var acc in accounts)
                        {
                            if (!intel.BankAccounts.Any(b => b.AccountNumber == acc))
                                intel.BankAccounts.Add(new BankAccount { AccountNumber = acc, Ifsc = firstIfsc });
                        }
                    }
                    else
                    {
                        foreach (var acc in accounts)
                        {
                            if (!intel.BankAccounts.Any(b => b.AccountNumber == acc))
                                intel.BankAccounts.Add(new BankAccount { AccountNumber = acc, Ifsc = null });
                        }
                    }
                }

                // If no regex matches and fallback allowed, ask LLM for structured extraction
                var anyFound = (intel.UpiIds.Count > 0) || (intel.PhoneNumbers.Count > 0) || (intel.Urls.Count > 0) || (intel.BankAccounts.Count > 0);

                if (!anyFound && useLLMFallback)
                {
                    var messages = new[]
                    {
                        new ChatMessage("system", "Extract any UPI ids, phone numbers, URLs, IFSC codes, and bank account numbers from the user's message and return ONLY a JSON object with arrays: upi_ids, phone_numbers, urls, bank_accounts (array of { account_number, ifsc }). Return empty arrays if none found."),
                        new ChatMessage("user", message)
                    };

                    var llm = await _groq.CreateChatCompletionAsync("llama-3.1-8b-instant", messages, ct);
                    if (llm?.Success == true && !string.IsNullOrWhiteSpace(llm.Content))
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(llm.Content);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("upi_ids", out var upiArr) && upiArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var e in upiArr.EnumerateArray())
                                {
                                    var s = e.GetString();
                                    if (!string.IsNullOrWhiteSpace(s) && !intel.UpiIds.Contains(s)) intel.UpiIds.Add(s);
                                }
                            }

                            if (root.TryGetProperty("phone_numbers", out var phoneArr) && phoneArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var e in phoneArr.EnumerateArray())
                                {
                                    var s = e.GetString();
                                    if (!string.IsNullOrWhiteSpace(s))
                                    {
                                        var cleaned = Regex.Replace(s, "[\\s-]", string.Empty);
                                        if (!intel.PhoneNumbers.Contains(cleaned)) intel.PhoneNumbers.Add(cleaned);
                                    }
                                }
                            }

                            if (root.TryGetProperty("urls", out var urlArr) && urlArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var e in urlArr.EnumerateArray())
                                {
                                    var s = e.GetString();
                                    if (!string.IsNullOrWhiteSpace(s) && !intel.Urls.Contains(s)) intel.Urls.Add(s);
                                }
                            }

                            if (root.TryGetProperty("bank_accounts", out var bankArr) && bankArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var e in bankArr.EnumerateArray())
                                {
                                    if (e.ValueKind != JsonValueKind.Object) continue;
                                    var acc = e.GetProperty("account_number").GetString();
                                    var ifsc = e.TryGetProperty("ifsc", out var ifscElem) ? ifscElem.GetString() : null;
                                    if (!string.IsNullOrWhiteSpace(acc) && !intel.BankAccounts.Any(b => b.AccountNumber == acc))
                                    {
                                        intel.BankAccounts.Add(new BankAccount { AccountNumber = acc, Ifsc = ifsc });
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignore parse errors from LLM
                        }
                    }
                }
            }

            // Merge into session state (best-effort) and return the aggregated intelligence
            try
            {
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    var session = _store.GetOrCreateSession(sessionId);
                    session.MergeExtractedIntelligence(intel);

                    // Return a copy of the aggregated intelligence so callers receive accumulated data
                    return CopyExtractedIntelligence(session.AggregatedIntelligence);
                }
            }
            catch
            {
                // swallow
            }
        }
        catch
        {
            // On any unexpected failure, return empty intelligence
            return new ExtractedIntelligence
            {
                UpiIds = new List<string>(),
                PhoneNumbers = new List<string>(),
                Urls = new List<string>(),
                BankAccounts = new List<BankAccount>()
            };
        }

        // If we couldn't merge into a session, return the per-request intelligence (possibly empty)
        return intel;
    }

    private static ExtractedIntelligence CopyExtractedIntelligence(ExtractedIntelligence? src)
    {
        if (src is null)
            return new ExtractedIntelligence
            {
                UpiIds = new List<string>(),
                PhoneNumbers = new List<string>(),
                Urls = new List<string>(),
                BankAccounts = new List<BankAccount>()
            };

        return new ExtractedIntelligence
        {
            UpiIds = src.UpiIds != null ? new List<string>(src.UpiIds) : new List<string>(),
            PhoneNumbers = src.PhoneNumbers != null ? new List<string>(src.PhoneNumbers) : new List<string>(),
            Urls = src.Urls != null ? new List<string>(src.Urls) : new List<string>(),
            BankAccounts = src.BankAccounts != null ? src.BankAccounts.Select(b => new BankAccount { AccountNumber = b.AccountNumber, Ifsc = b.Ifsc }).ToList() : new List<BankAccount>()
        };
    }
}
