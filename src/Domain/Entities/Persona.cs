namespace Aegis.Domain.Entities;

public class Persona
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tone { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string SuspicionLevel { get; set; } = string.Empty;
    public string Goals { get; set; } = string.Empty;
}