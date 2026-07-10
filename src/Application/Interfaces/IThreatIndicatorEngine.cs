using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;

namespace Aegis.Application.Interfaces;

/// <summary>
/// Extracts, normalizes, deduplicates, and returns ThreatIndicators from a message.
/// Each returned indicator is a typed, source-tagged, deduplicated finding.
/// </summary>
public interface IThreatIndicatorEngine
{
    /// <summary>
    /// Runs the full extraction pipeline (regex → LLM fallback → normalize → deduplicate)
    /// against <paramref name="message"/> and returns new indicators not already in
    /// the session's existing set.
    /// </summary>
    Task<IReadOnlyList<ThreatIndicator>> ExtractAsync(
        string sessionId,
        string message,
        IReadOnlyList<ThreatIndicator> existingIndicators,
        bool useLlmFallback = true,
        CancellationToken ct = default);
}
