using Aegis.Infrastructure.AI;
using Aegis.Application.Interfaces;
using System.Text.RegularExpressions;
using Aegis.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using Aegis.Domain.Enums;

namespace Aegis.Application.Services;

public class PersonaEngine : IPersonaEngine
{
    private readonly ILLMProvider _groq;
    private readonly IConversationRepository _store;
    private readonly string _model = "llama-3.1-8b-instant";

    public PersonaEngine(ILLMProvider groq, IConversationRepository store)
    {
        _groq = groq ?? throw new ArgumentNullException(nameof(groq));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public async Task<string?> GenerateAgentReplyAsync(string sessionId, string incomingMessage, bool isScam, Persona persona, ConversationStage currentStage, CancellationToken ct = default)
    {
        if (persona is null) throw new ArgumentNullException(nameof(persona));

        try
        {
            if (!isScam) return null;

            if (string.IsNullOrWhiteSpace(sessionId)) sessionId = Guid.NewGuid().ToString();

            var session = await _store.GetOrCreateSessionAsync(sessionId, ct);

            var maxHistory = persona.HistoryWindow > 0 ? persona.HistoryWindow : 8;
            var historyArray = session.History.ToArray();
            var recent = historyArray.Skip(Math.Max(0, historyArray.Length - maxHistory)).ToArray();

            var messages = new List<ChatMessage>();

            var fullSystemPrompt = persona.SystemPrompt + "\n\n" + GetStageInstruction(currentStage);

            messages.Add(new ChatMessage("system", fullSystemPrompt));

            foreach (var m in recent)
            {
                var role = m.Role switch
                {
                    "agent" => "assistant",
                    "user" => "user",
                    _ => "user"
                };

                var content = (m.Content ?? string.Empty);
                if (content.Length > 800) content = content.Substring(content.Length - 800);

                messages.Add(new ChatMessage(role, content));
            }

            messages.Add(new ChatMessage("user", incomingMessage));

            var result = await _groq.CreateChatCompletionAsync(_model, messages, ct);

            var reply = result?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(reply))
            {
                reply = "Sorry, I'm a bit confused — could you clarify why you need this?";
            }

            var maxSentences = persona.MaxSentences > 0 ? persona.MaxSentences : 2;
            reply = TruncateToSentences(reply, maxSentences);

            var sanitized = SanitizePersonalInfo(reply);

            if (!string.Equals(sanitized, reply, StringComparison.Ordinal))
            {
                reply = "That doesn't sound right — can you explain what this is about?";
            }
            else if (IsUnsafeReply(sanitized))
            {
                reply = "I don't feel comfortable with that request.";
            }
            else
            {
                reply = sanitized;
            }

            var maxLength = persona.MaxReplyLength > 0 ? persona.MaxReplyLength : 250;
            if (reply.Length > maxLength) reply = reply.Substring(0, maxLength).Trim();

            session.AppendMessage("agent", reply);

            return reply;
        }
        catch
        {
            return null;
        }
    }

    private static string GetStageInstruction(ConversationStage stage)
    {
        return stage switch
        {
            ConversationStage.Clarify => "STAGE OBJECTIVE (Clarify): Ask vague, indirect follow-up questions to prompt the sender to repeat or clarify details.",
            ConversationStage.Delay => "STAGE OBJECTIVE (Delay): Give excuses for why you cannot complete the action right now, stalling for time (e.g. poor internet, busy, card not with you).",
            ConversationStage.Extract => "STAGE OBJECTIVE (Extract): Act as if you are trying to comply, but ask them for exactly which account/UPI/link you should send to or use.",
            ConversationStage.Confuse => "STAGE OBJECTIVE (Confuse): Give contradictory information or intentionally misunderstand their instructions to frustrate them.",
            ConversationStage.Terminate => "STAGE OBJECTIVE (Terminate): Firmly but politely end the conversation without complying.",
            _ => "STAGE OBJECTIVE (Clarify): Ask vague, indirect follow-up questions to prompt the sender to repeat or clarify details."
        };
    }

    private static string TruncateToSentences(string text, int maxSentences)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var parts = Regex.Split(text.Trim(), @"(?<=[.!?])\s+");
        if (parts.Length <= maxSentences) return string.Join(" ", parts).Trim();
        return string.Join(" ", parts.Take(maxSentences)).Trim();
    }

    private static string SanitizePersonalInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        text = Regex.Replace(text, @"\b[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,6}\b", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, @"https?://\S+|www\.\S+", "[redacted]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b[a-zA-Z]{4}0[a-zA-Z0-9]{6}\b", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, @"\b[\w.%-]{2,}@[A-Za-z]{2,}\b", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, @"(?<!\d)(?:\+?91[\s-]?)?(?:\d[\s-]?){10,14}(?!\d)", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, @"(?<!\d)\d{6,}(?!\d)", "[redacted]", RegexOptions.Compiled);

        return text;
    }

    private static bool IsUnsafeReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var lower = text.ToLowerInvariant();

        if (Regex.IsMatch(lower, @"\b(otp|pin|password|passcode|one-time|one time)\b")) return true;
        if (Regex.IsMatch(lower, @"\b(i am a bot|i am an ai|as an ai|as a bot)\b")) return true;
        if (Regex.IsMatch(lower, @"(phone|call me|contact me|email|whatsapp|telegram|upi|bank account|ifsc|account number)")) return true;

        return false;
    }
}