using System;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

/// <summary>
/// Global record of a known ThreatIndicator value, tracking how many times
/// it has been observed across all sessions and when it was first/last seen.
/// Stored in the ThreatIntelligenceEngine's global registry.
/// </summary>
public class GlobalIndicatorRecord
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public Guid          Id            { get; init; } = Guid.NewGuid();
    public IndicatorType Type          { get; init; }

    /// <summary>Normalized value, e.g. "9876543210", "pay@bank", "http://scam.com".</summary>
    public string        Value         { get; init; } = string.Empty;

    // ── Occurrence tracking ───────────────────────────────────────────────────

    public int           OccurrenceCount { get; set; } = 1;
    public DateTime      FirstSeenUtc    { get; init; } = DateTime.UtcNow;
    public DateTime      LastSeenUtc     { get; set; }  = DateTime.UtcNow;

    // ── Risk enrichment ───────────────────────────────────────────────────────

    /// <summary>
    /// Computed risk score (0.0–1.0) derived from occurrence count and type weight.
    /// Increases with each sighting up to a ceiling of 1.0.
    /// </summary>
    public double        RiskContribution { get; set; } = 0.0;

    /// <summary>Whether this indicator has been manually flagged as high-risk.</summary>
    public bool          IsFlagged        { get; set; }

    // ── Lookup key ────────────────────────────────────────────────────────────

    /// <summary>Stable lookup key: "{Type}:{Value}" (case-insensitive).</summary>
    public static string BuildKey(IndicatorType type, string value)
        => $"{type}:{value.Trim()}".ToLowerInvariant();

    public string Key => BuildKey(Type, Value);
}
