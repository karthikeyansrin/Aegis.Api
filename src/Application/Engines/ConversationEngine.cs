using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.DTOs;
using Aegis.Application.Interfaces;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Application.Engines;

public interface IConversationEngine
{
    Task<ConversationResult> ProcessMessageAsync(string sessionId, string messageText, CancellationToken cancellationToken = default);
}

public class ConversationEngine : IConversationEngine
{
    private readonly IThreatEngine              _detector;
    private readonly IThreatIntelligenceEngine  _intelligence;
    private readonly IPolicyEngine              _policyEngine;
    private readonly IConversationRepository    _store;
    private readonly IThreatIndicatorEngine     _indicatorEngine;
    private readonly IPersonaEngine             _agent;
    private readonly IPersonaRepository         _personaRepo;

    public ConversationEngine(
        IThreatEngine              detector,
        IThreatIntelligenceEngine  intelligence,
        IPolicyEngine              policyEngine,
        IConversationRepository    store,
        IThreatIndicatorEngine     indicatorEngine,
        IPersonaEngine             agent,
        IPersonaRepository         personaRepo)
    {
        _detector        = detector;
        _intelligence    = intelligence;
        _policyEngine    = policyEngine;
        _store           = store;
        _indicatorEngine = indicatorEngine;
        _agent           = agent;
        _personaRepo     = personaRepo;
    }

    public async Task<ConversationResult> ProcessMessageAsync(
        string sessionId,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return MapToResult(
                enriched: null,
                agentReply: "Could you clarify what this message is about?",
                indicators: Array.Empty<ThreatIndicator>());
        }

        try
        {
            var analysisRequest = new ScamAnalysisRequest
            {
                Content = messageText,
                Source  = "api"
            };

            // --- 1. Classify threat ---
            ThreatAssessment assessment = await _detector.AnalyzeAsync(analysisRequest, cancellationToken);

            // --- 2. Load / create session, append incoming message ---
            var session = await _store.GetOrCreateSessionAsync(sessionId, cancellationToken);
            session.AppendMessage("user", messageText);

            // --- 3. Extract ThreatIndicators (regex → LLM → normalize → deduplicate) ---
            IReadOnlyList<ThreatIndicator> newIndicators = await _indicatorEngine.ExtractAsync(
                sessionId,
                messageText,
                existingIndicators: session.ThreatIndicators,
                useLlmFallback: true,
                ct: cancellationToken);

            // --- 4. Merge new indicators into session ---
            session.MergeThreatIndicators(newIndicators);

            // --- 5. Record all session indicators in the global intelligence store ---
            await _intelligence.RecordIndicatorsAsync(newIndicators, cancellationToken);

            // --- 6. Enrich assessment using cross-session intelligence ---
            //        Boosts RiskScore if known-bad indicators are present
            EnrichedThreatAssessment enriched = await _intelligence.EnrichAsync(
                assessment,
                session.ThreatIndicators,
                cancellationToken);

            // --- 7. Build an enriched assessment view for policy evaluation ---
            //        Re-wrap with boosted risk so policy sees the elevated score
            ThreatAssessment effectiveAssessment = enriched.MatchedRecords.Count > 0
                ? new ThreatAssessment
                  {
                      Id = assessment.Id,
                      RiskScore = enriched.EffectiveRiskScore,
                      Confidence = assessment.Confidence,
                      Level = enriched.EffectiveLevel,
                      Category = assessment.Category,
                      Indicators = assessment.Indicators,
                      ReasonCodes = new System.Collections.Generic.List<string>(assessment.ReasonCodes)
                                    { $"INTEL_BOOST:{enriched.IntelligenceRiskBoost:F3}" },
                      CanAutoEngage = assessment.CanAutoEngage,
                      CreatedAt = assessment.CreatedAt
                  }
                : assessment;

            // --- 8. Evaluate policy set against (potentially boosted) assessment ---
            DecisionResult decision = await _policyEngine.EvaluateAsync(effectiveAssessment, cancellationToken);

            // --- 9. Load persona (default for now, can be dynamically selected later) ---
            Persona persona = await _personaRepo.GetDefaultAsync(cancellationToken);

            // --- 10. Generate persona reply (decision policy governs engagement) ---
            var agentReply = decision.CanAutoEngage
                ? await _agent.GenerateAgentReplyAsync(
                    sessionId,
                    messageText,
                    enriched.IsThreat,
                    persona,
                    session.CurrentStage,
                    cancellationToken) ?? "Can you explain what this is regarding?"
                : null;

            // --- 11. Persist session ---
            await _store.SaveChangesAsync(cancellationToken);

            // --- 12. Map to v1 API response contract ---
            return MapToResult(enriched, decision, agentReply, session.ThreatIndicators);
        }
        catch
        {
            try { await _store.SaveChangesAsync(cancellationToken); } catch { /* best-effort */ }

            return MapToResult(
                enriched: null,
                agentReply: "Can you explain what this is regarding?",
                indicators: Array.Empty<ThreatIndicator>());
        }
    }

    /// <summary>
    /// Anti-corruption layer: projects ThreatIndicators + EnrichedThreatAssessment
    /// to the flat v1 API DTO. No business logic — pure projection.
    /// </summary>
    private static ConversationResult MapToResult(
        EnrichedThreatAssessment? enriched,
        DecisionResult? decision,
        string? agentReply,
        IEnumerable<ThreatIndicator> indicators)
    {
        var indicatorList = indicators.ToList();
        var assessment    = enriched?.Original;
        var isScam        = enriched?.IsThreat ?? assessment?.IsThreat ?? false;
        var confidence    = enriched?.Original.Confidence ?? 0.0;
        var scamType      = assessment?.ScamCategory == "not_scam" ? null : assessment?.ScamCategory;

        return new ConversationResult
        {
            IsScam     = isScam,
            ScamType   = scamType,
            Confidence = confidence,
            AgentReply = agentReply ?? "Can you explain what this is regarding?",

            ExtractedIntelligence = new ExtractedIntelligenceDto
            {
                UpiIds       = indicatorList
                                   .Where(i => i.Type == IndicatorType.UpiId)
                                   .Select(i => i.Value).ToList(),
                PhoneNumbers = indicatorList
                                   .Where(i => i.Type == IndicatorType.PhoneNumber)
                                   .Select(i => i.Value).ToList(),
                Urls         = indicatorList
                                   .Where(i => i.Type == IndicatorType.Url)
                                   .Select(i => i.Value).ToList(),
                BankAccounts = indicatorList
                                   .Where(i => i.Type == IndicatorType.BankAccount)
                                   .Select(i => i.Value).ToList()
            }
        };
    }

    // Overload for the empty/error path
    private static ConversationResult MapToResult(
        EnrichedThreatAssessment? enriched,
        string? agentReply,
        IEnumerable<ThreatIndicator> indicators)
        => MapToResult(enriched, decision: null, agentReply, indicators);
}