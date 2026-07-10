using System.Collections.Generic;
using Aegis.Domain.Enums;

namespace Aegis.Domain.Entities;

/// <summary>
/// The output of DecisionEngine: what the system should do given a ThreatAssessment.
/// </summary>
public class DecisionResult
{
    public Decision Decision { get; init; }
    public RecommendedAction RecommendedAction { get; init; }
    public bool CanAutoEngage { get; init; }
    public List<string> ReasonCodes { get; init; } = new();

    // Convenience string representations for v1 API mapping
    public string DecisionLabel     => Decision.ToString().ToLowerInvariant();
    public string ActionLabel       => FormatAction(RecommendedAction);

    private static string FormatAction(RecommendedAction action) => action switch
    {
        RecommendedAction.PassThrough        => "pass_through",
        RecommendedAction.FlagForReview      => "flag_for_review",
        RecommendedAction.NotifyUser         => "notify_user",
        RecommendedAction.EngagePersona      => "engage_persona",
        RecommendedAction.EscalateToAnalyst  => "escalate_to_analyst",
        _                                    => action.ToString().ToLowerInvariant()
    };
}
