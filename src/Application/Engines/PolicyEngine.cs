using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.Interfaces;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Application.Engines;

/// <summary>
/// Evaluates the active policy set (sourced from IPolicyRepository) against a
/// ThreatAssessment and returns the first matching policy's DecisionResult.
///
/// Evaluation order: policies sorted by Priority ascending (lowest number first).
/// ALL conditions in a policy must pass (AND semantics) for it to match.
/// The first matching policy wins — no fall-through.
///
/// Falls back to DecisionEngine (hardcoded defaults) if no policy matches.
/// </summary>
public class PolicyEngine : IPolicyEngine
{
    private readonly IPolicyRepository _repository;
    private readonly IDecisionEngine   _fallback;

    public PolicyEngine(IPolicyRepository repository, IDecisionEngine fallback)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _fallback   = fallback   ?? throw new ArgumentNullException(nameof(fallback));
    }

    public async Task<DecisionResult> EvaluateAsync(ThreatAssessment assessment, CancellationToken ct = default)
    {
        if (assessment is null) throw new ArgumentNullException(nameof(assessment));

        IReadOnlyList<Policy> policies = await _repository.GetEnabledPoliciesAsync(ct);

        foreach (var policy in policies.OrderBy(p => p.Priority))
        {
            if (!policy.Enabled) continue;
            if (!AllConditionsMet(policy.Conditions, assessment)) continue;

            // First match wins
            var codes = new List<string>(assessment.ReasonCodes);
            codes.AddRange(policy.ReasonCodes);
            codes.Add($"MATCHED_POLICY:{policy.Name}");

            return new DecisionResult
            {
                Decision          = policy.Decision,
                RecommendedAction = policy.RecommendedAction,
                CanAutoEngage     = policy.CanAutoEngage,
                ReasonCodes       = codes
            };
        }

        // No policy matched — defer to DecisionEngine hardcoded defaults
        return _fallback.Evaluate(assessment);
    }

    // ── Condition evaluator ──────────────────────────────────────────────────

    private static bool AllConditionsMet(IEnumerable<PolicyCondition> conditions, ThreatAssessment assessment)
    {
        foreach (var condition in conditions)
        {
            if (!Evaluate(condition, assessment))
                return false;
        }
        return true; // empty list = always matches
    }

    private static bool Evaluate(PolicyCondition condition, ThreatAssessment assessment)
    {
        try
        {
            return condition.Field switch
            {
                ConditionField.RiskScore      => CompareDouble(assessment.RiskScore,  condition.Operator, condition.Value),
                ConditionField.Confidence     => CompareDouble(assessment.Confidence, condition.Operator, condition.Value),
                ConditionField.ThreatLevel    => CompareEnum<ThreatLevel>(assessment.Level,    condition.Operator, condition.Value),
                ConditionField.ThreatCategory => CompareEnum<ThreatCategory>(assessment.Category, condition.Operator, condition.Value),
                ConditionField.CanAutoEngage  => CompareBool(assessment.CanAutoEngage, condition.Operator, condition.Value),
                _                             => false
            };
        }
        catch
        {
            // Malformed condition value — treat as not matching (fail-safe)
            return false;
        }
    }

    private static bool CompareDouble(double actual, ConditionOperator op, string raw)
    {
        if (!double.TryParse(raw, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var threshold))
            return false;

        return op switch
        {
            ConditionOperator.Equals             => Math.Abs(actual - threshold) < 1e-10,
            ConditionOperator.GreaterThan        => actual >  threshold,
            ConditionOperator.GreaterThanOrEqual => actual >= threshold,
            ConditionOperator.LessThan           => actual <  threshold,
            ConditionOperator.LessThanOrEqual    => actual <= threshold,
            _                                    => false
        };
    }

    private static bool CompareEnum<TEnum>(TEnum actual, ConditionOperator op, string raw)
        where TEnum : struct, Enum
    {
        if (!Enum.TryParse<TEnum>(raw, ignoreCase: true, out var expected))
            return false;

        var cmp = Comparer<TEnum>.Default.Compare(actual, expected);
        return op switch
        {
            ConditionOperator.Equals             => cmp == 0,
            ConditionOperator.GreaterThan        => cmp >  0,
            ConditionOperator.GreaterThanOrEqual => cmp >= 0,
            ConditionOperator.LessThan           => cmp <  0,
            ConditionOperator.LessThanOrEqual    => cmp <= 0,
            ConditionOperator.Contains           => actual.ToString()
                                                         .Contains(raw, StringComparison.OrdinalIgnoreCase),
            _                                    => false
        };
    }

    private static bool CompareBool(bool actual, ConditionOperator op, string raw)
    {
        if (!bool.TryParse(raw, out var expected)) return false;
        return op == ConditionOperator.Equals && actual == expected;
    }
}
