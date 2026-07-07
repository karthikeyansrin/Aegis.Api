using System;
using System.Collections.Generic;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

/// <summary>
/// Result of enriching a ThreatAssessment with global intelligence data.
/// Wraps the original assessment and adds cross-session indicator context.
/// </summary>
public class EnrichedThreatAssessment
{
    // ── Original assessment ───────────────────────────────────────────────────

    public ThreatAssessment Original { get; init; } = null!;

    // ── Intelligence enrichment ───────────────────────────────────────────────

    /// <summary>
    /// Known global records matched against the current session's extracted indicators.
    /// Empty when no previously-seen indicators were found.
    /// </summary>
    public IReadOnlyList<GlobalIndicatorRecord> MatchedRecords { get; init; }
        = Array.Empty<GlobalIndicatorRecord>();

    /// <summary>
    /// Additional risk boost from known-bad indicators (0.0–0.5 additive cap).
    /// Applied on top of Original.RiskScore by ConversationEngine.
    /// </summary>
    public double IntelligenceRiskBoost { get; init; }

    /// <summary>Whether any matched record is manually flagged as high-risk.</summary>
    public bool   HasFlaggedIndicators  { get; init; }

    // ── Derived ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Effective risk score after applying the intelligence boost.
    /// Clamped to [0.0, 1.0].
    /// </summary>
    public double EffectiveRiskScore
        => Math.Clamp(Original.RiskScore + IntelligenceRiskBoost, 0.0, 1.0);

    /// <summary>
    /// Effective threat level re-computed from EffectiveRiskScore using the canonical bands.
    /// </summary>
    public ThreatLevel EffectiveLevel
        => ThreatAssessment.RiskScoreToLevel(EffectiveRiskScore);

    /// <summary>True when the effective threat is at Medium or above.</summary>
    public bool IsThreat => EffectiveLevel >= ThreatLevel.Medium;
}
