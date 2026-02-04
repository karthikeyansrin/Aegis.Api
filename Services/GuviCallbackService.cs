using System.Net.Http.Json;
using Aegis.Api.Models;

namespace Aegis.Api.Services;

public class GuviCallbackService
{
    private readonly HttpClient _http;

    public GuviCallbackService(HttpClient http)
    {
        _http = http;
    }

    public async Task SendFinalResultAsync(
        string sessionId,
        bool scamDetected,
        int totalMessagesExchanged,
        ExtractedIntelligence intelligence,
        string agentNotes,
        CancellationToken ct = default)
    {
        var payload = new
        {
            sessionId = sessionId,
            scamDetected = scamDetected,
            totalMessagesExchanged = totalMessagesExchanged,
            extractedIntelligence = new
            {
                bankAccounts = intelligence.BankAccounts.Select(b => b.AccountNumber),
                upiIds = intelligence.UpiIds,
                phishingLinks = intelligence.Urls,
                phoneNumbers = intelligence.PhoneNumbers,
                suspiciousKeywords = new[] { "urgent", "verify", "account blocked" }
            },
            agentNotes = agentNotes
        };

        await _http.PostAsJsonAsync(
            "https://hackathon.guvi.in/api/updateHoneyPotFinalResult",
            payload,
            ct
        );
    }
}
