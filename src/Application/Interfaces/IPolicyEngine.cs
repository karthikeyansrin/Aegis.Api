using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

/// <summary>
/// Evaluates the active policy set against a ThreatAssessment and returns a DecisionResult.
/// </summary>
public interface IPolicyEngine
{
    Task<DecisionResult> EvaluateAsync(ThreatAssessment assessment, CancellationToken ct = default);
}
