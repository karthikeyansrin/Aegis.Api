using System;
using System.Collections.Generic;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

/// <summary>
/// Rich domain model representing the complete threat evaluation result.
/// Replaces the flat IsScam/ScamType/Confidence pattern.
/// </summary>
public class ThreatAssessment
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public double RiskScore { get; init; }
    public double Confidence { get; init; }
    public ThreatLevel Level { get; init; }
    public ThreatCategory Category { get; init; }
    public List<ThreatIndicator> Indicators { get; init; } = new();
    public List<string> ReasonCodes { get; init; } = new();
    public bool CanAutoEngage { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    // ── Derived convenience properties ──────────────────────────────────────

    /// <summary>True when the threat level is Medium or above.</summary>
    public bool IsThreat => Level >= ThreatLevel.Medium;

    /// <summary>
    /// Human-readable category string for v1 API responses.
    /// Returns null for ThreatCategory.None.
    /// </summary>
    public string? ScamCategory => Category == ThreatCategory.None
        ? null
        : FormatCategory(Category);

    // ── Static helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Maps a RiskScore (0.0–1.0) to a ThreatLevel using fixed percentage bands:
    ///   0 –25 %  → Low
    ///  26 –50 %  → Medium
    ///  51 –75 %  → High
    ///  76 –100 % → Critical
    /// </summary>
    public static ThreatLevel RiskScoreToLevel(double riskScore)
    {
        // Normalise to 0-100 range; clamp to avoid floating-point edge cases
        var pct = Math.Clamp(riskScore * 100.0, 0.0, 100.0);
        return pct switch
        {
            <= 25.0  => ThreatLevel.Low,
            <= 50.0  => ThreatLevel.Medium,
            <= 75.0  => ThreatLevel.High,
            _        => ThreatLevel.Critical
        };
    }

    /// <summary>Converts a ThreatLevel to its display-name string for v1 API responses.</summary>
    public static string LevelToString(ThreatLevel level) => level switch
    {
        ThreatLevel.None     => "none",
        ThreatLevel.Low      => "low",
        ThreatLevel.Medium   => "medium",
        ThreatLevel.High     => "high",
        ThreatLevel.Critical => "critical",
        _                    => "unknown"
    };

    /// <summary>Formats a ThreatCategory as a snake_case string suitable for API responses.</summary>
    public static string? FormatCategory(ThreatCategory category) => category switch
    {
        ThreatCategory.None             => null,
        ThreatCategory.PhishingAttempt  => "phishing_attempt",
        ThreatCategory.FinancialFraud   => "financial_fraud",
        ThreatCategory.IdentityTheft    => "identity_theft",
        ThreatCategory.SocialEngineering=> "social_engineering",
        ThreatCategory.MalwareDelivery  => "malware_delivery",
        ThreatCategory.PrizeSweepstakes => "prize_sweepstakes",
        ThreatCategory.InvestmentFraud  => "investment_fraud",
        ThreatCategory.RomanceScam      => "romance_scam",
        ThreatCategory.TechSupportFraud => "tech_support_fraud",
        ThreatCategory.Unknown          => "unknown",
        _                               => category.ToString().ToLowerInvariant()
    };
}