using System.Threading;
using Aegis.Api.Models;
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

    public HoneypotController(IScamAnalysisService detector, ConversationStore store, IntelligenceExtractionService extractor, HoneypotAgentService agent)
    {
        _detector = detector;
        _store = store;
        _extractor = extractor;
        _agent = agent;
    }

    /// <summary>
    /// Analyze a aegis-captured message for scam indicators, update session, extract intelligence, and generate an agent reply when applicable.
    /// </summary>
    [HttpPost("analyze")]
    [Consumes("application/json")]
    public async Task<IActionResult> Analyze([FromBody] HoneypotRequest? request, CancellationToken cancellationToken = default)
    {
        if (request is null)
            return BadRequest(new { error = "Request body is required" });

        var (isValid, error) = request.Validate();
        if (!isValid)
            return BadRequest(new { error });

        try
        {
            // 1. Detect scam
            var analysisRequest = new ScamAnalysisRequest { Content = request.Message, Source = request.LanguageHint };
            var analysis = await _detector.AnalyzeAsync(analysisRequest, cancellationToken);

            // 2. Update conversation state
            try
            {
                var session = _store.GetOrCreateSession(request.SessionId);
                session.AppendMessage("user", request.Message, request.Timestamp);
            }
            catch
            {
                // continue even if session update fails
            }

            // 3. Extract intelligence (merge into session)
            var extracted = await _extractor.ExtractAsync(request.SessionId, request.Message, true, cancellationToken);

            // 4. Generate agent reply only if scam
            string? agentReply = null;
            try
            {
                agentReply = await _agent.GenerateAgentReplyAsync(request.SessionId, request.Message, analysis.IsScam, cancellationToken);
            }
            catch
            {
                // swallow to keep latency low and response valid
            }

            // 5. Build final response using the typed HoneypotResponse model
            double confidence = 0.0;
            if (analysis.Evidence != null && analysis.Evidence.TryGetValue("confidence", out var cVal))
            {
                if (cVal is double d) confidence = d;
                else if (cVal is float f) confidence = f;
                else if (cVal is decimal dec) confidence = (double)dec;
                else if (double.TryParse(cVal?.ToString(), out var parsed)) confidence = parsed;
            }

            var respModel = new HoneypotResponse
            {
                IsScam = analysis.IsScam,
                ScamType = analysis.Summary,
                Confidence = confidence,
                ExtractedIntelligence = extracted ?? new ExtractedIntelligence(),
                AgentReply = agentReply ?? string.Empty
            };

            return Ok(respModel);
        }
        catch (Exception ex)
        {
            // Ensure we never leak exceptions — always return JSON
            return StatusCode(500, new { error = "internal_error", detail = ex.Message });
        }
    }
}
