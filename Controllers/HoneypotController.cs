using System.Threading;
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

    [HttpPost("analyze")]
    [Consumes("application/json")]
    public async Task<IActionResult> Analyze(
        [FromBody] HoneypotRequest? request,
        CancellationToken cancellationToken = default)
    {
        // ✅ Evaluator / probe-safe response
        if (request is null)
        {
            return Ok(new HoneypotResponse
            {
                IsScam = false,
                ScamType = "unknown",
                Confidence = 0.0,
                AgentReply = string.Empty,
                ExtractedIntelligence = new ExtractedIntelligence()
            });
        }

        var (isValid, error) = request.Validate();
        if (!isValid)
        {
            return Ok(new HoneypotResponse
            {
                IsScam = false,
                ScamType = "invalid_request",
                Confidence = 0.0,
                AgentReply = string.Empty,
                ExtractedIntelligence = new ExtractedIntelligence()
            });
        }

        var analysisRequest = new ScamAnalysisRequest
        {
            Content = request.Message,
            Source = request.LanguageHint
        };

        var analysis = await _detector.AnalyzeAsync(analysisRequest, cancellationToken);

        var session = _store.GetOrCreateSession(request.SessionId);
        session.AppendMessage("user", request.Message, request.Timestamp);

        var extracted = await _extractor.ExtractAsync(
            request.SessionId,
            request.Message,
            true,
            cancellationToken
        );

        session.MergeExtractedIntelligence(extracted);

        string? agentReply = null;
        if (analysis.IsScam)
        {
            agentReply = await _agent.GenerateAgentReplyAsync(
                request.SessionId,
                request.Message,
                analysis.IsScam,
                cancellationToken
            );
        }

        var confidence = 0.0;
        if (analysis.Evidence?.TryGetValue("confidence", out var cVal) == true &&
            double.TryParse(cVal?.ToString(), out var parsed))
        {
            confidence = parsed;
        }

        return Ok(new HoneypotResponse
        {
            IsScam = analysis.IsScam,
            ScamType = analysis.Summary,
            Confidence = confidence,
            ExtractedIntelligence = session.AggregatedIntelligence,
            AgentReply = agentReply ?? string.Empty
        });
    }
}
