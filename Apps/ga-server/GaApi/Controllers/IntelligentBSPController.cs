namespace GaApi.Controllers;

using GA.Business.Core.Fretboard;
using GA.Business.Core.Notes;
using Microsoft.AspNetCore.RateLimiting;
using Models;

/// <summary>
///     API controller for Intelligent BSP Level Generation
///     Uses ALL 9 advanced mathematical techniques to create musically-aware BSP levels
/// </summary>
[ApiController]
[Route("api/intelligent-bsp")]
[EnableRateLimiting("fixed")]
public class IntelligentBspController(
    IntelligentBspGenerator generator,
    IShapeGraphBuilder graphBuilder,
    ILogger<IntelligentBspController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Generate an intelligent BSP level with musical awareness
    /// </summary>
    /// <param name="request">Level generation request</param>
    /// <returns>Intelligent BSP level with floors, landmarks, portals, safe zones, and challenge paths</returns>
    [HttpPost("generate-level")]
    [ProducesResponseType(typeof(ApiResponse<IntelligentBspLevelDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GenerateLevel([FromBody] GenerateLevelRequest request)
    {
        try
        {
            logger.LogInformation("🧠 Generating intelligent BSP level...");

            // Parse pitch class sets
            var pitchClassSets = request.PitchClassSets
                .Select(pcs => PitchClassSet.Parse(pcs))
                .ToList();

            // Build shape graph
            var tuning = ParseTuning(request.Tuning);
            var graph = await graphBuilder.BuildGraphAsync(tuning, pitchClassSets);

            // Generate intelligent level
            var options = new BspLevelOptions
            {
                ChordFamilyCount = request.ChordFamilyCount ?? 5,
                LandmarkCount = request.LandmarkCount ?? 10,
                BridgeChordCount = request.BridgeChordCount ?? 5,
                LearningPathLength = request.LearningPathLength ?? 8
            };

            var level = await generator.GenerateLevelAsync(graph, options);

            // Convert to DTO
            var dto = new IntelligentBspLevelDto
            {
                Floors = level.Floors.Select(f => new BspFloorDto
                {
                    FloorId = f.FloorId,
                    Name = f.Name,
                    ShapeIds = f.ShapeIds,
                    Color = $"#{f.Color:X6}"
                }).ToList(),
                Landmarks = level.Landmarks.Select(l => new BspLandmarkDto
                {
                    ShapeId = l.ShapeId,
                    Name = l.Name,
                    Importance = l.Importance,
                    Type = l.Type
                }).ToList(),
                Portals = level.Portals.Select(p => new BspPortalDto
                {
                    ShapeId = p.ShapeId,
                    Name = p.Name,
                    Strength = p.Strength,
                    Type = p.Type
                }).ToList(),
                SafeZones = level.SafeZones.Select(s => new BspSafeZoneDto
                {
                    ShapeId = s.ShapeId,
                    Name = s.Name,
                    Stability = s.Stability,
                    Type = s.Type
                }).ToList(),
                ChallengePaths = level.ChallengePaths.Select(c => new BspChallengePathDto
                {
                    Name = c.Name,
                    ShapeIds = c.ShapeIds,
                    Period = c.Period,
                    Difficulty = c.Difficulty
                }).ToList(),
                LearningPath = level.LearningPath,
                Difficulty = level.Difficulty,
                Metadata = level.Metadata
            };

            logger.LogInformation("✅ Level generated successfully");
            return Ok(ApiResponse<IntelligentBspLevelDto>.Ok(dto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating intelligent BSP level");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get level statistics and quality metrics
    /// </summary>
    /// <param name="request">Level generation request</param>
    /// <returns>Level quality metrics</returns>
    [HttpPost("level-stats")]
    [ProducesResponseType(typeof(ApiResponse<LevelStatsDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GetLevelStats([FromBody] GenerateLevelRequest request)
    {
        try
        {
            // Generate level
            var levelResponse = await GenerateLevel(request);
            if (levelResponse is not OkObjectResult okResult)
            {
                return levelResponse;
            }

            var apiResponse = okResult.Value as ApiResponse<IntelligentBspLevelDto>;
            var level = apiResponse?.Data;
            if (level == null)
            {
                return BadRequest(ApiResponse<object>.Fail("Failed to generate level"));
            }

            // Extract stats
            var stats = new LevelStatsDto
            {
                FloorCount = level.Floors.Count,
                LandmarkCount = level.Landmarks.Count,
                PortalCount = level.Portals.Count,
                SafeZoneCount = level.SafeZones.Count,
                ChallengePathCount = level.ChallengePaths.Count,
                LearningPathLength = level.LearningPath.Count,
                Difficulty = level.Difficulty,
                AlgebraicConnectivity =
                    level.Metadata.TryGetValue("AlgebraicConnectivity", out var ac) ? (double)ac : 0,
                SpectralGap = level.Metadata.TryGetValue("SpectralGap", out var sg) ? (double)sg : 0,
                LyapunovExponent = level.Metadata.TryGetValue("LyapunovExponent", out var le) ? (double)le : 0,
                Entropy = level.Metadata.TryGetValue("Entropy", out var e) ? (double)e : 0,
                Complexity = level.Metadata.TryGetValue("Complexity", out var c) ? (double)c : 0,
                Quality = level.Metadata.TryGetValue("Quality", out var q) ? (double)q : 0
            };

            return Ok(ApiResponse<LevelStatsDto>.Ok(stats));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting level stats");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    private Tuning ParseTuning(string? tuningStr)
    {
        if (string.IsNullOrWhiteSpace(tuningStr))
        {
            return Tuning.Default; // Standard guitar tuning (E A D G B E)
        }

        // Parse as pitch collection string (e.g., "E2 A2 D3 G3 B3 E4")
        return new Tuning(PitchCollection.Parse(tuningStr));
    }
}

// ==================
// Request/Response DTOs
// ==================

public class GenerateLevelRequest
{
    public required List<string> PitchClassSets { get; init; }
    public string? Tuning { get; init; }
    public int? ChordFamilyCount { get; init; }
    public int? LandmarkCount { get; init; }
    public int? BridgeChordCount { get; init; }
    public int? LearningPathLength { get; init; }
}

public class IntelligentBspLevelDto
{
    public required List<BspFloorDto> Floors { get; init; }
    public required List<BspLandmarkDto> Landmarks { get; init; }
    public required List<BspPortalDto> Portals { get; init; }
    public required List<BspSafeZoneDto> SafeZones { get; init; }
    public required List<BspChallengePathDto> ChallengePaths { get; init; }
    public required List<string> LearningPath { get; init; }
    public required double Difficulty { get; init; }
    public required Dictionary<string, object> Metadata { get; init; }
}

public class BspFloorDto
{
    public required int FloorId { get; init; }
    public required string Name { get; init; }
    public required List<string> ShapeIds { get; init; }
    public required string Color { get; init; }
}

public class BspLandmarkDto
{
    public required string ShapeId { get; init; }
    public required string Name { get; init; }
    public required double Importance { get; init; }
    public required string Type { get; init; }
}

public class BspPortalDto
{
    public required string ShapeId { get; init; }
    public required string Name { get; init; }
    public required double Strength { get; init; }
    public required string Type { get; init; }
}

public class BspSafeZoneDto
{
    public required string ShapeId { get; init; }
    public required string Name { get; init; }
    public required double Stability { get; init; }
    public required string Type { get; init; }
}

public class BspChallengePathDto
{
    public required string Name { get; init; }
    public required List<string> ShapeIds { get; init; }
    public required int Period { get; init; }
    public required double Difficulty { get; init; }
}

public class LevelStatsDto
{
    public required int FloorCount { get; init; }
    public required int LandmarkCount { get; init; }
    public required int PortalCount { get; init; }
    public required int SafeZoneCount { get; init; }
    public required int ChallengePathCount { get; init; }
    public required int LearningPathLength { get; init; }
    public required double Difficulty { get; init; }
    public required double AlgebraicConnectivity { get; init; }
    public required double SpectralGap { get; init; }
    public required double LyapunovExponent { get; init; }
    public required double Entropy { get; init; }
    public required double Complexity { get; init; }
    public required double Quality { get; init; }
}
