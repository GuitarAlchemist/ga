namespace GA.Domain.Core.Instruments.Shapes.Applications;

/// <summary>
/// Options for harmonic analysis
/// </summary>
public record HarmonicAnalysisOptions
{
    public bool IncludeSpectralAnalysis { get; init; } = true;
    public bool IncludeDynamicalAnalysis { get; init; } = true;
    public bool IncludeTopologicalAnalysis { get; init; } = false;
    public int ClusterCount { get; init; } = 5;
    public int TopCentralShapes { get; init; } = 10;
    public int TopBottlenecks { get; init; } = 5;
}