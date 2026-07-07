using System;
using System.Collections.Generic;
using Aegis.Application.Interfaces;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using Aegis.Shared.Options;
using Microsoft.Extensions.Options;

namespace Aegis.Application.Engines;

/// <summary>
/// Evaluates a ThreatAssessment against a configurable policy table and
/// returns a DecisionResult that tells the system what action to take.
///
/// Default rules (overridable via appsettings DecisionEngine:Policies):
///   ThreatLevel.None     → Allow     / PassThrough       / CanAutoEngage = false
///   ThreatLevel.Low      → Allow     / PassThrough       / CanAutoEngage = false
///   ThreatLevel.Medium   → Warn      / FlagForReview     / CanAutoEngage = false
///   ThreatLevel.High     → Challenge / NotifyUser        / CanAutoEngage = false
///   ThreatLevel.Critical → AutoEngage/ EngagePersona    / CanAutoEngage = true
/// </summary>
public class DecisionEngine : IDecisionEngine
{
    // Hard-coded default rules — applied when no matching policy is found in options.
    private static readonly IReadOnlyDictionary<ThreatLevel, (Decision Decision, RecommendedAction Action, bool CanAutoEngage)>
        DefaultRules = new Dictionary<ThreatLevel, (Decision, RecommendedAction, bool)>
        {
            [ThreatLevel.None]     = (Decision.Allow,      RecommendedAction.PassThrough,       false),
            [ThreatLevel.Low]      = (Decision.Allow,      RecommendedAction.PassThrough,       false),
            [ThreatLevel.Medium]   = (Decision.Warn,       RecommendedAction.FlagForReview,     false),
            [ThreatLevel.High]     = (Decision.Challenge,  RecommendedAction.NotifyUser,        false),
            [ThreatLevel.Critical] = (Decision.AutoEngage, RecommendedAction.EngagePersona,     true),
        };

    private readonly DecisionEngineOptions _options;

    public DecisionEngine(IOptions<DecisionEngineOptions> options)
    {
        _options = options.Value;
    }

    public DecisionResult Evaluate(ThreatAssessment assessment)
    {
        if (assessment is null) throw new ArgumentNullException(nameof(assessment));

        var levelName = assessment.Level.ToString();

        // Try to resolve from configurable policy table first
        if (_options.Policies.TryGetValue(levelName, out var policy))
        {
            return BuildFromPolicy(policy, assessment.Level, assessment.ReasonCodes);
        }

        // Fall back to hard-coded defaults
        if (DefaultRules.TryGetValue(assessment.Level, out var rule))
        {
            return BuildFromDefaults(rule, assessment.Level, assessment.ReasonCodes);
        }

        // Ultimate safety net — treat anything unrecognised as Warn
        return new DecisionResult
        {
            Decision          = Decision.Warn,
            RecommendedAction = RecommendedAction.FlagForReview,
            CanAutoEngage     = false,
            ReasonCodes       = new List<string>(assessment.ReasonCodes) { "UNKNOWN_THREAT_LEVEL" }
        };
    }

    private static DecisionResult BuildFromPolicy(
        ThreatLevelPolicy policy,
        ThreatLevel level,
        List<string> threatReasonCodes)
    {
        var decision = Enum.TryParse<Decision>(policy.Decision, ignoreCase: true, out var d)
            ? d : Decision.Warn;

        var action = Enum.TryParse<RecommendedAction>(policy.Action, ignoreCase: true, out var a)
            ? a : RecommendedAction.FlagForReview;

        var codes = new List<string>(threatReasonCodes)
        {
            $"POLICY:{level.ToString().ToUpperInvariant()}",
            "SOURCE:CONFIG"
        };

        return new DecisionResult
        {
            Decision          = decision,
            RecommendedAction = action,
            CanAutoEngage     = policy.CanAutoEngage,
            ReasonCodes       = codes
        };
    }

    private static DecisionResult BuildFromDefaults(
        (Decision Decision, RecommendedAction Action, bool CanAutoEngage) rule,
        ThreatLevel level,
        List<string> threatReasonCodes)
    {
        var codes = new List<string>(threatReasonCodes)
        {
            $"POLICY:{level.ToString().ToUpperInvariant()}",
            "SOURCE:DEFAULT"
        };

        return new DecisionResult
        {
            Decision          = rule.Decision,
            RecommendedAction = rule.Action,
            CanAutoEngage     = rule.CanAutoEngage,
            ReasonCodes       = codes
        };
    }
}
