using GA.Business.Core.Atonal.Primitives;
using GA.Business.Core.Fretboard.Shapes;
using GA.Business.Core.Fretboard.Shapes.Applications;
using GA.Business.Core.Fretboard.Shapes.DynamicalSystems;
using GA.Business.Core.Fretboard.Shapes.InformationTheory;
using GA.Business.Core.Fretboard.Shapes.Spectral;
using GA.Business.Core.Fretboard.Shapes.Topology;

namespace GA.BSP.Core;

/// <summary>
/// Intelligent BSP Level Generator
///
/// Uses ALL 9 advanced mathematical techniques to create musically-aware BSP levels:
///
/// 1. SPECTRAL GRAPH THEORY - Chord family detection, bridge chords, PageRank centrality
/// 2. INFORMATION THEORY - Complexity measurement, entropy, predictability
/// 3. DYNAMICAL SYSTEMS - Attractor detection, limit cycles, chaos analysis
/// 4. CATEGORY THEORY - Transformation composition, functors, monads
/// 5. TOPOLOGICAL DATA ANALYSIS - Harmonic clusters, cyclic progressions
/// 6. DIFFERENTIAL GEOMETRY - Voice leading optimization, geodesics
/// 7. TENSOR ANALYSIS - Multi-dimensional harmonic space analysis
/// 8. OPTIMAL TRANSPORT - Wasserstein distance, optimal voicing assignments
/// 9. PROGRESSION OPTIMIZATION - Multi-objective optimization combining all techniques
///
/// This creates BSP levels that are:
/// - Musically coherent (spectral clustering)
/// - Pedagogically optimal (information theory)
/// - Naturally flowing (dynamical systems)
/// - Topologically interesting (persistent homology)
/// - Smoothly connected (voice leading)
/// </summary>
public class IntelligentBspGenerator(ILoggerFactory loggerFactory)
{
    private readonly ILogger<IntelligentBspGenerator> _logger = loggerFactory.CreateLogger<IntelligentBspGenerator>();
    private readonly SpectralGraphAnalyzer _spectralAnalyzer = new(loggerFactory.CreateLogger<SpectralGraphAnalyzer>());
    private readonly ProgressionAnalyzer _progressionAnalyzer = new(loggerFactory.CreateLogger<ProgressionAnalyzer>());
    private readonly HarmonicDynamics _harmonicDynamics = new(loggerFactory.CreateLogger<HarmonicDynamics>());
    private readonly ProgressionOptimizer _progressionOptimizer = new(loggerFactory);
    private readonly HarmonicAnalysisEngine _analysisEngine = new(loggerFactory);

