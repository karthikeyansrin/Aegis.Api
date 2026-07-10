using System;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

/// <summary>
/// A single piece of extracted threat-relevant data (UPI ID, phone, URL, bank account…).
/// Replaces the raw string-bag ExtractedIntelligence model.
/// Each item carries its type, normalized value, source, and extraction confidence.
/// </summary>
public class ThreatIndicator
{
    // ── Identity ─────────────────────────────────────────────────────────────

    public Guid   Id          { get; init; } = Guid.NewGuid();
    public string SessionId   { get; init; } = string.Empty;

    // ── Classification ───────────────────────────────────────────────────────

    /// <summary>The semantic type of the extracted value.</summary>
    public IndicatorType Type { get; init; } = IndicatorType.Unknown;

    /// <summary>
    /// For ThreatAssessment use: a short machine-readable code.
    /// e.g. "UPI_ID", "PHONE_NUMBER", "URL", "BANK_ACCOUNT"
    /// </summary>
    public string Code { get; init; } = string.Empty;

    // ── Payload ──────────────────────────────────────────────────────────────

    /// <summary>The normalized, extracted value (e.g. a UPI ID string, a phone number).</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Human-readable description of what was found and where.</summary>
    public string Description { get; init; } = string.Empty;

    // ── Metadata ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Extraction confidence weight: 1.0 = regex-confirmed, 0.7 = LLM-extracted.
    /// </summary>
    public double Weight { get; init; } = 1.0;

    /// <summary>Who produced this indicator: "regex" or "llm".</summary>
    public string Source { get; init; } = "regex";

    /// <summary>Additional context — e.g. IFSC code paired with a bank account number.</summary>
    public string? Metadata { get; init; }
}