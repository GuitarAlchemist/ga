namespace GA.Analytics.Service.Constants;

/// <summary>
/// Cache keys for analytics service
/// </summary>
public static class CacheKeys
{
    public const string ShapeGraph = "shape_graph";
    public const string HarmonicAnalysis = "harmonic_analysis";
    public const string ProgressionOptimization = "progression_optimization";
    public const string SpectralAnalysis = "spectral_analysis";
    public const string ValidationResults = "validation_results";
    public const string ICVResults = "icv_results";
    public const string AnalyticsMetrics = "analytics_metrics";
    public const string FretboardShapes = "fretboard_shapes";
    public const string Durations = "durations";
    public const string HeatMap = "heat_map";
    
    public static string GetShapeGraphKey(string parameters) => $"{ShapeGraph}:{parameters}";
    public static string GetHarmonicAnalysisKey(string shapeId) => $"{HarmonicAnalysis}:{shapeId}";
    public static string GetProgressionKey(string progressionId) => $"{ProgressionOptimization}:{progressionId}";
    public static string GetSpectralKey(string agentId) => $"{SpectralAnalysis}:{agentId}";
    public static string GetHeatMapKey(string parameters) => $"{HeatMap}:{parameters}";
    public static string GetFretboardShapesKey(string parameters) => $"{FretboardShapes}:{parameters}";
}

/// <summary>
/// Optimization strategies
/// </summary>
public static class AnalyticsOptimizationStrategy
{
    public const string Genetic = "genetic";
    public const string SimulatedAnnealing = "simulated_annealing";
    public const string ParticleSwarm = "particle_swarm";
    public const string GradientDescent = "gradient_descent";
    public const string RandomSearch = "random_search";
    public const string Hybrid = "hybrid";
    public const string Balanced = "balanced";
    public const string MinimizeVoiceLeading = "minimize_voice_leading";
    public const string MaximizeInformationGain = "maximize_information_gain";
    public const string ExploreFamilies = "explore_families";
    public const string FollowAttractors = "follow_attractors";
}
