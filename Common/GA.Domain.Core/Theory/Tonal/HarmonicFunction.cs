namespace GA.Domain.Core.Theory.Tonal;

/// <summary>
///     Represents the functional role of a chord within a key or mode.
///     Standard 7-degree tonal functions (<see href="https://en.wikipedia.org/wiki/Function_(music)" />).
/// </summary>
public enum HarmonicFunction
{
    Unknown,
    Tonic, // I
    Supertonic, // ii
    Mediant, // iii
    Subdominant, // IV
    Dominant, // V
    Submediant, // vi
    LeadingTone, // vii°, one semitone below the tonic
    Subtonic // VII, two semitones below the tonic
}

/// <summary>
///     Extension methods for HarmonicFunction
/// </summary>
public static class HarmonicFunctionExtensions
{
    public static HarmonicFunction FromDegree(int degree) => degree switch
    {
        1 => HarmonicFunction.Tonic,
        2 => HarmonicFunction.Supertonic,
        3 => HarmonicFunction.Mediant,
        4 => HarmonicFunction.Subdominant,
        5 => HarmonicFunction.Dominant,
        6 => HarmonicFunction.Submediant,
        7 => HarmonicFunction.LeadingTone,
        _ => HarmonicFunction.Unknown
    };

    /// <summary>
    ///     Resolves a scale degree when its chromatic distance below the tonic is known.
    /// </summary>
    /// <remarks>
    ///     Degree seven is a leading tone at one semitone below the tonic and a subtonic at two.
    ///     Other distances do not describe a diatonic seventh degree.
    /// </remarks>
    public static HarmonicFunction FromDegree(int degree, int semitonesBelowTonic) => degree == 7
        ? semitonesBelowTonic switch
        {
            1 => HarmonicFunction.LeadingTone,
            2 => HarmonicFunction.Subtonic,
            _ => HarmonicFunction.Unknown
        }
        : FromDegree(degree);
}
