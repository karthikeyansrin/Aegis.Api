namespace Aegis.Api.Models;

public sealed class ScamAnalysisRequest
{
    // Raw content captured by the aegis (email body, message text, etc.)
    public required string Content { get; init; }

    // Optional metadata about the source
    public string? Source { get; init; }
}
