using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Application.Interfaces;

/// <summary>
/// Global cross-session threat intelligence store.
/// Maintains a registry of all seen ThreatIndicators with occurrence metadata,
/// enabling enrichment of new assessments with historical signal.
/// </summary>
public interface IThreatIntelligenceEngine
{
    /// <summary>
    /// Records a batch of new indicators from the current session.
    /// Increments occurrence count and updates LastSeenUtc for existing records.
    /// Creates new GlobalIndicatorRecords for first-time values.
    /// </summary>
    Task RecordIndicatorsAsync(
        IEnumerable<ThreatIndicator> indicators,
        CancellationToken ct = default);

    /// <summary>
    /// Looks up a single indicator by type and value.
    /// Returns null when the indicator has never been seen globally.
    /// </summary>
    Task<GlobalIndicatorRecord?> LookupIndicatorAsync(
        IndicatorType type,
        string value,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the computed cross-session risk contribution (0.0–1.0) for an indicator.
    /// Returns 0.0 when the indicator is unknown.
    /// </summary>
    Task<double> GetIndicatorRiskAsync(
        IndicatorType type,
        string value,
        CancellationToken ct = default);

    /// <summary>
    /// Enriches a ThreatAssessment by matching the session's extracted indicators
    /// against the global registry and applying a risk boost for known-bad signals.
    /// </summary>
    Task<EnrichedThreatAssessment> EnrichAsync(
        ThreatAssessment assessment,
        IEnumerable<ThreatIndicator> sessionIndicators,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all stored records matching the given type.
    /// Supports future search / admin UI scenarios.
    /// </summary>
    Task<IReadOnlyList<GlobalIndicatorRecord>> SearchAsync(
        IndicatorType? type = null,
        string? valueContains = null,
        CancellationToken ct = default);
}
