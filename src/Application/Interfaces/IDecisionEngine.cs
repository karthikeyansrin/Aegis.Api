using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

public interface IDecisionEngine
{
    DecisionResult Evaluate(ThreatAssessment assessment);
}
