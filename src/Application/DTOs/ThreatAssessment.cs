namespace Aegis.Application.DTOs;

public class ThreatAssessment
{
    public bool IsThreat { get; set; }
    public double RiskScore { get; set; }
    public string? ScamCategory { get; set; }
    public double Confidence { get; set; }
}