namespace Aegis.Shared.Options;

public class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 45;
}