namespace GA.Business.Core.Fretboard.Biomechanics;

/// <summary>
/// Domain-driven physical and biomechanical constants.
/// </summary>
public static class BiomechanicsConstants
{
    // --- Instrument Geometry ---
    
    /// <summary>Standard Guitar Scale Length in Millimeters (25.5 inches).</summary>
    public const double StandardScaleLengthMm = 647.7;

    /// <summary>Number of frets in an octave.</summary>
    public const int SemitonesPerOctave = 12;

    // --- Player Ergonomics ---
    
    /// <summary>
    /// Comfortable hand span for an average adult (Medium size) in Millimeters.
    /// Derived from NASA STD-3000 anthropometry data.
    /// </summary>
    public const double ComfortableHandSpanMm = 215.0;

    /// <summary>
    /// Practical finger reach (approx 4-fret span at the nut) in relative scale units.
    /// (1 - 2^(-4/12)) approx 0.206
    /// </summary>
    public const double ComfortableReachScaleUnits = 0.21;

    /// <summary>Threshold fret for 'cramped' finger positions.</summary>
    public const int CrampedFretThreshold = 17;

    /// <summary>Threshold fret for 'high' register access difficulty.</summary>
    public const int HighRegisterThreshold = 12;
}
