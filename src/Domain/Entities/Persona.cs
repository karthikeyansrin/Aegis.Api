using System;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

/// <summary>
/// A persona defines the character, tone, and behavioural rules
/// that the PersonaEngine uses when generating agent replies.
/// Future versions will be persisted in the database and selected
/// dynamically based on ThreatAssessment or session context.
/// </summary>
public class Persona
{
    // ── Identity ─────────────────────────────────────────────────────────────

    public Guid   Id      { get; init; } = Guid.NewGuid();
    public string Name    { get; init; } = string.Empty;
    public bool   Enabled { get; init; } = true;

    // ── Character definition ──────────────────────────────────────────────────

    /// <summary>Short human-readable description of this persona's character.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Emotional and linguistic tone. Injected into the system prompt context.
    /// e.g. "mildly sarcastic, slightly confused, skeptical"
    /// </summary>
    public string Tone { get; init; } = string.Empty;

    /// <summary>
    /// Describes the conversational style used in replies.
    /// e.g. "casual", "formal", "curious", "evasive"
    /// </summary>
    public string ConversationStyle { get; init; } = string.Empty;

    /// <summary>
    /// How suspicious this persona behaves toward incoming messages.
    /// e.g. "Low", "Medium", "High"
    /// </summary>
    public string SuspicionLevel { get; init; } = string.Empty;

    /// <summary>
    /// The goals of this persona during engagement.
    /// Used to guide the LLM toward intended behaviour.
    /// </summary>
    public string Goals { get; init; } = string.Empty;

    // ── LLM system prompt ─────────────────────────────────────────────────────

    /// <summary>
    /// Full system prompt injected as the first chat message.
    /// This is the primary behavioural contract handed to the LLM.
    /// </summary>
    public string SystemPrompt { get; init; } = string.Empty;

    // ── Persona constraints ───────────────────────────────────────────────────

    /// <summary>Maximum characters in a generated reply.</summary>
    public int    MaxReplyLength  { get; init; } = 250;

    /// <summary>Maximum sentences in a generated reply.</summary>
    public int    MaxSentences    { get; init; } = 2;

    /// <summary>Number of recent history messages to include as context.</summary>
    public int    HistoryWindow   { get; init; } = 8;
}