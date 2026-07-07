using Aegis.Application.Interfaces;
using Aegis.Application.DTOs;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Application.Services;

public class SimpleThreatEngine : IThreatEngine
{
    private static readonly string[] ScamKeywords = new[]
    {
        "wire transfer",
        "western union",
        "send money",
        "verify your identity",
        "urgent action required",
        "claim your prize",
        "limited time",
        "bitcoin",
        "crypto",
        "gift card"
    };

    public Task<ThreatAssessment> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default)
    {
        var content = request.Content?.ToLowerInvariant() ?? string.Empty;

        var matches = ScamKeywords.Where(k => content.Contains(k)).ToArray();

        var score = Math.Min(1.0, matches.Length / 3.0); 

        var isScam = score >= 0.5 || content.Contains("congrat");

        var category = isScam ? "keyword_match" : null;

        var assessment = new ThreatAssessment
        {
            IsThreat = isScam,
            ScamCategory = category,
            Confidence = isScam ? Math.Max(0.5, score) : score,
            RiskScore = score
        };

        return Task.FromResult(assessment);
    }
}