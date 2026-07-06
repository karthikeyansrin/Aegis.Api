using System.Text.Json;
using Aegis.Application.DTOs;
using Aegis.Domain.Entities;
using Aegis.Application.Interfaces;
using Aegis.Application.Services;
using Aegis.Infrastructure.Persistence;
using Aegis.Infrastructure.AI;
using Microsoft.AspNetCore.Mvc;

namespace Aegis.Api.Controllers;

[ApiController]
[Route("api/aegis")]
[Produces("application/json")]
public class HoneypotController : ControllerBase
{
    private readonly IScamAnalysisService _detector;
    private readonly ConversationStore _store;
    private readonly IntelligenceExtractionService _extractor;
    private readonly HoneypotAgentService _agent;

    public HoneypotController(
        IScamAnalysisService detector,
        ConversationStore store,
        IntelligenceExtractionService extractor,
        HoneypotAgentService agent)
    {
        _detector = detector;
        _store = store;
        _extractor = extractor;
        _agent = agent;
    }

    [HttpPost("analyze")]
    [Consumes("application/json")]
    public async Task<IActionResult> Analyze(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string sessionId = "default-session";
            string messageText = string.Empty;

            if (body.TryGetProperty("sessionId", out var sidElem) &&
                sidElem.ValueKind == JsonValueKind.String)
            {
                sessionId = sidElem.GetString() ?? sessionId;
            }
            else if (body.TryGetProperty("session_id", out var sessionIdElem) &&
                     sessionIdElem.ValueKind == JsonValueKind.String)
            {
                sessionId = sessionIdElem.GetString() ?? sessionId;
            }

            if (body.TryGetProperty("message", out var msgElem))
            {
                if (msgElem.ValueKind == JsonValueKind.Object &&
                    msgElem.TryGetProperty("text", out var textElem))
                {
                    messageText = textElem.GetString() ?? string.Empty;
                }
                else if (msgElem.ValueKind == JsonValueKind.String)
                {
                    messageText = msgElem.GetString() ?? string.Empty;
                }
            }

            if (string.IsNullOrWhiteSpace(messageText) &&
                body.TryGetProperty("text", out var rawTextElem) &&
                rawTextElem.ValueKind == JsonValueKind.String)
            {
                messageText = rawTextElem.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(messageText))
            {
                return Ok(new
                {
                    isScam = false,
                    scamType = (string?)null,
                    confidence = 0.0,
                    agentReply = "Could you clarify what this message is about?",
                    extractedIntelligence = new
                    {
                        upiIds = Array.Empty<string>(),
                        phoneNumbers = Array.Empty<string>(),
                        urls = Array.Empty<string>(),
                        bankAccounts = Array.Empty<string>()
                    }
                });
            }

            var analysisRequest = new ScamAnalysisRequest
            {
                Content = messageText,
                Source = "api"
            };

            var analysis = await _detector.AnalyzeAsync(analysisRequest, cancellationToken);

            var session = _store.GetOrCreateSession(sessionId);
            session.AppendMessage("user", messageText);

            var extracted = await _extractor.ExtractAsync(
                sessionId,
                messageText,
                true,
                cancellationToken);

            session.MergeExtractedIntelligence(extracted);

            var confidence = 0.0;
            if (analysis.Evidence != null &&
                analysis.Evidence.TryGetValue("confidence", out var confidenceValue) &&
                confidenceValue is not null)
            {
                switch (confidenceValue)
                {
                    case double d:
                        confidence = d;
                        break;
                    case float f:
                        confidence = f;
                        break;
                    case decimal m:
                        confidence = (double)m;
                        break;
                    case JsonElement jsonElem when jsonElem.ValueKind == JsonValueKind.Number && jsonElem.TryGetDouble(out var cd):
                        confidence = cd;
                        break;
                    default:
                        if (double.TryParse(confidenceValue.ToString(), out var parsed))
                        {
                            confidence = parsed;
                        }
                        break;
                }
            }

            if (double.IsNaN(confidence) || confidence < 0) confidence = 0.0;
            if (confidence > 1) confidence = 1.0;

            var scamType = analysis.Summary == "not_scam" ? null : analysis.Summary;

            var agentReply = await _agent.GenerateAgentReplyAsync(
                sessionId,
                messageText,
                analysis.IsScam,
                cancellationToken) ?? "Can you explain what this is regarding?";

            return Ok(new
            {
                isScam = analysis.IsScam,
                scamType,
                confidence,
                agentReply,
                extractedIntelligence = new
                {
                    upiIds = session.AggregatedIntelligence.UpiIds,
                    phoneNumbers = session.AggregatedIntelligence.PhoneNumbers,
                    urls = session.AggregatedIntelligence.Urls,
                    bankAccounts = session.AggregatedIntelligence.BankAccounts.Select(b => b.AccountNumber).ToList()
                }
            });
        }
        catch
        {
            return Ok(new
            {
                isScam = false,
                scamType = (string?)null,
                confidence = 0.0,
                agentReply = "Can you explain what this is regarding?",
                extractedIntelligence = new
                {
                    upiIds = Array.Empty<string>(),
                    phoneNumbers = Array.Empty<string>(),
                    urls = Array.Empty<string>(),
                    bankAccounts = Array.Empty<string>()
                }
            });
        }
    }
}
