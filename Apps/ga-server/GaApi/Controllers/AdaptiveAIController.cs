namespace GaApi.Controllers;

using Actors.Messages;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Notes;
using Microsoft.AspNetCore.RateLimiting;
using Models;
using Services;
// using GA.Business.Core.Fretboard.Shapes.Applications // REMOVED - namespace does not exist;

/// <summary>
///     API controller for Adaptive AI Difficulty System
///     Uses information theory and dynamical systems to adapt to player performance
///     Now powered by Proto.Actor for isolated, thread-safe player sessions
/// </summary>
[ApiController]
[Route("api/adaptive-ai")]
[EnableRateLimiting("fixed")]
public class AdaptiveAiController(
    IShapeGraphBuilder graphBuilder,
    ActorSystemManager actorSystem,
    ILogger<AdaptiveAiController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Record player performance and update difficulty
    /// </summary>
    /// <param name="request">Performance record request</param>
    /// <returns>Updated player stats</returns>
    [HttpPost("record-performance")]
    [ProducesResponseType(typeof(ApiResponse<PlayerStatsDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> RecordPerformance([FromBody] RecordPerformanceRequest request)
    {
        try
        {
            logger.LogInformation("📊 Recording performance for player {PlayerId}", request.PlayerId);

            // Calculate performance metrics
            var accuracy = request.Success ? 1.0 : 0.0;
            var speed = 1000.0 / Math.Max(request.TimeMs, 1.0); // Inverse of time
            var consistency = 1.0 / Math.Max(request.Attempts, 1.0); // Inverse of attempts

            // Send update to player session actor
            var updateMsg = new UpdatePerformance(accuracy, speed, consistency, request.ShapeId);
            var response = await actorSystem.AskPlayerSession<DifficultyResponse>(
                request.PlayerId,
                updateMsg);

            // Get full session state
            var stateResponse = await actorSystem.AskPlayerSession<SessionStateResponse>(
                request.PlayerId,
                new GetSessionState());

            var dto = new PlayerStatsDto
            {
                TotalAttempts = stateResponse.TotalExercises,
                SuccessRate = request.Success ? 1.0 : 0.0, // Would track this properly in actor
                AverageTime = request.TimeMs,
                CurrentDifficulty = response.CurrentDifficulty,
                LearningRate = 0.1, // Would track this in actor
                CurrentAttractor = null
            };

            logger.LogInformation("✅ Performance recorded, difficulty={Difficulty:F2}", response.CurrentDifficulty);
            return Ok(ApiResponse<PlayerStatsDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error recording performance");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    // TODO: Refactor to use actor system - needs access to AdaptiveDifficultySystem.GenerateAdaptiveChallenge
    // This method requires direct access to the AdaptiveDifficultySystem instance
    // Options:
    // 1. Add a new message type to the actor for this operation
    // 2. Move this logic into the actor itself
    // 3. Expose the system instance through a message (breaks encapsulation)
    /*
    /// <summary>
    /// Generate adaptive challenge based on player performance
    /// </summary>
    /// <param name="request">Challenge generation request</param>
    /// <returns>Adaptive challenge progression</returns>
    [HttpPost("generate-challenge")]
    [ProducesResponseType(typeof(ApiResponse<AdaptiveChallengeDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GenerateChallenge([FromBody] GenerateChallengeRequest request)
    {
        try
        {
            logger.LogInformation("🎯 Generating adaptive challenge for player {PlayerId}", request.PlayerId);

            // Get player session
            var system = GetOrCreatePlayerSession(request.PlayerId);

            // Parse pitch class sets
            var pitchClassSets = request.PitchClassSets
                .Select(pcs => PitchClassSet.Parse(pcs))
                .ToList();

            // Build shape graph
            var tuning = ParseTuning(request.Tuning);
            var graph = await graphBuilder.BuildGraphAsync(tuning, pitchClassSets);

            // Generate adaptive challenge
            var progression = system.GenerateAdaptiveChallenge(graph, request.RecentProgression);

            // Convert to DTO
            var dto = new AdaptiveChallengeDto
            {
                ShapeIds = progression.ShapeIds.ToList(),
                Quality = progression.Quality,
                Entropy = progression.Entropy,
                Complexity = progression.Complexity,
                Predictability = progression.Predictability,
                Diversity = progression.Diversity,
                Strategy = progression.Strategy.ToString(),
                CurrentDifficulty = system.GetPlayerStats().CurrentDifficulty,
                LearningRate = system.GetPlayerStats().LearningRate,
            };

            logger.LogInformation("✅ Challenge generated: quality={Quality:F2}, difficulty={Difficulty:F2}",
                dto.Quality, dto.CurrentDifficulty);
            return Ok(ApiResponse<AdaptiveChallengeDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating adaptive challenge");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }
    */

    // TODO: Refactor to use actor system - needs access to AdaptiveDifficultySystem.SuggestNextShapes
    // This method requires direct access to the AdaptiveDifficultySystem instance
    // Options:
    // 1. Add a new message type to the actor for this operation
    // 2. Move this logic into the actor itself
    // 3. Expose the system instance through a message (breaks encapsulation)
    /*
    /// <summary>
    /// Get shape suggestions adapted to player skill
    /// </summary>
    /// <param name="request">Suggestion request</param>
    /// <returns>Adaptive shape suggestions</returns>
    [HttpPost("suggest-shapes")]
    [ProducesResponseType(typeof(ApiResponse<List<ShapeSuggestionDto>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> SuggestShapes([FromBody] SuggestShapesRequest request)
    {
        try
        {
            logger.LogInformation("💡 Suggesting shapes for player {PlayerId}", request.PlayerId);

            // TODO: Implement actor-based shape suggestions
            // This requires adding a SuggestShapes message to PlayerSessionActor
            logger.LogWarning("SuggestShapes not yet implemented with actor system");

            return StatusCode(501, ApiResponse<object>.Fail("Not implemented", "Shape suggestions need to be migrated to actor system"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error suggesting shapes");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }
    */

    /// <summary>
    ///     Get player statistics
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <returns>Player statistics</returns>
    [HttpGet("player-stats/{playerId}")]
    [ProducesResponseType(typeof(ApiResponse<PlayerStatsDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetPlayerStats(string playerId)
    {
        try
        {
            var stateResponse = await actorSystem.AskPlayerSession<SessionStateResponse>(
                playerId,
                new GetSessionState());

            var dto = new PlayerStatsDto
            {
                TotalAttempts = stateResponse.TotalExercises,
                SuccessRate = 0.0, // Would track this in actor
                AverageTime = 0.0, // Would track this in actor
                CurrentDifficulty = stateResponse.CurrentDifficulty,
                LearningRate = 0.1, // Would track this in actor
                CurrentAttractor = null
            };

            return Ok(ApiResponse<PlayerStatsDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting player stats");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Reset player session (for testing or new player)
    /// </summary>
    /// <param name="playerId">Player ID</param>
    /// <returns>Success response</returns>
    [HttpPost("reset-session/{playerId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> ResetSession(string playerId)
    {
        try
        {
            await actorSystem.StopPlayerSession(playerId);
            logger.LogInformation("🔄 Reset session for player {PlayerId}", playerId);
            return Ok(ApiResponse<object>.Ok(new { message = "Session reset successfully" }));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting session");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    // ==================
    // Private Methods
    // ==================

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

public class RecordPerformanceRequest
{
    public required string PlayerId { get; init; }
    public required bool Success { get; init; }
    public required double TimeMs { get; init; }
    public required int Attempts { get; init; }
    public required string ShapeId { get; init; }
}

public class GenerateChallengeRequest
{
    public required string PlayerId { get; init; }
    public required List<string> PitchClassSets { get; init; }
    public required List<string> RecentProgression { get; init; }
    public string? Tuning { get; init; }
}

public class SuggestShapesRequest
{
    public required string PlayerId { get; init; }
    public required List<string> PitchClassSets { get; init; }
    public required List<string> CurrentProgression { get; init; }
    public string? Tuning { get; init; }
    public int? TopK { get; init; }
}

public class PlayerStatsDto
{
    public required int TotalAttempts { get; init; }
    public required double SuccessRate { get; init; }
    public required double AverageTime { get; init; }
    public required double CurrentDifficulty { get; init; }
    public required double LearningRate { get; init; }
    public required string? CurrentAttractor { get; init; }
}

public class AdaptiveChallengeDto
{
    public required List<string> ShapeIds { get; init; }
    public required double Quality { get; init; }
    public required double Entropy { get; init; }
    public required double Complexity { get; init; }
    public required double Predictability { get; init; }
    public required double Diversity { get; init; }
    public required string Strategy { get; init; }
    public required double CurrentDifficulty { get; init; }
    public required double LearningRate { get; init; }
}

public class ShapeSuggestionDto
{
    public required string ShapeId { get; init; }
    public required double InformationGain { get; init; }
    public required double VoiceLeadingCost { get; init; }
    public required double FamilyDiversity { get; init; }
    public required double Score { get; init; }
    public required string Reason { get; init; }
}
