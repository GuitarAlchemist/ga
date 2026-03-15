namespace GA.Domain.Services.Fretboard.Analysis;

using Domain.Core.Instruments.Fretboard.Analysis;

/// <summary>
///     Factory for creating chord pattern equivalence collections.
/// </summary>
public static class ChordPatternEquivalenceFactory
{
    /// <summary>
    ///     Creates a standard guitar chord equivalence collection using
    ///     translation equivalence (same shape, different position = same pattern).
    /// </summary>
    public static ChordPatternEquivalences CreateGuitarChordEquivalences() => new();
}

/// <summary>
///     Provides translation-equivalence normalization for chord patterns.
///     Two voicings with the same relative finger shape are considered equivalent
///     regardless of their position on the neck.
/// </summary>
public sealed class ChordPatternEquivalences
{
    /// <summary>
    ///     Gets the prime (canonical) form of a pattern.
    ///     For translation equivalence, the PatternId is already normalized
    ///     (relative fret offsets with base fret subtracted), so it IS the prime form.
    /// </summary>
    public PatternId? GetPrimeForm(PatternId patternId) => patternId;
}
