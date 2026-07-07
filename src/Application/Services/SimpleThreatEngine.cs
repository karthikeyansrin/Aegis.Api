using Aegis.Application.Interfaces;
using Aegis.Application.DTOs;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Application.Services;

public class SimpleThreatEngine : IThreatEngine
{
    private static readonly (string Keyword, ThreatCategory Category)[] ScamKeywords = new[]
    {
        ("wire transfer",          ThreatCategory.FinancialFraud),
        ("western union",          ThreatCategory.FinancialFraud),
        ("send money",             ThreatCategory.FinancialFraud),
        ("verify your identity",   ThreatCategory.IdentityTheft),
        ("urgent action required", ThreatCategory.SocialEngineering),
        ("claim your prize",       ThreatCategory.PrizeSweepstakes),
        ("limited time",           ThreatCategory.SocialEngineering),
        ("bitcoin",                ThreatCategory.InvestmentFraud),
        ("crypto",                 ThreatCategory.InvestmentFraud),
        ("gift card",              ThreatCategory.FinancialFraud)
    };

    public Task<ThreatAssessment> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default)
    {
        var content = request.Content?.ToLowerInvariant() ?? string.Empty;

        var hits = ScamKeywords
            .Where(k => content.Contains(k.Keyword))
            .ToArray();

        var score = Math.Min(1.0, hits.Length / 3.0);
        var isThreat = score >= 0.5 || content.Contains("congrat");

        var dominantCategory = hits.Length > 0
            ? hits.GroupBy(h => h.Category)
                  .OrderByDescending(g => g.Count())
                  .First().Key
            : ThreatCategory.None;

        var level = ThreatAssessment.RiskScoreToLevel(score);

        var indicators = hits.Select(h => new ThreatIndicator
        {
            Code = $"KEYWORD:{h.Keyword.Replace(" ", "_").ToUpper()}",
            Description = $"Message contains scam keyword: '{h.Keyword}'",
            Weight = 1.0 / 3.0
        }).ToList();

        var reasonCodes = new List<string>();
        if (hits.Length > 0) reasonCodes.Add("KEYWORD_MATCH");
        if (content.Contains("congrat")) reasonCodes.Add("CONGRATULATIONS_PATTERN");

        var confidence = isThreat ? Math.Max(0.5, score) : score;

        var assessment = new ThreatAssessment
        {
            RiskScore = score,
            Confidence = confidence,
            Level = level,
            Category = isThreat ? dominantCategory : ThreatCategory.None,
            Indicators = indicators,
            ReasonCodes = reasonCodes,
            CanAutoEngage = isThreat && confidence >= 0.5
        };

        return Task.FromResult(assessment);
    }
}