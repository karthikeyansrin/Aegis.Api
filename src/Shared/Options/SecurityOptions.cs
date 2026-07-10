namespace Aegis.Shared.Options;

public class SecurityOptions
{
    public const string SectionName = "Security";
    public string ApiKey { get; set; } = "dev-secret-key";
}