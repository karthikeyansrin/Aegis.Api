using System;
using System.Collections.Generic;

namespace Aegis.Application.DTOs;

public class ConversationResult
{
    public bool IsScam { get; set; }
    public string? ScamType { get; set; }
    public double Confidence { get; set; }
    public string AgentReply { get; set; } = string.Empty;
    public ExtractedIntelligenceDto ExtractedIntelligence { get; set; } = new();
}

public class ExtractedIntelligenceDto
{
    public IEnumerable<string> UpiIds { get; set; } = Array.Empty<string>();
    public IEnumerable<string> PhoneNumbers { get; set; } = Array.Empty<string>();
    public IEnumerable<string> Urls { get; set; } = Array.Empty<string>();
    public IEnumerable<string> BankAccounts { get; set; } = Array.Empty<string>();
}