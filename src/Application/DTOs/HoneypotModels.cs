using System;
using System.Text.Json.Serialization;
using Aegis.Domain.Entities;

namespace Aegis.Application.DTOs
{
    public sealed class HoneypotRequest
    {
        [JsonPropertyName("session_id")]
        public required string SessionId { get; init; }

        [JsonPropertyName("message")]
        public required string Message { get; init; }

        [JsonPropertyName("language_hint")]
        public string? LanguageHint { get; init; }

        [JsonPropertyName("timestamp")]
        public DateTimeOffset? Timestamp { get; init; }

        public (bool IsValid, string? Error) Validate()
        {
            if (string.IsNullOrWhiteSpace(SessionId))
                return (false, "'session_id' is required");

            if (string.IsNullOrWhiteSpace(Message))
                return (false, "'message' is required and must not be empty");

            return (true, null);
        }
    }

    public sealed class HoneypotResponse
    {
        [JsonPropertyName("is_scam")]
        public required bool IsScam { get; init; }

        [JsonPropertyName("scam_type")]
        public string? ScamType { get; init; }

        [JsonPropertyName("confidence")]
        public double Confidence { get; init; } = 0.0;

        [JsonPropertyName("agent_reply")]
        public string AgentReply { get; init; } = string.Empty;

        [JsonPropertyName("extracted_intelligence")]
        public ExtractedIntelligence ExtractedIntelligence { get; set; } = null!;

        [JsonPropertyName("conversation_state")]
        public string? ConversationState { get; init; }

        [JsonPropertyName("safety_flags")]
        public SafetyFlags? SafetyFlags { get; init; }
    }

    public sealed class SafetyFlags
    {
        [JsonPropertyName("requested_money")]
        public bool RequestedMoney { get; init; }

        [JsonPropertyName("asked_for_otp")]
        public bool AskedForOtp { get; init; }
    }
}