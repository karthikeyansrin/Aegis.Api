using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.Interfaces;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Infrastructure.Policies;

/// <summary>
/// In-memory IPolicyRepository seeded with the default production policy set.
/// Replace or supplement with an EF Core implementation backed by PostgreSQL
/// when dynamic policy management is needed.
/// </summary>
public sealed class InMemoryPolicyRepository : IPolicyRepository
{
    private static readonly IReadOnlyList<Policy> DefaultPolicies = BuildDefaults();

    public Task<IReadOnlyList<Policy>> GetEnabledPoliciesAsync(CancellationToken ct = default)
        => Task.FromResult(DefaultPolicies);

    private static IReadOnlyList<Policy> BuildDefaults()
    {
        return new List<Policy>
        {
            // ── Priority 10: CRITICAL RiskScore → AutoEngage ───────────────────
            new()
            {
                Name     = "Critical Threat - Auto Engage",
                Enabled  = true,
                Priority = 10,
                Conditions = new()
                {
                    new() { Field = ConditionField.RiskScore, Operator = ConditionOperator.GreaterThanOrEqual, Value = "0.76" }
                },
                Decision          = Decision.AutoEngage,
                RecommendedAction = RecommendedAction.EngagePersona,
                CanAutoEngage     = true,
                ReasonCodes       = new() { "RULE:CRITICAL_AUTO_ENGAGE" }
            },

            // ── Priority 20: HIGH RiskScore → Challenge ─────────────────────────
            new()
            {
                Name     = "High Threat - Challenge",
                Enabled  = true,
                Priority = 20,
                Conditions = new()
                {
                    new() { Field = ConditionField.RiskScore, Operator = ConditionOperator.GreaterThanOrEqual, Value = "0.51" }
                },
                Decision          = Decision.Challenge,
                RecommendedAction = RecommendedAction.NotifyUser,
                CanAutoEngage     = false,
                ReasonCodes       = new() { "RULE:HIGH_CHALLENGE" }
            },

            // ── Priority 30: MEDIUM RiskScore → Warn ───────────────────────────
            new()
            {
                Name     = "Medium Threat - Warn",
                Enabled  = true,
                Priority = 30,
                Conditions = new()
                {
                    new() { Field = ConditionField.RiskScore, Operator = ConditionOperator.GreaterThanOrEqual, Value = "0.26" }
                },
                Decision          = Decision.Warn,
                RecommendedAction = RecommendedAction.FlagForReview,
                CanAutoEngage     = false,
                ReasonCodes       = new() { "RULE:MEDIUM_WARN" }
            },

            // ── Priority 40: Explicit FinancialFraud category → Escalate ───────
            new()
            {
                Name     = "Financial Fraud - Escalate",
                Enabled  = true,
                Priority = 40,
                Conditions = new()
                {
                    new() { Field = ConditionField.ThreatCategory, Operator = ConditionOperator.Equals, Value = "FinancialFraud" },
                    new() { Field = ConditionField.Confidence,     Operator = ConditionOperator.GreaterThanOrEqual, Value = "0.6" }
                },
                Decision          = Decision.Challenge,
                RecommendedAction = RecommendedAction.EscalateToAnalyst,
                CanAutoEngage     = false,
                ReasonCodes       = new() { "RULE:FINANCIAL_FRAUD_ESCALATE" }
            },

            // ── Priority 100: Catch-all / LOW → Allow ───────────────────────────
            new()
            {
                Name      = "Default - Allow",
                Enabled   = true,
                Priority  = 100,
                Conditions = new(),   // no conditions — always matches
                Decision          = Decision.Allow,
                RecommendedAction = RecommendedAction.PassThrough,
                CanAutoEngage     = false,
                ReasonCodes       = new() { "RULE:DEFAULT_ALLOW" }
            }
        }.AsReadOnly();
    }
}
