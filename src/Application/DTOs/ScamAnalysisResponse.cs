using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Domain.Entities;
using Aegis.Application.DTOs;
using Aegis.Application.Services;
using Aegis.Application.Interfaces;
namespace Aegis.Application.DTOs;

public sealed class ScamAnalysisResponse
{
    public required string Id { get; init; }
    public required bool IsScam { get; init; }
    public required string Summary { get; init; }
    public Dictionary<string, object?>? Evidence { get; init; }
}
