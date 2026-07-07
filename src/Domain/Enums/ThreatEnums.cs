namespace Aegis.Domain.Enums;

public enum ThreatLevel
{
    None = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ThreatCategory
{
    None,
    PhishingAttempt,
    FinancialFraud,
    IdentityTheft,
    SocialEngineering,
    MalwareDelivery,
    PrizeSweepstakes,
    InvestmentFraud,
    RomanceScam,
    TechSupportFraud,
    Unknown
}