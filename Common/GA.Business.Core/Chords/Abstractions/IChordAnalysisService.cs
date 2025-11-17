namespace GA.Business.Core.Chords.Abstractions;

using Atonal;

/// <summary>
/// Abstraction for chord analysis strategies (tonal, atonal, etc.).
/// Implementations decide if they can analyze a given chord template and
/// provide a suggested name and description when applicable.
/// </summary>
public interface IChordAnalysisService
{
    /// <summary>
    /// Returns true if this strategy should be used for the provided template.
    /// </summary>
    bool CanAnalyze(ChordTemplate template);

    /// <summary>
    /// Returns a suggested human-readable name/symbol for the chord.
    /// </summary>
    string GetSuggestedName(ChordTemplate template, PitchClass root);

    /// <summary>
    /// Returns a textual description with theory details.
    /// </summary>
    string GetDescription(ChordTemplate template, PitchClass root);
}
