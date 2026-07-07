using Aegis.Infrastructure.AI;
using Aegis.Application.Interfaces;
using Aegis.Application.DTOs;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Aegis.Application.Services;

public class ThreatEngine : IThreatEngine
{
    private readonly ILLMProvider _groq;
    private readonly SimpleThreatEngine _fallback;
    private const string DefaultModel = "llama-3.1-8b-instant";

    public ThreatEngine(ILLMProvider groq, SimpleThreatEngine fallback)
    {
        _groq = groq ?? throw new ArgumentNullException(nameof(groq));
        _fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
    }

    private static string? ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start >= 0 && end > start)
            return text.Substring(start, end - start + 1);
        return null;
    }

    private static ThreatCategory ParseCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return ThreatCategory.None;

        var lower = raw.ToLowerInvariant();
        return lower switch
        {
            var s when s.Contains("phish")                            => ThreatCategory.PhishingAttempt,
            var s when s.Contains("financial") || s.Contains("fraud") => ThreatCategory.FinancialFraud,
            var s when s.Contains("identity")                         => ThreatCategory.IdentityTheft,
            var s when s.Contains("social")                           => ThreatCategory.SocialEngineering,
            var s when s.Contains("malware")                          => ThreatCategory.MalwareDelivery,
            var s when s.Contains("prize") || s.Contains("lottery")   => ThreatCategory.PrizeSweepstakes,
            var s when s.Contains("invest")                           => ThreatCategory.InvestmentFraud,
            var s when s.Contains("romance")                          => ThreatCategory.RomanceScam,
            var s when s.Contains("tech")                             => ThreatCategory.TechSupportFraud,
            _                                                         => ThreatCategory.Unknown
        };
    }

    private static ThreatLevel RiskToLevel(double riskScore)
        => ThreatAssessment.RiskScoreToLevel(riskScore);

    public async Task<ThreatAssessment> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default)
    {
        if (request is null)
            return await _fallback.AnalyzeAsync(new ScamAnalysisRequest { Content = string.Empty }, ct);

        try
        {
            var messages = new[]
            {
                new ChatMessage("system",
                    "You are a scam-classification assistant. Reply ONLY with a JSON object with three fields: " +
                    "is_scam (boolean), scam_type (string or null), confidence (number between 0 and 1). " +
                    "Do not include any extra commentary or markup. If unknown, use false, null, 0.0."),
                new ChatMessage("user",
                    $"Classify the following message for scam likelihood and type. Respond only with JSON. Message:\n{request.Content}")
            };

            var result = await _groq.CreateChatCompletionAsync(DefaultModel, messages, ct);

            if (result?.Success == true && !string.IsNullOrWhiteSpace(result.Content))
            {
                try
                {
                    var json = ExtractJson(result.Content);
                    if (json == null) throw new JsonException();

                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    bool isScam = false;
                    if (root.TryGetProperty("is_scam", out var isScamElem))
                    {
                        if (isScamElem.ValueKind == JsonValueKind.True) isScam = true;
                        else if (isScamElem.ValueKind == JsonValueKind.False) isScam = false;
                        else if (isScamElem.ValueKind == JsonValueKind.Number && isScamElem.TryGetDouble(out var d)) isScam = d >= 0.5;
                        else if (isScamElem.ValueKind == JsonValueKind.String && bool.TryParse(isScamElem.GetString(), out var b)) isScam = b;
                    }

                    string? scamTypeRaw = null;
                    if (root.TryGetProperty("scam_type", out var scamTypeElem) && scamTypeElem.ValueKind != JsonValueKind.Null)
                        scamTypeRaw = scamTypeElem.GetString();

                    double confidence = 0.0;
                    if (root.TryGetProperty("confidence", out var confElem))
                    {
                        if (confElem.ValueKind == JsonValueKind.Number && confElem.TryGetDouble(out var c)) confidence = c;
                        else if (confElem.ValueKind == JsonValueKind.String && double.TryParse(confElem.GetString(), out var c2)) confidence = c2;
                    }

                    if (double.IsNaN(confidence) || confidence < 0) confidence = 0.0;
                    if (confidence > 1) confidence = 1.0;

                    var category = ParseCategory(scamTypeRaw);
                    var riskScore = isScam ? Math.Max(0.5, confidence) : (confidence * 0.49);
                    var level = RiskToLevel(riskScore);

                    var indicators = new List<ThreatIndicator>();
                    if (isScam)
                    {
                        indicators.Add(new ThreatIndicator
                        {
                            Code = "LLM_CLASSIFICATION",
                            Description = $"LLM classified message as scam (type: {scamTypeRaw ?? "unknown"})",
                            Weight = confidence
                        });
                    }

                    var reasonCodes = new List<string>();
                    if (isScam) reasonCodes.Add("LLM_SCAM_DETECTED");
                    if (!string.IsNullOrWhiteSpace(scamTypeRaw)) reasonCodes.Add($"TYPE:{scamTypeRaw!.ToUpper().Replace(" ", "_")}");

                    return new ThreatAssessment
                    {
                        RiskScore = riskScore,
                        Confidence = confidence,
                        Level = level,
                        Category = isScam ? category : ThreatCategory.None,
                        Indicators = indicators,
                        ReasonCodes = reasonCodes,
                        CanAutoEngage = isScam && confidence >= 0.5
                    };
                }
                catch
                {
                    // fall back below
                }
            }
        }
        catch
        {
            // fall back below
        }

        // Safe fallback when LLM fails — default to Medium via RiskScore 0.5
        var fallbackRisk = 0.5;
        return new ThreatAssessment
        {
            RiskScore = fallbackRisk,
            Confidence = 0.5,
            Level = ThreatAssessment.RiskScoreToLevel(fallbackRisk),
            Category = ThreatCategory.Unknown,
            Indicators = new List<ThreatIndicator>
            {
                new() { Code = "LLM_FALLBACK", Description = "LLM unavailable; defaulting to medium threat", Weight = 0.5 }
            },
            ReasonCodes = new List<string> { "LLM_ERROR_FALLBACK" },
            CanAutoEngage = true
        };
    }
}