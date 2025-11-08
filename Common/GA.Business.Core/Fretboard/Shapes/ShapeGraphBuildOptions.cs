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
}

