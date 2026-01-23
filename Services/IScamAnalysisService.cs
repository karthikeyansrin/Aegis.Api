using Aegis.Api.Models;

namespace Aegis.Api.Services;

public interface IScamAnalysisService
{
    Task<ScamAnalysisResponse> AnalyzeAsync(ScamAnalysisRequest request, CancellationToken ct = default);
}