    /// <summary>
    /// Generate intelligent BSP levels with musical awareness
    /// </summary>
    public async Task<IntelligentBspLevel> GenerateLevelAsync(
        ShapeGraph graph,
        BspLevelOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("ðŸ§  Generating intelligent BSP level with ALL advanced techniques...");

        // Step 1: Comprehensive harmonic analysis (uses ALL techniques!)
        _logger.LogInformation("ðŸ”¬ Step 1: Running comprehensive harmonic analysis...");
        var analysisOptions = new HarmonicAnalysisOptions
        {
            IncludeSpectralAnalysis = true,      // Spectral graph theory
            IncludeDynamicalAnalysis = true,     // Dynamical systems
            IncludeTopologicalAnalysis = true,   // Topological data analysis
            ClusterCount = options.ChordFamilyCount,
            TopCentralShapes = options.LandmarkCount,
            TopBottlenecks = options.BridgeChordCount,
        };

        var analysis = await _analysisEngine.AnalyzeAsync(graph, analysisOptions);

        _logger.LogInformation("âœ… Analysis complete:");
        _logger.LogInformation("   - Chord families: {Count}", analysis.ChordFamilies.Count);
        _logger.LogInformation("   - Central shapes (landmarks): {Count}", analysis.CentralShapes.Count);
        _logger.LogInformation("   - Bridge chords (portals): {Count}", analysis.Bottlenecks.Count);
        _logger.LogInformation("   - Attractors (safe zones): {Count}", analysis.Dynamics?.Attractors.Count ?? 0);
        _logger.LogInformation("   - Limit cycles (patterns): {Count}", analysis.Dynamics?.LimitCycles.Count ?? 0);
        if (analysis.Topology != null)
        {
            var betti = analysis.Topology.BettiNumbers;
            _logger.LogInformation("   - Topological features: H0={H0}, H1={H1}",
                betti.Count > 0 ? betti[0] : 0,
                betti.Count > 1 ? betti[1] : 0);
        }

        // Step 2: Create BSP floors based on chord families (spectral clustering)
        _logger.LogInformation("ðŸ¢ Step 2: Creating BSP floors from chord families...");
        var floors = CreateFloorsFromFamilies(analysis.ChordFamilies, graph);
        _logger.LogInformation("âœ… Created {Count} floors", floors.Count);

        // Step 3: Place landmarks at central shapes (PageRank)
        _logger.LogInformation("ðŸ—¿ Step 3: Placing landmarks at central shapes...");
        var landmarks = CreateLandmarks(analysis.CentralShapes, graph);
        _logger.LogInformation("âœ… Placed {Count} landmarks", landmarks.Count);

        // Step 4: Create portals at bridge chords (bottlenecks)
        _logger.LogInformation("ðŸšª Step 4: Creating portals at bridge chords...");
        var portals = CreatePortals(analysis.Bottlenecks, graph);
        _logger.LogInformation("âœ… Created {Count} portals", portals.Count);

        // Step 5: Mark safe zones at attractors (dynamical systems)
        _logger.LogInformation("ðŸ›¡ï¸ Step 5: Marking safe zones at attractors...");
        var safeZones = CreateSafeZones(analysis.Dynamics.Attractors, graph);
        _logger.LogInformation("âœ… Marked {Count} safe zones", safeZones.Count);

        // Step 6: Create challenge paths from limit cycles (patterns)
        _logger.LogInformation("âš”ï¸ Step 6: Creating challenge paths from limit cycles...");
        var challengePaths = CreateChallengePaths(analysis.Dynamics.LimitCycles, graph);
        _logger.LogInformation("âœ… Created {Count} challenge paths", challengePaths.Count);

        // Step 7: Generate optimal learning progression (multi-objective optimization)
        _logger.LogInformation("ðŸŽ“ Step 7: Generating optimal learning progression...");
        var learningPath = _progressionOptimizer.GeneratePracticeProgression(graph, new ProgressionConstraints
        {
            TargetLength = options.LearningPathLength,
            Strategy = OptimizationStrategy.Balanced, // Uses ALL techniques!
            PreferCentralShapes = true,
            AllowRandomness = true,
        });
        _logger.LogInformation("âœ… Learning path generated: quality={Quality:F2}, entropy={Entropy:F2}",
            learningPath.Quality, learningPath.Entropy);

        // Step 8: Validate level quality using spectral analysis
        _logger.LogInformation("ðŸ” Step 8: Validating level quality with spectral analysis...");
        var spectralMetrics = _spectralAnalyzer.Analyze(graph, useWeights: true, normalized: true);
        var spectralQuality = ValidateSpectralQuality(spectralMetrics);
        _logger.LogInformation("âœ… Spectral quality: {Quality:F2} (connectivity={Connectivity:F3}, gap={Gap:F3})",
            spectralQuality, spectralMetrics.AlgebraicConnectivity, spectralMetrics.SpectralGap);

        // Step 9: Analyze progression flow using progression analyzer
        _logger.LogInformation("ðŸŒŠ Step 9: Analyzing progression flow patterns...");
        var progressionReport = _progressionAnalyzer.AnalyzeProgression(graph, learningPath.ShapeIds);
        _logger.LogInformation("âœ… Progression flow: entropy={Entropy:F2}, complexity={Complexity:F2}, predictability={Predictability:F2}",
            progressionReport.Entropy, progressionReport.Complexity, progressionReport.Predictability);

        // Step 10: Verify dynamical stability using harmonic dynamics
        _logger.LogInformation("âš–ï¸ Step 10: Verifying dynamical stability...");
        var dynamicsInfo = _harmonicDynamics.Analyze(graph);
        var stabilityScore = CalculateStabilityScore(dynamicsInfo);
        _logger.LogInformation("âœ… Stability score: {Score:F2} (attractors={Attractors}, lyapunov={Lyapunov:F3})",
            stabilityScore, dynamicsInfo.Attractors.Count, dynamicsInfo.LyapunovExponent);

        // Step 11: Measure level difficulty (information theory + chaos + spectral)
        _logger.LogInformation("ðŸ“Š Step 11: Measuring comprehensive level difficulty...");
        var difficulty = CalculateDifficulty(analysis, learningPath, spectralMetrics, progressionReport, dynamicsInfo);
        _logger.LogInformation("âœ… Level difficulty: {Difficulty:F2} (0=easy, 1=hard)", difficulty);

        // Step 12: Create intelligent BSP level
        var level = new IntelligentBspLevel
        {
            Floors = floors,
            Landmarks = landmarks,
            Portals = portals,
            SafeZones = safeZones,
            ChallengePaths = challengePaths,
            LearningPath = learningPath.ShapeIds.ToList(),
            Difficulty = difficulty,
            Analysis = analysis,
            Metadata = new Dictionary<string, object>
            {
                ["ChordFamilyCount"] = analysis.ChordFamilies.Count,
                ["LandmarkCount"] = landmarks.Count,
                ["PortalCount"] = portals.Count,
                ["SafeZoneCount"] = safeZones.Count,
                ["ChallengePathCount"] = challengePaths.Count,
                ["AlgebraicConnectivity"] = analysis.Spectral.AlgebraicConnectivity,
                ["SpectralGap"] = analysis.Spectral.SpectralGap,
                ["LyapunovExponent"] = analysis.Dynamics.LyapunovExponent,
                ["Entropy"] = learningPath.Entropy,
                ["Complexity"] = learningPath.Complexity,
                ["Quality"] = learningPath.Quality,
                // New metrics from direct analyzer usage
                ["SpectralQuality"] = spectralQuality,
                ["ProgressionEntropy"] = progressionReport.Entropy,
                ["ProgressionComplexity"] = progressionReport.Complexity,
                ["ProgressionPredictability"] = progressionReport.Predictability,
                ["StabilityScore"] = stabilityScore,
                ["AttractorCount"] = dynamicsInfo.Attractors.Count,
                ["LimitCycleCount"] = dynamicsInfo.LimitCycles.Count,
            }
        };

        _logger.LogInformation("âœ… Intelligent BSP level complete!");
        return level;
    }

