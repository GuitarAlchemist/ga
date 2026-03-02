namespace GA.Domain.Core.Theory.Harmony.Progressions;

using Design.Persistence;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;

/// <summary>
///     Represents a labeled harmonic progression in the domain corpus.
/// </summary>
public sealed record ProgressionCorpusItem : DocumentBase
{
    public required string StyleLabel { get; init; } // "Jazz", "Rock", "Blues", etc.
    public IReadOnlyList<ChordVoicingSnapshot> Chords { get; init; } = [];
    public required string Source { get; init; }
}
