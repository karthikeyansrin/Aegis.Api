namespace Aegis.Shared.Options;

public class DecisionEngineOptions
{
    public const string SectionName = "DecisionEngine";

    /// <summary>
    /// Decision rules keyed by ThreatLevel name (case-insensitive).
    /// Allows overriding default LOW/MEDIUM/HIGH/CRITICAL mappings via appsettings.
    /// </summary>
    public Dictionary<string, ThreatLevelPolicy> Policies { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Low"]      = new() { Decision = "Allow",      Action = "PassThrough",       CanAutoEngage = false },
        ["Medium"]   = new() { Decision = "Warn",        Action = "FlagForReview",     CanAutoEngage = false },
        ["High"]     = new() { Decision = "Challenge",   Action = "NotifyUser",        CanAutoEngage = false },
        ["Critical"] = new() { Decision = "AutoEngage",  Action = "EngagePersona",     CanAutoEngage = true  }
    };
}

public class ThreatLevelPolicy
{
    public string Decision      { get; set; } = "Allow";
    public string Action        { get; set; } = "PassThrough";
    public bool   CanAutoEngage { get; set; }
}
