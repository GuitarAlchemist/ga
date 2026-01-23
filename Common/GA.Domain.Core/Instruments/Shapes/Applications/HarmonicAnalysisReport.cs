namespace GA.Domain.Core.Instruments.Shapes.Applications;

using DynamicalSystems;
using Spectral;

/// <summary>
/// Comprehensive harmonic analysis report
/// </summary>
public record HarmonicAnalysisReport
{
    public SpectralMetrics? Spectral { get; init; }
    public DynamicalSystemInfo? Dynamics { get; init; }
    public List<ChordFamily> ChordFamilies { get; init; } = [];
    public List<(string ShapeId, double Centrality)> CentralShapes { get; init; } = [];
    public List<(string ShapeId, double Bottleneck)> Bottlenecks { get; init; } = [];
    public TopologyInfo? Topology { get; init; }
}