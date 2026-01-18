namespace GA.Business.Core.Chords;

/// <summary>
/// Represents the fundamental quality of a musical chord.
/// Consolidates basic triads and common 7th chord qualities.
/// </summary>
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
