using System;
using System.Collections.Generic;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

/// <summary>
/// A single evaluatable condition within a Policy.
/// e.g. Field=RiskScore, Operator=GreaterThanOrEqual, Value="0.76"
/// </summary>
public class PolicyCondition
{
    public ConditionField    Field    { get; init; }
    public ConditionOperator Operator { get; init; }

    /// <summary>
    /// String-serialized value — compared against the ThreatAssessment field at runtime.
    /// Use numeric strings for scores, enum names for ThreatLevel / ThreatCategory.
    /// </summary>
    public string Value { get; init; } = string.Empty;
}

/// <summary>
/// A named, prioritized, enable-able rule that maps a set of Conditions to a Decision.
/// Higher Priority wins when multiple policies match (lower number = higher priority).
/// </summary>
public class Policy
{
    public Guid   Id       { get; init; } = Guid.NewGuid();
    public string Name     { get; init; } = string.Empty;
    public bool   Enabled  { get; init; } = true;
    public int    Priority { get; init; } = 100;   // lower = evaluated first

    /// <summary>
    /// ALL conditions must be satisfied for this policy to match (AND logic).
    /// An empty list always matches — use as a catch-all / default policy.
    /// </summary>
    public List<PolicyCondition> Conditions { get; init; } = new();

    // Output of this policy when it matches
    public Decision           Decision          { get; init; } = Decision.Allow;
    public RecommendedAction  RecommendedAction { get; init; } = RecommendedAction.PassThrough;
    public bool               CanAutoEngage     { get; init; }
    public List<string>       ReasonCodes       { get; init; } = new();
}
