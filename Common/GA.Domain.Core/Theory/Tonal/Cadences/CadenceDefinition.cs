namespace GA.Domain.Core.Theory.Tonal.Cadences;

/// <summary>
///     Defines a named cadence — a harmonic progression that creates a sense of resolution or punctuation
///     (<see href="https://en.wikipedia.org/wiki/Cadence" />).
/// </summary>
public sealed record CadenceDefinition
{
    public string Name { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> RomanNumerals { get; init; } = [];
    public string InKey { get; init; } = string.Empty;
    public IReadOnlyList<string> Chords { get; init; } = [];
    public string? Function { get; init; }
    public string? VoiceLeading { get; init; }
}
