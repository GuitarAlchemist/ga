namespace GA.Business.Core.Fretboard.Shapes.Applications;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DynamicalSystems;
using Spectral;

/// <summary>
/// Comprehensive harmonic analysis engine combining all techniques
/// </summary>
public class HarmonicAnalysisEngine
{
    private readonly SpectralGraphAnalyzer _spectralAnalyzer = new();
    private readonly HarmonicDynamics _dynamicsAnalyzer = new();
    private readonly ProgressionAnalyzer _progressionAnalyzer = new();
    private readonly ProgressionOptimizer _progressionOptimizer = new();

    /// <summary>
    /// Perform comprehensive analysis of a shape graph
    /// </summary>
    public async Task<HarmonicAnalysisReport> AnalyzeAsync(
        ShapeGraph graph,
        HarmonicAnalysisOptions? options = null)
    {
        options ??= new();

        // Run analyses in parallel
        var spectralTask = Task.Run(() =>
            options.IncludeSpectralAnalysis ? _spectralAnalyzer.Analyze(graph) : null);

        var dynamicsTask = Task.Run(() =>
            options.IncludeDynamicalAnalysis ? _dynamicsAnalyzer.Analyze(graph) : null);

        var clusteringTask = Task.Run(() =>
            options.ClusterCount > 0 ? _spectralAnalyzer.Cluster(graph, options.ClusterCount) : []);

        await Task.WhenAll(spectralTask, dynamicsTask, clusteringTask);

        return new()
        {
            Spectral = await spectralTask,
            Dynamics = await dynamicsTask,
            ChordFamilies = await clusteringTask,
            Topology = options.IncludeTopologicalAnalysis ? ComputeTopology(graph) : null
        };
    }

    /// <summary>
    /// Analyze a specific progression
    /// </summary>
    public ProgressionInfo AnalyzeProgression(ShapeGraph graph, List<FretboardShape> progression)
    {
        return _progressionAnalyzer.AnalyzeProgression(graph, progression);
    }

    /// <summary>
    /// Compare two progressions
    /// </summary>
    public ProgressionComparison CompareProgressions(
        ShapeGraph graph,
        List<FretboardShape> progression1,
        List<FretboardShape> progression2)
    {
        var info1 = _progressionAnalyzer.AnalyzeProgression(graph, progression1);
        var info2 = _progressionAnalyzer.AnalyzeProgression(graph, progression2);

        var similarity = 1.0 - Math.Abs(info1.Entropy - info2.Entropy) / Math.Max(info1.Entropy, info2.Entropy);
        var wassersteinDistance = Math.Abs(info1.Complexity - info2.Complexity);

        return new()
        {
            Similarity = similarity,
            WassersteinDistance = wassersteinDistance,
            Info1 = info1,
            Info2 = info2
        };
    }

    /// <summary>
    /// Find optimal practice path
    /// </summary>
    public List<FretboardShape> FindOptimalPracticePath(
        ShapeGraph graph,
        FretboardShape startShape,
        int pathLength,
        PracticeGoal goal)
    {
        var strategy = goal switch
        {
            PracticeGoal.MaximizeInformationGain => OptimizationStrategy.MaximizeVariety,
            PracticeGoal.MinimizePhysicalCost => OptimizationStrategy.MinimizeVoiceLeading,
            _ => OptimizationStrategy.BalancedPractice
        };

        var result = _progressionOptimizer.GeneratePracticeProgression(graph, new()
        {
            TargetLength = pathLength,
            Strategy = strategy
        });

        return result.Shapes;
    }

    private TopologyInfo? ComputeTopology(ShapeGraph graph)
    {
        // Simplified topology analysis
        return new()
        {
            ConnectedComponents = 1,
            EulerCharacteristic = graph.ShapeCount - graph.TransitionCount
        };
    }
}

/// <summary>
/// Options for harmonic analysis
/// </summary>
public record HarmonicAnalysisOptions
{
    public bool IncludeSpectralAnalysis { get; init; } = true;
    public bool IncludeDynamicalAnalysis { get; init; } = true;
    public bool IncludeTopologicalAnalysis { get; init; } = false;
    public int ClusterCount { get; init; } = 5;
}

/// <summary>
/// Comprehensive harmonic analysis report
/// </summary>
public record HarmonicAnalysisReport
{
    public SpectralMetrics? Spectral { get; init; }
    public DynamicalSystemInfo? Dynamics { get; init; }
    public List<ChordFamily> ChordFamilies { get; init; } = [];
    public TopologyInfo? Topology { get; init; }
}

/// <summary>
/// Topology information
/// </summary>
public record TopologyInfo
{
    public int ConnectedComponents { get; init; }
    public int EulerCharacteristic { get; init; }
}

/// <summary>
/// Comparison of two progressions
/// </summary>
public record ProgressionComparison
{
    public double Similarity { get; init; }
    public double WassersteinDistance { get; init; }
    public ProgressionInfo Info1 { get; init; } = null!;
    public ProgressionInfo Info2 { get; init; } = null!;
}

/// <summary>
/// Practice goal for optimization
/// </summary>
public enum PracticeGoal
{
    MaximizeInformationGain,
    MinimizePhysicalCost,
    Balanced
}
