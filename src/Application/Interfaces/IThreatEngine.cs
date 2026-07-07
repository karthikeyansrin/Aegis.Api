using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.DTOs;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

public interface IThreatEngine
{
    Task<ThreatAssessment> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default);
}