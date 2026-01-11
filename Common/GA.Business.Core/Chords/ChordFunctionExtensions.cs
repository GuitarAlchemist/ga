namespace GA.Business.Core.Chords;

/// <summary>
/// Extensions for identifying the harmonic function of intervals.
/// </summary>
public static class ChordFunctionExtensions
{
    /// <summary>
    /// Determines the chord function from a semitone value (relative to root).
    /// </summary>
    public static ChordFunction FromSemitones(int semitones)
    {
        return semitones switch
        {
            2 or 14 => ChordFunction.Ninth,
            3 or 4 => ChordFunction.Third,
            5 or 17 => ChordFunction.Eleventh,
            6 or 7 or 8 => ChordFunction.Fifth, // Expanded to capture dim/aug 5ths as "Fifth" function contextually
            9 or 21 => ChordFunction.Thirteenth,
            10 or 11 => ChordFunction.Seventh,
            _ => ChordFunction.Root
        };
    }
}
