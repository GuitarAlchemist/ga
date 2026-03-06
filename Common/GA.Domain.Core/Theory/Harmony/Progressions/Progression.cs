namespace GA.Domain.Core.Theory.Harmony.Progressions;

/// <summary>
///     Represents a named sequence of chords/voicings.
///     Used for playback, analysis, and educational examples
///     (<see href="https://en.wikipedia.org/wiki/Chord_progression" />).
/// </summary>
public sealed record Progression
{
    /// <summary>
    ///     Unique Identifier.
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();

    /// <summary>
    ///     Display name (e.g., "ii-V-I in C Major").
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    ///     Description or theory notes.
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    ///     The sequence of chords.
    /// </summary>
    public IReadOnlyList<ProgressionStep> Steps { get; init; } = [];

    /// <summary>
    ///     Tags for categorization (e.g., "jazz", "beginner", "cadence").
    /// </summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    public Progression WithStep(ProgressionStep step) => this with { Steps = [.. Steps, step] };

    public Progression WithTag(string tag) =>
        string.IsNullOrWhiteSpace(tag)
            ? this
            : this with { Tags = [.. Tags, tag] };
}
