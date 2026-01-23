namespace GA.Domain.Core.Theory.Tonal.Cadences;

public class CadenceDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> RomanNumerals { get; set; } = new();
    public string InKey { get; set; } = string.Empty;
    public List<string> Chords { get; set; } = new();
    public string? Function { get; set; }
    public string? VoiceLeading { get; set; }
}