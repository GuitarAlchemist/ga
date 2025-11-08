namespace GuitarAlchemistChatbot.Services;

using System.Text.Json;

/// <summary>
///     HTTP client for communicating with GaApi backend
/// </summary>
public class GaApiClient(HttpClient httpClient, ILogger<GaApiClient> logger)
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    ///     Analyze a chord progression using information theory
    /// </summary>
    public async Task<ProgressionAnalysisResponse?> AnalyzeProgressionAsync(
        string[] chordNames,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Analyzing progression: {Chords}", string.Join(" -> ", chordNames));

            var request = new { ChordNames = chordNames };
            var response = await httpClient.PostAsJsonAsync(
                "/api/chord-progressions/analyze",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ProgressionAnalysisResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing progression");
            return null;
        }
    }

    /// <summary>
    ///     Generate an optimal practice path
    /// </summary>
    public async Task<OptimizedPracticePathResponse?> GeneratePracticePathAsync(
        int[] pitchClasses,
        string tuningId,
        int pathLength = 8,
        string strategy = "balanced",
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Generating practice path: {PitchClasses}, length={Length}, strategy={Strategy}",
                string.Join(",", pitchClasses),
                pathLength,
                strategy);

            var request = new
            {
                PitchClasses = pitchClasses,
                TuningId = tuningId,
                PathLength = pathLength,
                Strategy = strategy,
                PreferCentralShapes = true,
                MinErgonomics = 0.5
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/grothendieck/generate-practice-path",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<OptimizedPracticePathResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating practice path");
            return null;
        }
    }

    /// <summary>
    ///     Analyze a shape graph using comprehensive harmonic analysis
    /// </summary>
    public async Task<ShapeGraphAnalysisResponse?> AnalyzeShapeGraphAsync(
        int[] pitchClasses,
        string tuningId,
        int clusterCount = 5,
        int topCentralShapes = 10,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Analyzing shape graph: {PitchClasses}, tuning={Tuning}",
                string.Join(",", pitchClasses),
                tuningId);

            var request = new
            {
                PitchClasses = pitchClasses,
                TuningId = tuningId,
                ClusterCount = clusterCount,
                TopCentralShapes = topCentralShapes
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/grothendieck/analyze-shape-graph",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ShapeGraphAnalysisResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error analyzing shape graph");
            return null;
        }
    }

    /// <summary>
    ///     Generate a BSP dungeon
    /// </summary>
    public async Task<DungeonGenerationResponse?> GenerateDungeonAsync(
        int width = 80,
        int height = 60,
        int maxDepth = 4,
        int? seed = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("Generating BSP dungeon: {Width}x{Height}", width, height);

            var request = new
            {
                Width = width,
                Height = height,
                MaxDepth = maxDepth,
                Seed = seed
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/bsp-rooms/generate",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<DungeonGenerationResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating BSP dungeon");
            return null;
        }
    }

    /// <summary>
    ///     Generate an intelligent musical dungeon
    /// </summary>
    public async Task<IntelligentDungeonResponse?> GenerateIntelligentDungeonAsync(
        int[] pitchClasses,
        string tuning,
        int width = 100,
        int height = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation(
                "Generating intelligent dungeon: {PitchClasses}, tuning={Tuning}",
                string.Join(",", pitchClasses),
                tuning);

            var request = new
            {
                PitchClasses = pitchClasses,
                Tuning = tuning,
                Width = width,
                Height = height
            };

            var response = await httpClient.PostAsJsonAsync(
                "/api/intelligent-bsp/generate-level",
                request,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<IntelligentDungeonResponse>(
                _jsonOptions,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating intelligent dungeon");
            return null;
        }
    }
}

// Response DTOs (matching GaApi)
public record ProgressionAnalysisResponse(
    double Entropy,
    double Complexity,
    double Predictability,
    int UniqueShapes,
    int ShapeCount,
    List<NextShapeSuggestion> Suggestions);

public record NextShapeSuggestion(
    string ShapeId,
    double InformationGain,
    double Probability);

public record OptimizedPracticePathResponse(
    List<string> ShapeIds,
    List<FretboardShapeResponse> Shapes,
    double Entropy,
    double Complexity,
    double Predictability,
    double Diversity,
    double Quality);

public record FretboardShapeResponse(
    string Id,
    PositionResponse[] Positions,
    int MinFret,
    int MaxFret,
    int Span,
    double Diagness,
    double Ergonomics,
    int FingerCount,
    string[] Tags);

public record PositionResponse(
    int String,
    int Fret,
    bool IsMuted);

public record ShapeGraphAnalysisResponse(
    SpectralMetricsDto? Spectral,
    List<ChordFamilyDto> ChordFamilies,
    List<CentralShapeDto> CentralShapes,
    List<BottleneckDto> Bottlenecks,
    DynamicsDto? Dynamics,
    TopologyDto? Topology);

public record SpectralMetricsDto(
    double AlgebraicConnectivity,
    double SpectralGap,
    int ComponentCount,
    double AveragePathLength,
    int Diameter);

public record ChordFamilyDto(
    int ClusterId,
    List<string> ShapeIds,
    string Representative);

public record CentralShapeDto(
    string ShapeId,
    double Centrality);

public record BottleneckDto(
    string ShapeId,
    double BetweennessCentrality);

public record DynamicsDto(
    List<AttractorDto> Attractors,
    List<LimitCycleDto> LimitCycles,
    double LyapunovExponent,
    bool IsChaotic,
    bool IsStable);

public record AttractorDto(
    string ShapeId,
    double BasinSize,
    string Type);

public record LimitCycleDto(
    List<string> ShapeIds,
    int Period,
    double Stability);

public record TopologyDto(
    int BettiNumber0,
    int BettiNumber1,
    List<PersistentFeatureDto> Features);

public record PersistentFeatureDto(
    double Birth,
    double Death,
    double Persistence,
    int Dimension);

// BSP Dungeon DTOs
public record DungeonGenerationResponse(
    int Width,
    int Height,
    int? Seed,
    List<DungeonRoom> Rooms,
    List<DungeonCorridor> Corridors);

public record DungeonRoom(
    int X,
    int Y,
    int Width,
    int Height,
    int CenterX,
    int CenterY);

public record DungeonCorridor(
    int Width,
    List<DungeonPoint> Points);

public record DungeonPoint(
    int X,
    int Y);

public record IntelligentDungeonResponse(
    int Width,
    int Height,
    List<DungeonFloor> Floors,
    List<DungeonLandmark> Landmarks,
    List<DungeonPortal> Portals,
    List<DungeonSafeZone> SafeZones,
    List<DungeonChallengePath> ChallengePaths,
    OptimizedPracticePathResponse LearningPath);

public record DungeonFloor(
    int FloorNumber,
    int FamilyId,
    int ShapeCount,
    List<string> ShapeIds);

public record DungeonLandmark(
    string Name,
    string ShapeId,
    int X,
    int Y,
    double Centrality);

public record DungeonPortal(
    int FromFloor,
    int ToFloor,
    string ShapeId,
    double Betweenness);

public record DungeonSafeZone(
    string ShapeId,
    int X,
    int Y,
    double BasinSize);

public record DungeonChallengePath(
    List<string> ShapeIds,
    int Period,
    double Strength);
