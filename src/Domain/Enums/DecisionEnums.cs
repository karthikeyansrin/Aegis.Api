namespace Aegis.Domain.Enums;

public enum Decision
{
    Allow,
    Warn,
    Challenge,
    AutoEngage
}

public enum RecommendedAction
{
    PassThrough,
    FlagForReview,
    NotifyUser,
    EngagePersona,
    EscalateToAnalyst
}