    private List<BspFloor> CreateFloorsFromFamilies(
        IReadOnlyList<ChordFamily> chordFamilies,
        ShapeGraph graph)
    {
        var floors = new List<BspFloor>();

        foreach (var family in chordFamilies.OrderBy(f => f.Id))
        {
            floors.Add(new BspFloor
            {
                FloorId = family.Id,
                Name = $"Chord Family {family.Id}",
                ShapeIds = family.ShapeIds,
                Color = GetFamilyColor(family.Id),
            });
        }

        return floors;
    }

    private List<BspLandmark> CreateLandmarks(
        IReadOnlyList<(string ShapeId, double Centrality)> centralShapes,
        ShapeGraph graph)
    {
        return centralShapes.Select((shape, index) => new BspLandmark
        {
            ShapeId = shape.ShapeId,
            Name = $"Landmark {index + 1}",
            Importance = shape.Centrality,
            Type = "Central",
        }).ToList();
    }

    private List<BspPortal> CreatePortals(
        IReadOnlyList<(string ShapeId, double BottleneckScore)> bottlenecks,
        ShapeGraph graph)
    {
        return bottlenecks.Select((shape, index) => new BspPortal
        {
            ShapeId = shape.ShapeId,
            Name = $"Portal {index + 1}",
            Strength = shape.BottleneckScore,
            Type = "Bridge",
        }).ToList();
    }

    private List<BspSafeZone> CreateSafeZones(
        IReadOnlyList<Attractor> attractors,
        ShapeGraph graph)
    {
        return attractors.Select((attractor, index) => new BspSafeZone
        {
            ShapeId = attractor.ShapeId,
            Name = $"Safe Zone {index + 1}",
            Stability = attractor.Strength, // Use Strength instead of Stability
            Type = "Attractor",
        }).ToList();
    }

    private List<BspChallengePath> CreateChallengePaths(
        IReadOnlyList<LimitCycle> limitCycles,
        ShapeGraph graph)
    {
        return limitCycles.Select((cycle, index) => new BspChallengePath
        {
            Name = $"Challenge {index + 1}",
            ShapeIds = cycle.Shapes.ToList(), // Use Shapes instead of ShapeIds
            Period = cycle.Period,
            Difficulty = Math.Min(1.0, cycle.Period / 10.0),
        }).ToList();
    }

