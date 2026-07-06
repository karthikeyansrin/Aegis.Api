namespace Aegis.Shared.Options;

public class RateLimitOptions
{
    public const string SectionName = "RateLimit";
    public int MaxRequestsPerMinute { get; set; } = 100;
}