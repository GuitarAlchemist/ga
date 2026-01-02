namespace GA.Business.Core.Fretboard.Shapes;

/// <summary>
/// Options for building shape graphs
/// </summary>
public record ShapeGraphBuildOptions
{
    /// <summary>
    /// Maximum fret to consider (default: 12)
    /// </summary>
    public int MaxFret { get; init; } = 12;

    /// <summary>
    /// Maximum fret span for a shape (default: 5)
    /// </summary>
    public int MaxSpan { get; init; } = 5;

    /// <summary>
    /// Minimum ergonomics score (0-1, default: 0.0)
    /// </summary>
    public double MinErgonomics { get; init; } = 0.0;

    /// <summary>
    /// Maximum number of shapes per pitch-class set (default: 20)
    /// </summary>
    public int MaxShapesPerSet { get; init; } = 20;

    /// <summary>
    /// Maximum harmonic distance for transitions (default: 5)
    /// </summary>
    public int MaxHarmonicDistance { get; init; } = 5;

    /// <summary>
    /// Maximum physical cost for transitions (default: 10.0)
    /// </summary>
    public double MaxPhysicalCost { get; init; } = 10.0;

    // --- OPTIC (Tymoczko) voice-leading integration for transitions ---

    /// <summary>
    /// Weight applied to OPTIC voice-leading cost when computing transition weighted score.
    /// Set to 0.0 to disable influence (default).
    /// </summary>
    public double VoiceLeadingWeight { get; init; } = 0.0;

    /// <summary>
    /// Voice-leading space: Octave equivalence (O)
    /// </summary>
    public bool VlOctaveEquivalence { get; init; } = true;

    /// <summary>
    /// Voice-leading space: Permutation equivalence (P)
    /// </summary>
    public bool VlPermutationEquivalence { get; init; } = true;

    /// <summary>
    /// Voice-leading space: Transposition equivalence (T)
    /// </summary>
    public bool VlTranspositionEquivalence { get; init; } = true;

    /// <summary>
    /// Voice-leading space: Inversion equivalence (I)
    /// </summary>
    public bool VlInversionEquivalence { get; init; } = false;
}

