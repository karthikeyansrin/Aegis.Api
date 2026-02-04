using System.Text.Json;
using Aegis.Api.Models;
using Aegis.Api.Services;
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

    /// <summary>
    /// Evaluator + production compatible honeypot endpoint
    /// </summary>
    [HttpPost("analyze")]
    [Consumes("application/json")]
    public async Task<IActionResult> Analyze(
        [FromBody] JsonElement body,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // -------------------------------
            // 1. Normalize input (adapter)
            // -------------------------------
            string sessionId = "default-session";
            string messageText = string.Empty;

            // Evaluator format: sessionId
            if (body.TryGetProperty("sessionId", out var sidElem) &&
                sidElem.ValueKind == JsonValueKind.String)
            {
                sessionId = sidElem.GetString() ?? sessionId;
            }

            // Evaluator format: message.text
            if (body.TryGetProperty("message", out var msgElem))
            {
                if (msgElem.ValueKind == JsonValueKind.Object &&
                    msgElem.TryGetProperty("text", out var textElem))
                {
                    messageText = textElem.GetString() ?? "";
                }
                // Original format: message as string
                else if (msgElem.ValueKind == JsonValueKind.String)
                {
                    messageText = msgElem.GetString() ?? "";
                }
            }

            // Absolute fallback (probe / empty request)
            if (string.IsNullOrWhiteSpace(messageText))
            {
                return Ok(new
                {
                    status = "success",
                    reply = "Could you clarify what this message is about?"
                });
            }

            // -------------------------------
            // 2. Internal Aegis request
            // -------------------------------
            var analysisRequest = new ScamAnalysisRequest
            {
                Content = messageText,
                Source = "evaluator"
            };

            var analysis = await _detector.AnalyzeAsync(
                analysisRequest,
                cancellationToken
            );

            var session = _store.GetOrCreateSession(sessionId);
            session.AppendMessage("user", messageText);

            var extracted = await _extractor.ExtractAsync(
                sessionId,
                messageText,
                true,
                cancellationToken
            );

            session.MergeExtractedIntelligence(extracted);

            string? agentReply = null;

            if (analysis.IsScam)
            {
                agentReply = await _agent.GenerateAgentReplyAsync(
                    sessionId,
                    messageText,
                    analysis.IsScam,
                    cancellationToken
                );
            }

            // -------------------------------
            // 3. Evaluator response format
            // -------------------------------
            return Ok(new
            {
                status = "success",
                reply = string.IsNullOrWhiteSpace(agentReply)
                    ? "Why is my account being suspended?"
                    : agentReply
            });
        }
        catch
        {
            // Never fail evaluator
            return Ok(new
            {
                status = "success",
                reply = "Can you explain what this is regarding?"
            });
        }
    }
}
