using System.Text.RegularExpressions;
using Aegis.Api.Models;

namespace Aegis.Api.Services;

/// <summary>
/// Generates short agent replies for scam messages using the Groq service and conversation history.
/// Respects safety rules (no personal info), tone requirements, and appends agent replies to the conversation.
/// </summary>
public class HoneypotAgentService
{
    private readonly IGroqService _groq;
    private readonly ConversationStore _store;
    private readonly string _model = "llama-3.1-8b-instant";

    public HoneypotAgentService(IGroqService groq, ConversationStore store)
    {
        _groq = groq ?? throw new ArgumentNullException(nameof(groq));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Generate an agent reply only when message is identified as scam. Returns null when no reply should be generated.
    /// This method never throws; failures result in a null return.
    /// </summary>
    public async Task<string?> GenerateAgentReplyAsync(string sessionId, string incomingMessage, bool isScam, CancellationToken ct = default)
    {
        try
        {
            if (!isScam) return null;

            if (string.IsNullOrWhiteSpace(sessionId)) sessionId = Guid.NewGuid().ToString();

            var session = _store.GetOrCreateSession(sessionId);

            // Append the incoming user message to history
            session.AppendMessage("user", incomingMessage);

            // Build context: last N messages (user/agent) to keep prompt short
            const int maxHistory = 8;
            var historyArray = session.History.ToArray();
            var recent = historyArray.Skip(Math.Max(0, historyArray.Length - maxHistory)).ToArray();

            var messages = new List<ChatMessage>();

            // System prompt: strict rules for tone and safety
            messages.Add(new ChatMessage("system",
                "You are a human-sounding assistant responding in a polite, slightly confused tone appropriate for Indian English. " +
                "Produce ONLY 1-2 short sentences (concise and conversational). Never ask for OTPs, PINs, passwords, or other sensitive personal data. " +
                "Never provide bank details, UPI ids, IFSCs, account numbers, phone numbers, emails, URLs, or any contact instructions. " +
                "Do NOT reveal that you are an AI or the system identity. If the request is suspicious, politely express confusion or refusal without asking for sensitive data. " +
                "Do not include extra analysis, markup, or lists."
            ));

            // include recent history entries to provide context
            foreach (var m in recent)
            {
                var role = m.Role switch
                {
                    "agent" => "assistant",
                    "user" => "user",
                    _ => "user"
                };

                // Trim long content for prompt size
                var content = (m.Content ?? string.Empty);
                if (content.Length > 800) content = content.Substring(content.Length - 800);

                messages.Add(new ChatMessage(role, content));
            }

            // Finally, include the latest user message explicitly
            messages.Add(new ChatMessage("user", incomingMessage));

            var result = await _groq.CreateChatCompletionAsync(_model, messages, ct);

            var reply = result?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(reply))
            {
                // Nothing generated from LLM — fall back to a safe generic reply
                reply = "Sorry, I'm a bit confused — could you clarify why you need this?";
            }

            // Keep only 1-2 sentences
            reply = TruncateToTwoSentences(reply);

            // Sanitize any accidental personal info
            reply = SanitizePersonalInfo(reply);

            // Post-check: ensure reply does not request sensitive info or reveal identity
            if (IsUnsafeReply(reply))
            {
                reply = "Sorry, I don't feel comfortable with that request.";
            }

            // Ensure reply is short (limit characters as a safeguard)
            if (reply.Length > 250) reply = reply.Substring(0, 250).Trim();

            // Append agent reply to session history
            session.AppendMessage("agent", reply);

            return reply;
        }
        catch
        {
            // Never throw from this service
            return null;
        }
    }

    private static string TruncateToTwoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var parts = Regex.Split(text.Trim(), "(?<=[.!?])\\s+");
        if (parts.Length <= 2) return string.Join(" ", parts).Trim();
        return string.Join(" ", parts.Take(2)).Trim();
    }

    private static string SanitizePersonalInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // email
        text = Regex.Replace(text, @"\b[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,6}\b", "[redacted]", RegexOptions.Compiled);

        // URLs
        text = Regex.Replace(text, @"https?://\S+|www\.\S+", "[redacted]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // IFSC (e.g., ABCD0EFGHIJ)
        text = Regex.Replace(text, "\b[a-zA-Z]{4}0[a-zA-Z0-9]{6}\b", "[redacted]", RegexOptions.Compiled);

        // UPI ids like name@bank
        text = Regex.Replace(text, "\b[\w.%-]{2,}@[A-Za-z]{2,}\b", "[redacted]", RegexOptions.Compiled);

        // Phone numbers (10-14 digits, with optional separators)
        text = Regex.Replace(text, @"(?<!\d)(?:\+?91[\s-]?)?(?:\d[\s-]?){10,14}(?!\d)", "[redacted]", RegexOptions.Compiled);

        // Long digit sequences (account numbers) 6+ digits
        text = Regex.Replace(text, "(?<!\d)\d{6,}(?!\d)", "[redacted]", RegexOptions.Compiled);

        return text;
    }

    private static bool IsUnsafeReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Lower for checks
        var lower = text.ToLowerInvariant();

        // Reject if asking for OTP/PIN/password or other sensitive data
        if (Regex.IsMatch(lower, "\b(otp|pin|password|passcode|one-time|one time)\b")) return true;

        // Reject if revealing system identity
        if (Regex.IsMatch(lower, "\b(i am a bot|i am an ai|as an ai|as a bot)\b")) return true;

        // Reject if asking for contact details or providing them
        if (Regex.IsMatch(lower, "(phone|call me|contact me|email|whatsapp|telegram|upi|bank account|ifsc|account number)")) return true;

        return false;
    }
}
