using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Application.Services;
using Aegis.Application.Interfaces;
using System.Text.Json;
using Aegis.Application.DTOs;
using Aegis.Domain.Entities;

namespace Aegis.Application.Services;

public class ScamDetectionService : IScamAnalysisService
{
    private readonly IGroqService _groq;
    private readonly SimpleScamAnalysisService _fallback;
    private const string DefaultModel = "llama-3.1-8b-instant";

    public ScamDetectionService(IGroqService groq, SimpleScamAnalysisService fallback)
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

    public async Task<ScamAnalysisResponse> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default)
    {
        // Ensure we always return a result and never throw unhandled exceptions.
        if (request is null)
        {
            return await _fallback.AnalyzeAsync(new ScamAnalysisRequest { Content = string.Empty }, ct);
        }

            try
            {
                var messages = new[]
                {
                    new ChatMessage("system", "You are a scam-classification assistant. Reply ONLY with a JSON object with three fields: is_scam (boolean), scam_type (string or null), confidence (number between 0 and 1). Do not include any extra commentary or markup. If unknown, use false, null, 0.0."),
                    new ChatMessage("user", $"Classify the following message for scam likelihood and type. Respond only with JSON. Message:\n{request.Content}")
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

                        // Parse is_scam
                        bool isScam = false;
                        if (root.TryGetProperty("is_scam", out var isScamElem))
                        {
                            if (isScamElem.ValueKind == JsonValueKind.True) isScam = true;
                            else if (isScamElem.ValueKind == JsonValueKind.False) isScam = false;
                            else if (isScamElem.ValueKind == JsonValueKind.Number && isScamElem.TryGetDouble(out var d)) isScam = d >= 0.5;
                            else if (isScamElem.ValueKind == JsonValueKind.String && bool.TryParse(isScamElem.GetString(), out var b)) isScam = b;
                        }

                        // Parse scam_type
                        string? scamType = null;
                        if (root.TryGetProperty("scam_type", out var scamTypeElem) && scamTypeElem.ValueKind != JsonValueKind.Null)
                        {
                            scamType = scamTypeElem.GetString();
                        }

                        // Parse confidence
                        double confidence = 0.0;
                        if (root.TryGetProperty("confidence", out var confElem))
                        {
                            if (confElem.ValueKind == JsonValueKind.Number && confElem.TryGetDouble(out var c)) confidence = c;
                            else if (confElem.ValueKind == JsonValueKind.String && double.TryParse(confElem.GetString(), out var c2)) confidence = c2;
                        }

                        // Clamp confidence
                        if (double.IsNaN(confidence) || confidence < 0) confidence = 0.0;
                        if (confidence > 1) confidence = 1.0;

                        var summary = scamType ?? (isScam ? "scam" : "not_scam");

                        var response = new ScamAnalysisResponse
                        {
                            Id = Guid.NewGuid().ToString("D"),
                            IsScam = isScam,
                            Summary = summary,
                            Evidence = new Dictionary<string, object?>
                            {
                                ["scam_type"] = scamType,
                                ["confidence"] = confidence,
                                ["model_raw"] = result.RawJson ?? result.Content
                            }
                        };

                        return response;
                    }
                    catch
                    {
                        // parsing failed — fall back to safe default below
                    }
                }
            }
            catch
            {
                // Any failure with the model — fall back to safe default below
            }

            // Safe fallback when LLM fails or returns invalid output
            try
            {
                return new ScamAnalysisResponse
                {
                    Id = Guid.NewGuid().ToString("D"),
                    IsScam = true,
                    Summary = "unknown",
                    Evidence = new Dictionary<string, object?>
                    {
                        ["confidence"] = 0.5,
                        ["fallback"] = true
                    }
                };
            }
            catch
            {
                // As a last resort, return a minimal safe response
                return new ScamAnalysisResponse
                {
                    Id = Guid.NewGuid().ToString("D"),
                    IsScam = true,
                    Summary = "unknown",
                    Evidence = new Dictionary<string, object?> { ["confidence"] = 0.5 }
                };
            }
    }
}
