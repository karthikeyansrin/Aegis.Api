using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.DTOs;
using Aegis.Application.Interfaces;

namespace Aegis.Application.Engines;

public interface IConversationEngine
{
    Task<ConversationResult> ProcessMessageAsync(string sessionId, string messageText, CancellationToken cancellationToken = default);
}

public class ConversationEngine : IConversationEngine
{
    private readonly IThreatEngine _detector;
    private readonly IConversationStore _store;
    private readonly IIntelligenceEngine _extractor;
    private readonly IPersonaEngine _agent;

    public ConversationEngine(
        IThreatEngine detector,
        IConversationStore store,
        IIntelligenceEngine extractor,
        IPersonaEngine agent)
    {
        _detector = detector;
        _store = store;
        _extractor = extractor;
        _agent = agent;
    }

    public async Task<ConversationResult> ProcessMessageAsync(string sessionId, string messageText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageText))
        {
            return new ConversationResult
            {
                IsScam = false,
                ScamType = null,
                Confidence = 0.0,
                AgentReply = "Could you clarify what this message is about?",
                ExtractedIntelligence = new ExtractedIntelligenceDto()
            };
        }

        try
        {
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

            var agentReply = await _agent.GenerateAgentReplyAsync(
                sessionId,
                messageText,
                analysis.IsThreat,
                cancellationToken) ?? "Can you explain what this is regarding?";

            return new ConversationResult
            {
                IsScam = analysis.IsThreat,
                ScamType = analysis.ScamCategory == "not_scam" ? null : analysis.ScamCategory,
                Confidence = analysis.Confidence,
                AgentReply = agentReply,
                ExtractedIntelligence = new ExtractedIntelligenceDto
                {
                    UpiIds = session.AggregatedIntelligence.UpiIds,
                    PhoneNumbers = session.AggregatedIntelligence.PhoneNumbers,
                    Urls = session.AggregatedIntelligence.Urls,
                    BankAccounts = session.AggregatedIntelligence.BankAccounts.Select(b => b.AccountNumber).ToList()
                }
            };
        }
        catch
        {
            return new ConversationResult
            {
                IsScam = false,
                ScamType = null,
                Confidence = 0.0,
                AgentReply = "Can you explain what this is regarding?",
                ExtractedIntelligence = new ExtractedIntelligenceDto()
            };
        }
    }
}