namespace Aegis.Shared.Options;

public class OpenAIOptions
{
    public const string SectionName = "OpenAI";
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/";
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The provider name to use by default. Supported values: "groq", "openai".
    /// Defaults to "groq".
    /// </summary>
    public string DefaultProvider { get; set; } = "groq";
}