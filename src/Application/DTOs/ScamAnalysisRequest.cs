using Aegis.Infrastructure.AI;
using Aegis.Infrastructure.Persistence;
using Aegis.Domain.Entities;
using Aegis.Application.DTOs;
using Aegis.Application.Services;
using Aegis.Application.Interfaces;
namespace Aegis.Application.DTOs;

public sealed class ScamAnalysisRequest
{
    // Raw content captured by the aegis (email body, message text, etc.)
    public required string Content { get; init; }

    // Optional metadata about the source
    public string? Source { get; init; }
}
