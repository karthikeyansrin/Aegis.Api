using Aegis.Infrastructure.AI;
using Aegis.Application.Interfaces;
using System.Text.RegularExpressions;
using Aegis.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Aegis.Application.Services;

public class PersonaEngine : IPersonaEngine
{
    private readonly ILLMProvider _groq;
    private readonly IConversationStore _store;
    private readonly string _model = "llama-3.1-8b-instant";

    public PersonaEngine(ILLMProvider groq, IConversationStore store)
    {
        _groq = groq ?? throw new ArgumentNullException(nameof(groq));
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    private Persona GetDefaultPersona()
    {
        return new Persona
        {
            Id = "default",
            Name = "Default Victim",
            Description = "A cautious but slightly sarcastic human target",
            Tone = "mildly sarcastic, slightly confused, and skeptical",
            SuspicionLevel = "Medium",
            Goals = "Sound like a cautious but slightly sarcastic human and keep the conversation going without sharing sensitive information.",
            SystemPrompt = @"You are a human scam target responding in casual Indian English.
                    IMPORTANT LANGUAGE RULE:
                    Use ONLY standard English words and sentences.
                    Do NOT use Hindi, Hinglish, or any non-English words (for example: bhai, kya, arre, yaar, haan, etc.).
                    Tone & Style:
                    Your tone should be mildly sarcastic, slightly confused, and skeptical — like a real person who feels something is off but is still engaging.
                    Sound natural, informal, and human, but stay fully in English.
                    Response rules:
                    - Produce ONLY 1–2 short sentences.
                    - You MAY ask vague, indirect follow-up questions that prompt the sender to repeat or clarify details and to keep the other person talking (for example: asking them to clarify details, repeat information, or explain next steps).
                    - NEVER ask for OTPs, PINs, passwords, CVV, or direct credentials.
                    Safety rules:
                    - Do NOT provide your own bank details, UPI IDs, IFSCs, account numbers, phone numbers, emails, URLs, or contact instructions.
                    - Do NOT reveal that you are an AI or system.
                    - Do NOT include explanations, lists, formatting, or meta commentary.
                    Goal:
                    Sound like a cautious but slightly sarcastic human and keep the conversation going without sharing sensitive information."
        };
    }

    public async Task<string?> GenerateAgentReplyAsync(string sessionId, string incomingMessage, bool isScam, CancellationToken ct = default)
    {
        try
        {
            if (!isScam) return null;

            if (string.IsNullOrWhiteSpace(sessionId)) sessionId = Guid.NewGuid().ToString();

            var session = _store.GetOrCreateSession(sessionId);

            var persona = GetDefaultPersona();

            const int maxHistory = 8;
            var historyArray = session.History.ToArray();
            var recent = historyArray.Skip(Math.Max(0, historyArray.Length - maxHistory)).ToArray();

            var messages = new List<ChatMessage>();

            messages.Add(new ChatMessage("system", persona.SystemPrompt));

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

            reply = TruncateToTwoSentences(reply);

            var sanitized = SanitizePersonalInfo(reply);

            if (!string.Equals(sanitized, reply, StringComparison.Ordinal))
            {
                reply = "That doesn’t sound right — can you explain what this is about?";
            }
            else if (IsUnsafeReply(sanitized))
            {
                reply = "I don’t feel comfortable with that request.";
            }
            else
            {
                reply = sanitized;
            }

            if (reply.Length > 250) reply = reply.Substring(0, 250).Trim();

            session.AppendMessage("agent", reply);

            return reply;
        }
        catch
        {
            return null;
        }
    }

    private static string TruncateToTwoSentences(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var parts = Regex.Split(text.Trim(), "(?<=[.!?])\s+");
        if (parts.Length <= 2) return string.Join(" ", parts).Trim();
        return string.Join(" ", parts.Take(2)).Trim();
    }

    private static string SanitizePersonalInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        text = Regex.Replace(text, "\b[\w.%+-]+@[\w.-]+\.[A-Za-z]{2,6}\b", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, "https?://\S+|www\.\S+", "[redacted]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        text = Regex.Replace(text, "\b[a-zA-Z]{4}0[a-zA-Z0-9]{6}\b", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, "\b[\w.%-]{2,}@[A-Za-z]{2,}\b", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, "(?<!\d)(?:\+?91[\s-]?)?(?:\d[\s-]?){10,14}(?!\d)", "[redacted]", RegexOptions.Compiled);
        text = Regex.Replace(text, "(?<!\d)\d{6,}(?!\d)", "[redacted]", RegexOptions.Compiled);

        return text;
    }

    private static bool IsUnsafeReply(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        var lower = text.ToLowerInvariant();

        if (Regex.IsMatch(lower, "\b(otp|pin|password|passcode|one-time|one time)\b")) return true;
        if (Regex.IsMatch(lower, "\b(i am a bot|i am an ai|as an ai|as a bot)\b")) return true;
        if (Regex.IsMatch(lower, "(phone|call me|contact me|email|whatsapp|telegram|upi|bank account|ifsc|account number)")) return true;

        return false;
    }
}