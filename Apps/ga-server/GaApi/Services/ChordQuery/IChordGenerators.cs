namespace GaApi.Services.ChordQuery;

using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes;
using Models;

/// <summary>
///     Internal interface for chord generators
///     Used by ChordQueryExecutor to invoke specific generators
/// </summary>
public interface IChordGenerators
{
    /// <summary>
    ///     Generates diatonic chords for a key
    /// </summary>
    IEnumerable<ChordInContext> GenerateDiatonicChords(Key key, ScaleMode scale, ChordFilters filters);

    /// <summary>
    ///     Generates borrowed chords (modal interchange) for a key
    /// </summary>
    IEnumerable<ChordInContext> GenerateBorrowedChords(Key key, ChordFilters filters);

    /// <summary>
    ///     Generates secondary dominants for a key
    /// </summary>
    IEnumerable<ChordInContext> GenerateSecondaryDominants(Key key, ChordFilters filters);

    /// <summary>
    ///     Generates secondary ii-V progressions for a key
    /// </summary>
    IEnumerable<ChordInContext> GenerateSecondaryTwoFive(Key key, ChordFilters filters);

    /// <summary>
    ///     Generates modal chords for a scale/mode
    /// </summary>
    IEnumerable<ChordInContext> GenerateModalChords(ScaleMode mode, ChordFilters filters);
}
