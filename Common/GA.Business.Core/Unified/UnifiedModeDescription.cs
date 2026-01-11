namespace GA.Business.Core.Unified;

/// <summary>
///     Unified human-facing description.
/// </summary>
public sealed class UnifiedModeDescription
{
    public required string PrimaryName { get; init; }
    public required string Summary { get; init; }
    public string? Symmetry { get; init; }
    public string? IntervalClassVector { get; init; }
    public int Cardinality { get; init; }
    public int RotationIndex { get; init; }
    public string? FamilyInfo { get; init; }
    public string? PrimeForm { get; init; }
    public string? ForteNumber { get; init; }
    
    /// <summary>
    /// Comparative brightness index (sum of intervals). Higher is "brighter".
    /// </summary>
    public int Brightness { get; init; }

    /// <summary>
    /// Spectral centroid (timbral "color" or dissonance proxy) from DFT analysis.
    /// </summary>
    public double SpectralCentroid { get; init; }

    /// <summary>
    /// The Messiaen Mode of Limited Transposition number (1-7), if applicable.
    /// </summary>
    public int? MessiaenModeIndex { get; init; }
}
