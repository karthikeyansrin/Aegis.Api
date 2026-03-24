using Aegis.Api.Models;

namespace Aegis.Api.Services;

public class SimpleScamAnalysisService : IScamAnalysisService
{
    // Very small, deterministic rule set to keep outputs stable.
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

    public Task<ScamAnalysisResponse> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default)
    {
        var content = request.Content?.ToLowerInvariant() ?? string.Empty;

        var matches = ScamKeywords.Where(k => content.Contains(k)).ToArray();

        var score = Math.Min(1.0, matches.Length / 3.0); // simple normalized score

        var isScam = score >= 0.5 || content.Contains("congrat");

        var summary = isScam
            ? "Likely scam content detected based on keyword heuristics."
            : "No strong scam indicators found.";

        var response = new ScamAnalysisResponse
        {
            Id = Guid.NewGuid().ToString("D"),
            IsScam = isScam,
            Summary = summary,
            Evidence = new Dictionary<string, object?>
            {
                ["matched_keywords"] = matches,
                ["score"] = score,
                ["source"] = request.Source
            }
        };

        return Task.FromResult(response);
    }
}
