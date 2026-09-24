namespace GA.Domain.Core.Theory.Harmony;

/// <summary>
/// Represents the fundamental quality of a musical chord.
/// Consolidates basic triads and common 7th chord qualities.
/// </summary>
/// <remarks>
///     <see href="https://en.wikipedia.org/wiki/Chord_(music)#Chord_quality" />
/// </remarks>
public enum ChordQuality
{
    Other,
    Major,
    Minor,
    Dominant,       // Dominant 7th
    Major7,
    Minor7,
    Diminished,
    Diminished7,    // Fully diminished
    HalfDiminished, // m7b5
    Augmented,
    Suspended
}

/// <summary>
/// Groups specific seventh-chord species by the triad quality that determines
/// functional harmony and Roman-numeral casing.
/// </summary>
public static class ChordQualityExtensions
{
    public static ChordQuality ToTriadFamily(this ChordQuality quality) => quality switch
    {
        ChordQuality.Major7 or ChordQuality.Dominant => ChordQuality.Major,
        ChordQuality.Minor7 => ChordQuality.Minor,
        ChordQuality.Diminished7 or ChordQuality.HalfDiminished => ChordQuality.Diminished,
        _ => quality
    };
}
