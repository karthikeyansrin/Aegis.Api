namespace Aegis.Domain.Enums;

public enum ConditionOperator
{
    Equals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains
}

public enum ConditionField
{
    ThreatLevel,
    RiskScore,
    Confidence,
    ThreatCategory,
    CanAutoEngage
}
