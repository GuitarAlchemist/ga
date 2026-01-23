namespace GA.Domain.Core.Instruments.Shapes.Applications;

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
            options.ClusterCount > 0 ? _spectralAnalyzer.Cluster(graph, options.ClusterCount) : new List<ChordFamily>());

        var centralShapesTask = Task.Run(() =>
            options.IncludeSpectralAnalysis ? _spectralAnalyzer.FindCentralShapes(graph, options.TopCentralShapes) : new List<(string ShapeId, double Score)>());

        var bottlenecksTask = Task.Run(() =>
            options.IncludeSpectralAnalysis ? _spectralAnalyzer.FindBottlenecks(graph, options.TopBottlenecks) : new List<(string ShapeId, double Bottleneck)>());

        await Task.WhenAll(spectralTask, dynamicsTask, clusteringTask, centralShapesTask, bottlenecksTask);

        return new()
        {
            Spectral = await spectralTask,
            Dynamics = await dynamicsTask,
            ChordFamilies = await clusteringTask,
            CentralShapes = await centralShapesTask,
            Bottlenecks = await bottlenecksTask,
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