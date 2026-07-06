using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Application.Services;
using Aegis.Application.Interfaces;
using Aegis.Application.DTOs;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

public interface IScamAnalysisService
{
    Task<ScamAnalysisResponse> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default);
}