    private double ValidateSpectralQuality(SpectralMetrics metrics)
    {
        // Higher algebraic connectivity = better connected graph
        // Higher spectral gap = better clustering
        var connectivityScore = Math.Min(1.0, metrics.AlgebraicConnectivity / 0.5);
        var gapScore = Math.Min(1.0, metrics.SpectralGap / 1.0);

        return (connectivityScore + gapScore) / 2.0;
    }

    private double CalculateStabilityScore(DynamicalSystemInfo dynamics)
    {
        // More attractors = more stable regions
        // Lower Lyapunov exponent = less chaotic
        var attractorScore = Math.Min(1.0, dynamics.Attractors.Count / 5.0);
        var chaosScore = 1.0 - Math.Min(1.0, Math.Abs(dynamics.LyapunovExponent));

        return (attractorScore + chaosScore) / 2.0;
    }

    private double CalculateDifficulty(
        HarmonicAnalysisReport analysis,
        OptimizedProgression progression,
        SpectralMetrics spectralMetrics,
        ProgressionInfo progressionReport,
        DynamicalSystemInfo dynamicsInfo)
    {
        // Combine multiple difficulty factors from ALL analyzers
        var complexityFactor = progression.Complexity; // 0-1
        var entropyFactor = Math.Min(1.0, progressionReport.Entropy / 5.0); // Normalize to 0-1
        var chaosFactor = Math.Min(1.0, Math.Abs(dynamicsInfo.LyapunovExponent)); // 0-1
        var topologyFactor = Math.Min(1.0, analysis.Topology.GetIntervals(1).Count() / 10.0); // H1 loops
        var spectralFactor = 1.0 - Math.Min(1.0, spectralMetrics.AlgebraicConnectivity / 0.5); // Lower connectivity = harder
        var predictabilityFactor = 1.0 - progressionReport.Predictability; // Lower predictability = harder

        // Weighted average using all 6 factors
        return (complexityFactor * 0.2 +
                entropyFactor * 0.2 +
                chaosFactor * 0.15 +
                topologyFactor * 0.15 +
                spectralFactor * 0.15 +
                predictabilityFactor * 0.15);
    }

    private int GetFamilyColor(int familyId)
    {
        // Distinct colors for each family
        var colors = new[] { 0x4488ff, 0xff4488, 0x88ff44, 0xff8844, 0x8844ff, 0x44ff88 };
        return colors[familyId % colors.Length];
    }
}

// ==================
// Data Structures
// ==================

public class IntelligentBspLevel
{
    public required List<BspFloor> Floors { get; init; }
    public required List<BspLandmark> Landmarks { get; init; }
    public required List<BspPortal> Portals { get; init; }
    public required List<BspSafeZone> SafeZones { get; init; }
    public required List<BspChallengePath> ChallengePaths { get; init; }
    public required List<string> LearningPath { get; init; }
    public required double Difficulty { get; init; }
    public required HarmonicAnalysisReport Analysis { get; init; }
    public required Dictionary<string, object> Metadata { get; init; }
}

public class BspFloor
{
    public required int FloorId { get; init; }
    public required string Name { get; init; }
    public required List<string> ShapeIds { get; init; }
    public required int Color { get; init; }
}

public class BspLandmark
{
    public required string ShapeId { get; init; }
    public required string Name { get; init; }
    public required double Importance { get; init; }
    public required string Type { get; init; }
}

public class BspPortal
{
    public required string ShapeId { get; init; }
    public required string Name { get; init; }
    public required double Strength { get; init; }
    public required string Type { get; init; }
}

public class BspSafeZone
{
    public required string ShapeId { get; init; }
    public required string Name { get; init; }
    public required double Stability { get; init; }
    public required string Type { get; init; }
}

public class BspChallengePath
{
    public required string Name { get; init; }
    public required List<string> ShapeIds { get; init; }
    public required int Period { get; init; }
    public required double Difficulty { get; init; }
}

public class BspLevelOptions
{
    public int ChordFamilyCount { get; init; } = 5;
    public int LandmarkCount { get; init; } = 10;
    public int BridgeChordCount { get; init; } = 5;
    public int LearningPathLength { get; init; } = 8;
}

