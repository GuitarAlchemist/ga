namespace GA.Business.Core.Tonal;

/// <summary>
/// Represents the functional role of a chord within a key or mode.
/// Standard 7-degree tonal functions.
/// </summary>
public enum HarmonicFunction
{
    Unknown,
    Tonic,          // I
    Supertonic,     // ii
    Mediant,        // iii
    Subdominant,    // IV
    Dominant,       // V
    Submediant,     // vi
    LeadingTone     // vii°
}
