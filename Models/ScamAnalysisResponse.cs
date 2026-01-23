namespace Aegis.Api.Models;

public sealed class ScamAnalysisResponse
{
    public required string Id { get; init; }
    public required bool IsScam { get; init; }
    public required string Summary { get; init; }
    public Dictionary<string, object?>? Evidence { get; init; }
}
