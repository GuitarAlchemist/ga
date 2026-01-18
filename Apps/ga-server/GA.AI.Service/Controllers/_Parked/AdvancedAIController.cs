namespace GA.AI.Service.Controllers;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Shapes;
using GA.Business.Core.Notes;
using GA.AI.Service.Models;
using GA.AI.Service.Services;

using IShapeGraphBuilder = GA.Business.Core.Fretboard.Shapes.IShapeGraphBuilder;

/// <summary>
///     API controller for Advanced AI Features
///     Includes style learning and pattern recognition
/// </summary>
[ApiController]
[Route("api/advanced-ai")]
[EnableRateLimiting("fixed")]
public class AdvancedAiController(
    IShapeGraphBuilder graphBuilder,
    ILogger<AdvancedAiController> logger,
    ILoggerFactory loggerFactory)
    : ControllerBase
{
    // In-memory storage for player sessions (in production, use Redis or database)
    private static readonly Dictionary<string, StyleLearningSystem> _styleSystems = new();
    private static readonly Dictionary<string, PatternRecognitionSystem> _patternSystems = new();

    /// <summary>
    ///     Learn player's style from progression
    /// </summary>
    /// <param name="request">Style learning request</param>
    /// <returns>Updated style profile</returns>
    [HttpPost("learn-style")]
    [ProducesResponseType(typeof(ApiResponse<PlayerStyleProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> LearnStyle([FromBody] LearnStyleRequest request)
    {
        try
        {
            logger.LogInformation("🎨 Learning style for player {PlayerId}", request.PlayerId);

            // Get or create systems
            var styleSystem = GetOrCreateStyleSystem(request.PlayerId);
            var patternSystem = GetOrCreatePatternSystem(request.PlayerId);

            // Parse pitch class sets
            var pitchClassSets = request.PitchClassSets
                .Select(pcs => PitchClassSet.Parse(pcs))
                .ToList();

            // Build shape graph
            var tuning = ParseTuning(request.Tuning);
            var graph = await graphBuilder.BuildGraphAsync(tuning, pitchClassSets, new ShapeGraphBuildOptions());

            // Learn from progression
            styleSystem.LearnFromProgression(graph, request.Progression);
            patternSystem.LearnPatterns(request.Progression);

            // Get updated profile
            var profile = styleSystem.GetStyleProfile();
            var dto = MapStyleProfileToDto(profile);

            logger.LogInformation("✅ Style learned: complexity={Complexity:F2}", profile.PreferredComplexity);
            return Ok(ApiResponse<PlayerStyleProfileDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error learning style");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Generate progression matching player's style
    /// </summary>
    /// <param name="request">Style-matched generation request</param>
    /// <returns>Style-matched progression</returns>
    [HttpPost("generate-style-matched")]
    [ProducesResponseType(typeof(ApiResponse<StyleMatchedProgressionDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GenerateStyleMatched([FromBody] GenerateStyleMatchedRequest request)
    {
        try
        {
            logger.LogInformation("🎨 Generating style-matched progression for player {PlayerId}", request.PlayerId);

            // Get style system
            var styleSystem = GetOrCreateStyleSystem(request.PlayerId);

            // Parse pitch class sets
            var pitchClassSets = request.PitchClassSets
                .Select(pcs => PitchClassSet.Parse(pcs))
                .ToList();

            // Build shape graph
            var tuning = ParseTuning(request.Tuning);
            var graph = await graphBuilder.BuildGraphAsync(tuning, pitchClassSets, new ShapeGraphBuildOptions());

            // Generate progression
            var progression = styleSystem.GenerateStyleMatchedProgression(graph, pitchClassSets.FirstOrDefault()!, request.TargetLength ?? 8);

            var dto = new StyleMatchedProgressionDto
            {
                ShapeIds = progression,
                MatchScore = 0.85 // Placeholder - would calculate actual match score
            };

            logger.LogInformation("✅ Generated {Length} shapes matching style", progression.Count);
            return Ok(ApiResponse<StyleMatchedProgressionDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating style-matched progression");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get player's style profile
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <returns>Style profile</returns>
    [HttpGet("style-profile/{playerId}")]
    [ProducesResponseType(typeof(ApiResponse<PlayerStyleProfileDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult GetStyleProfile(string playerId)
    {
        try
        {
            var styleSystem = GetOrCreateStyleSystem(playerId);
            var profile = styleSystem.GetStyleProfile();
            var dto = MapStyleProfileToDto(profile);

            return Ok(ApiResponse<PlayerStyleProfileDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting style profile");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get recognized patterns
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <param name="topK">Number of top patterns to return</param>
    /// <returns>Recognized patterns</returns>
    [HttpGet("patterns/{playerId}")]
    [ProducesResponseType(typeof(ApiResponse<List<RecognizedPatternDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult GetPatterns(string playerId, [FromQuery] int topK = 10)
    {
        try
        {
            var patternSystem = GetOrCreatePatternSystem(playerId);
            var patterns = patternSystem.GetTopPatterns(topK);

            var dtos = patterns.Select(p => new RecognizedPatternDto
            {
                Pattern = p.Pattern,
                Frequency = p.Frequency,
                Probability = p.Probability
            }).ToList();

            return Ok(ApiResponse<List<RecognizedPatternDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting patterns");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Predict next shapes based on learned patterns
    /// </summary>
    /// <param name="request">Prediction request</param>
    /// <returns>Shape predictions</returns>
    [HttpPost("predict-next")]
    [ProducesResponseType(typeof(ApiResponse<List<ShapePredictionDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult PredictNext([FromBody] PredictNextRequest request)
    {
        try
        {
            var patternSystem = GetOrCreatePatternSystem(request.PlayerId);
            var predictions = patternSystem.PredictNextShapes(request.CurrentShape, request.TopK ?? 5);

            var dtos = predictions.Select(p => new ShapePredictionDto
            {
                ShapeId = p.ShapeId,
                Probability = p.Probability,
                Confidence = p.Confidence
            }).ToList();

            return Ok(ApiResponse<List<ShapePredictionDto>>.Ok(dtos));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error predicting next shapes");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get transition matrix for visualization
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <returns>Transition matrix</returns>
    [HttpGet("transition-matrix/{playerId}")]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, Dictionary<string, double>>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult GetTransitionMatrix(string playerId)
    {
        try
        {
            var patternSystem = GetOrCreatePatternSystem(playerId);
            var matrix = patternSystem.GetTransitionMatrix();

            return Ok(ApiResponse<Dictionary<string, Dictionary<string, double>>>.Ok(matrix));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting transition matrix");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Recommend progressions similar to player's favorites
    /// </summary>
    /// <param name="request">Recommendation request</param>
    /// <returns>Recommended progressions</returns>
    [HttpPost("recommend-progressions")]
    [ProducesResponseType(typeof(ApiResponse<List<List<string>>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> RecommendProgressions([FromBody] RecommendProgressionsRequest request)
    {
        try
        {
            var styleSystem = GetOrCreateStyleSystem(request.PlayerId);

            // Parse pitch class sets
            var pitchClassSets = request.PitchClassSets
                .Select(pcs => PitchClassSet.Parse(pcs))
                .ToList();

            // Build shape graph
            var tuning = ParseTuning(request.Tuning);
            var graph = await graphBuilder.BuildGraphAsync(tuning, pitchClassSets, new ShapeGraphBuildOptions());

            // Get recommendations
            var recommendations = styleSystem.RecommendSimilarProgressions(graph, request.TopK ?? 5);

            return Ok(ApiResponse<List<List<string>>>.Ok(recommendations));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recommending progressions");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    // ==================
    // Private Methods
    // ==================

    private StyleLearningSystem GetOrCreateStyleSystem(string playerId)
    {
        if (!_styleSystems.TryGetValue(playerId, out var system))
        {
            system = new StyleLearningSystem(loggerFactory);
            _styleSystems[playerId] = system;
        }

        return system;
    }

    private PatternRecognitionSystem GetOrCreatePatternSystem(string playerId)
    {
        if (!_patternSystems.TryGetValue(playerId, out var system))
        {
            system = new PatternRecognitionSystem(loggerFactory.CreateLogger<PatternRecognitionSystem>());
            _patternSystems[playerId] = system;
        }

        return system;
    }

    private PlayerStyleProfileDto MapStyleProfileToDto(PlayerStyleProfile profile)
    {
        return new PlayerStyleProfileDto
        {
            PreferredComplexity = profile.PreferredComplexity,
            ExplorationRate = profile.ExplorationRate,
            TopChordFamilies = profile.TopChordFamilies,
            FavoriteProgressionCount = profile.FavoriteProgressionCount,
            TotalProgressionsAnalyzed = profile.TotalProgressionsAnalyzed
        };
    }

    private Tuning ParseTuning(string? tuningStr)
    {
        if (string.IsNullOrWhiteSpace(tuningStr))
        {
            return Tuning.Default; // Standard guitar tuning
        }

        // Parse as pitch collection string (e.g., "E2 A2 D3 G3 B3 E4")
        return new Tuning(PitchCollection.Parse(tuningStr));
    }
}

// ==================
// Request/Response DTOs
// ==================

public class LearnStyleRequest
{
    public required string PlayerId { get; init; }
    public required List<string> PitchClassSets { get; init; }
    public required List<string> Progression { get; init; }
    public string? Tuning { get; init; }
}

public class GenerateStyleMatchedRequest
{
    public required string PlayerId { get; init; }
    public required List<string> PitchClassSets { get; init; }
    public string? Tuning { get; init; }
    public int? TargetLength { get; init; }
}

public class PredictNextRequest
{
    public required string PlayerId { get; init; }
    public required string CurrentShape { get; init; }
    public int? TopK { get; init; }
}

public class RecommendProgressionsRequest
{
    public required string PlayerId { get; init; }
    public required List<string> PitchClassSets { get; init; }
    public string? Tuning { get; init; }
    public int? TopK { get; init; }
}

public class PlayerStyleProfileDto
{
    public required double PreferredComplexity { get; init; }
    public required double ExplorationRate { get; init; }
    public required Dictionary<string, int> TopChordFamilies { get; init; }
    public required int FavoriteProgressionCount { get; init; }
    public required int TotalProgressionsAnalyzed { get; init; }
}

public class StyleMatchedProgressionDto
{
    public required List<string> ShapeIds { get; init; }
    public required double MatchScore { get; init; }
}

public class RecognizedPatternDto
{
    public required string Pattern { get; init; }
    public required int Frequency { get; init; }
    public required double Probability { get; init; }
}

public class ShapePredictionDto
{
    public required string ShapeId { get; init; }
    public required double Probability { get; init; }
    public required double Confidence { get; init; }
}
