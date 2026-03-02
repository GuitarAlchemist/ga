namespace GA.Domain.Core.Theory.Tonal;

/// <summary>
///     Represents the functional role of a chord within a key or mode.
///     Standard 7-degree tonal functions.
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
    LeadingTone // vii°
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
}
