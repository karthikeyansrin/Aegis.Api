using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aegis.Application.Interfaces;
using Aegis.Domain.Entities;
using Aegis.Domain.Enums;

namespace Aegis.Application.Engines;

/// <summary>
/// Thread-safe in-memory implementation of IThreatIntelligenceEngine.
///
/// Stores GlobalIndicatorRecords keyed by "{Type}:{Value}" (lower-cased).
/// Risk contribution grows logarithmically with occurrence count, capped at 0.5
/// so it can never solely push a score to Critical — it amplifies existing signals.
///
/// Replace storage with an EF Core repository (Singleton) when PostgreSQL persistence
/// is needed; the interface and all consumers require zero changes.
/// </summary>
public sealed class ThreatIntelligenceEngine : IThreatIntelligenceEngine
{
    // ── Type weights: how much each indicator type contributes to risk ─────────
    private static readonly IReadOnlyDictionary<IndicatorType, double> TypeWeights
        = new Dictionary<IndicatorType, double>
        {
            [IndicatorType.UpiId]       = 0.40,
            [IndicatorType.BankAccount] = 0.40,
            [IndicatorType.PhoneNumber] = 0.25,
            [IndicatorType.Url]         = 0.20,
            [IndicatorType.IfscCode]    = 0.15,
            [IndicatorType.EmailAddress]= 0.15,
            [IndicatorType.Unknown]     = 0.05,
        };

    // Max additive boost a single indicator record can contribute
    private const double MaxSingleBoost    = 0.15;
    // Max total boost applied to a ThreatAssessment from all matched records
    private const double MaxTotalBoost     = 0.50;

    private readonly ConcurrentDictionary<string, GlobalIndicatorRecord> _registry = new();

    // ── RecordIndicator ───────────────────────────────────────────────────────

    public Task RecordIndicatorsAsync(
        IEnumerable<ThreatIndicator> indicators,
        CancellationToken ct = default)
    {
        foreach (var indicator in indicators)
        {
            if (string.IsNullOrWhiteSpace(indicator.Value)) continue;

            var key = GlobalIndicatorRecord.BuildKey(indicator.Type, indicator.Value);

            _registry.AddOrUpdate(
                key,
                // Factory: first time we see this value
                _ =>
                {
                    var record = new GlobalIndicatorRecord
                    {
                        Type  = indicator.Type,
                        Value = indicator.Value.Trim()
                    };
                    record.RiskContribution = ComputeRisk(record.OccurrenceCount, indicator.Type);
                    return record;
                },
                // Update: increment occurrence count and refresh metadata
                (_, existing) =>
                {
                    existing.OccurrenceCount++;
                    existing.LastSeenUtc      = DateTime.UtcNow;
                    existing.RiskContribution = ComputeRisk(existing.OccurrenceCount, existing.Type);
                    return existing;
                });
        }

        return Task.CompletedTask;
    }

    // ── LookupIndicator ───────────────────────────────────────────────────────

    public Task<GlobalIndicatorRecord?> LookupIndicatorAsync(
        IndicatorType type,
        string value,
        CancellationToken ct = default)
    {
        var key = GlobalIndicatorRecord.BuildKey(type, value);
        _registry.TryGetValue(key, out var record);
        return Task.FromResult(record);
    }

    // ── GetIndicatorRisk ──────────────────────────────────────────────────────

    public Task<double> GetIndicatorRiskAsync(
        IndicatorType type,
        string value,
        CancellationToken ct = default)
    {
        var key = GlobalIndicatorRecord.BuildKey(type, value);
        var risk = _registry.TryGetValue(key, out var record)
            ? record.RiskContribution
            : 0.0;
        return Task.FromResult(risk);
    }

    // ── Enrich ────────────────────────────────────────────────────────────────

    public Task<EnrichedThreatAssessment> EnrichAsync(
        ThreatAssessment assessment,
        IEnumerable<ThreatIndicator> sessionIndicators,
        CancellationToken ct = default)
    {
        var matched       = new List<GlobalIndicatorRecord>();
        var totalBoost    = 0.0;
        var hasFlagged    = false;

        foreach (var indicator in sessionIndicators)
        {
            if (string.IsNullOrWhiteSpace(indicator.Value)) continue;

            var key = GlobalIndicatorRecord.BuildKey(indicator.Type, indicator.Value);
            if (!_registry.TryGetValue(key, out var record)) continue;

            matched.Add(record);

            if (record.IsFlagged) hasFlagged = true;

            // Boost capped per indicator to prevent a single indicator dominating
            var boost = Math.Min(record.RiskContribution, MaxSingleBoost);
            totalBoost = Math.Min(totalBoost + boost, MaxTotalBoost);
        }

        var enriched = new EnrichedThreatAssessment
        {
            Original              = assessment,
            MatchedRecords        = matched.AsReadOnly(),
            IntelligenceRiskBoost = totalBoost,
            HasFlaggedIndicators  = hasFlagged
        };

        return Task.FromResult(enriched);
    }

    // ── Search ────────────────────────────────────────────────────────────────

    public Task<IReadOnlyList<GlobalIndicatorRecord>> SearchAsync(
        IndicatorType? type = null,
        string? valueContains = null,
        CancellationToken ct = default)
    {
        IEnumerable<GlobalIndicatorRecord> results = _registry.Values;

        if (type.HasValue)
            results = results.Where(r => r.Type == type.Value);

        if (!string.IsNullOrWhiteSpace(valueContains))
            results = results.Where(r =>
                r.Value.Contains(valueContains, StringComparison.OrdinalIgnoreCase));

        IReadOnlyList<GlobalIndicatorRecord> list =
            results.OrderByDescending(r => r.OccurrenceCount).ToList().AsReadOnly();

        return Task.FromResult(list);
    }

    // ── Risk calculation ──────────────────────────────────────────────────────

    /// <summary>
    /// Risk grows logarithmically: first occurrence = baseWeight,
    /// doubles at ~7 occurrences, plateaus at MaxSingleBoost.
    /// Formula: min(baseWeight * log2(count + 1), MaxSingleBoost)
    /// </summary>
    private static double ComputeRisk(int occurrenceCount, IndicatorType type)
    {
        var baseWeight = TypeWeights.TryGetValue(type, out var w) ? w : 0.05;
        var raw        = baseWeight * Math.Log2(occurrenceCount + 1);
        return Math.Min(Math.Round(raw, 4), MaxSingleBoost);
    }
}
