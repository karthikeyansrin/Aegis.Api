using System;
using System.Collections.Generic;
using System.Linq;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

public class ConversationSession
{
    public string SessionId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTime LastUpdatedUtc { get; set; }

    public List<MessageEntry> History { get; set; } = new();
    public ExtractedIntelligence AggregatedIntelligence { get; set; } = new();

    /// <summary>
    /// All ThreatIndicators extracted across the lifetime of this session.
    /// Each extracted entity (UPI ID, phone, URL, bank account) lives here as a typed indicator.
    /// </summary>
    public List<ThreatIndicator> ThreatIndicators { get; set; } = new();

    /// <summary>
    /// The current stage of the conversation. Used by PersonaEngine to alter engagement tactics.
    /// </summary>
    public ConversationStage CurrentStage { get; set; } = ConversationStage.Clarify;

    public ConversationSession(string sessionId)
    {
        SessionId = sessionId ?? throw new ArgumentNullException(nameof(sessionId));
        CreatedAt = DateTimeOffset.UtcNow;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public void AppendMessage(string role, string content, DateTimeOffset? timestamp = null)
    {
        History.Add(new MessageEntry
        {
            Id = Guid.NewGuid(),
            SessionId = this.SessionId,
            Role = role ?? "user",
            Content = content ?? string.Empty,
            Timestamp = timestamp ?? DateTimeOffset.UtcNow
        });
        LastUpdatedUtc = DateTime.UtcNow;
    }

    public void MergeExtractedIntelligence(ExtractedIntelligence? intel)
    {
        if (intel is null) return;

        if (intel.UpiIds != null)
        {
            foreach (var id in intel.UpiIds)
                if (!AggregatedIntelligence.UpiIds.Contains(id)) AggregatedIntelligence.UpiIds.Add(id);
        }

        if (intel.PhoneNumbers != null)
        {
            foreach (var n in intel.PhoneNumbers)
                if (!AggregatedIntelligence.PhoneNumbers.Contains(n)) AggregatedIntelligence.PhoneNumbers.Add(n);
        }

        if (intel.Urls != null)
        {
            foreach (var u in intel.Urls)
                if (!AggregatedIntelligence.Urls.Contains(u)) AggregatedIntelligence.Urls.Add(u);
        }

        if (intel.BankAccounts != null)
        {
            var existing = new HashSet<string>(AggregatedIntelligence.BankAccounts.Select(b => b.AccountNumber));
            foreach (var b in intel.BankAccounts)
            {
                if (b == null || string.IsNullOrWhiteSpace(b.AccountNumber)) continue;
                if (!existing.Contains(b.AccountNumber))
                {
                    AggregatedIntelligence.BankAccounts.Add(new BankAccount 
                    { 
                        Id = Guid.NewGuid(),
                        ExtractedIntelligenceId = AggregatedIntelligence.Id,
                        AccountNumber = b.AccountNumber, 
                        Ifsc = b.Ifsc 
                    });
                    existing.Add(b.AccountNumber);
                }
            }
        }
        LastUpdatedUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Merges new ThreatIndicators into the session, deduplicating by Type + Value.
    /// </summary>
    public void MergeThreatIndicators(IEnumerable<ThreatIndicator> indicators)
    {
        if (indicators is null) return;

        var existingKeys = new HashSet<string>(
            ThreatIndicators.Select(i => $"{i.Type}:{i.Value}"),
            StringComparer.OrdinalIgnoreCase);

        foreach (var indicator in indicators)
        {
            if (string.IsNullOrWhiteSpace(indicator.Value)) continue;
            var key = $"{indicator.Type}:{indicator.Value}";
            if (existingKeys.Add(key))
                ThreatIndicators.Add(indicator);
        }

        LastUpdatedUtc = DateTime.UtcNow;
    }
}


public class MessageEntry
{
    public Guid Id { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    
    public ConversationSession Conversation { get; set; } = null!;
}

public class ExtractedIntelligence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string SessionId { get; set; } = string.Empty;

    public List<string> UpiIds { get; set; } = new();
    public List<string> PhoneNumbers { get; set; } = new();
    public List<string> Urls { get; set; } = new();

    public List<BankAccount> BankAccounts { get; set; } = new();
    
    public ConversationSession Conversation { get; set; } = null!;
}

public class BankAccount
{
    public Guid Id { get; set; }
    public Guid ExtractedIntelligenceId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string? Ifsc { get; set; }
    
    public ExtractedIntelligence ExtractedIntelligence { get; set; } = null!;
}
