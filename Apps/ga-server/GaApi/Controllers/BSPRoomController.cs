namespace GaApi.Controllers;

using GA.BSP.Core.Spatial;
using Microsoft.AspNetCore.RateLimiting;
using Models;

// using GA.Business.Core.Spatial // REMOVED - namespace does not exist;

/// <summary>
///     API controller for BSP dungeon room generation
/// </summary>
[ApiController]
[Route("api/bsp-rooms")]
[EnableRateLimiting("fixed")]
public class BspRoomController(ILogger<BspRoomController> logger) : ControllerBase
{
    /// <summary>
    ///     Generate a dungeon layout using BSP algorithm
    /// </summary>
    /// <param name="params">Generation parameters</param>
    /// <returns>Complete dungeon layout with rooms and corridors</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(ApiResponse<DungeonLayoutResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult GenerateDungeon([FromBody] DungeonGenerationParams? @params)
    {
        try
        {
            @params ??= new DungeonGenerationParams();

            logger.LogInformation("Generating dungeon: {Width}x{Height}, depth={MaxDepth}, seed={Seed}",
                @params.Width, @params.Height, @params.MaxDepth, @params.Seed);

            // Create generator with parameters
            var generator = new BspRoomGenerator(
                @params.Seed,
                @params.MinRoomSize,
                @params.MaxRoomSize,
                @params.CorridorWidth
            );

            // Generate dungeon
            var dungeon = generator.GenerateDungeon(@params.Width, @params.Height, @params.MaxDepth);

            // Convert to DTO
            var response = new DungeonLayoutResponse
            {
                Width = dungeon.Width,
                Height = dungeon.Height,
                Seed = @params.Seed,
                Params = @params,
                Rooms = [.. dungeon.Rooms.Select(r => new RoomDto
                {
                    X = r.X,
                    Y = r.Y,
                    Width = r.Width,
                    Height = r.Height,
                    CenterX = r.CenterX,
                    CenterY = r.CenterY
                })],
                Corridors = [.. dungeon.Corridors.Select(c => new CorridorDto
                {
                    Width = c.Width,
                    Points = [.. c.Points.Select(p => new PointDto
                    {
                        X = p.X,
                        Y = p.Y
                    })]
                })]
            };

            logger.LogInformation("Generated dungeon with {RoomCount} rooms and {CorridorCount} corridors",
                response.Rooms.Count, response.Corridors.Count);

            return Ok(ApiResponse<DungeonLayoutResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating dungeon");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Generate a dungeon with default parameters
    /// </summary>
    /// <param name="seed">Optional seed for reproducible generation</param>
    /// <returns>Complete dungeon layout with rooms and corridors</returns>
    [HttpGet("generate")]
    [ProducesResponseType(typeof(ApiResponse<DungeonLayoutResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public IActionResult GenerateDungeonDefault([FromQuery] int? seed = null)
    {
        var @params = new DungeonGenerationParams { Seed = seed };
        return GenerateDungeon(@params);
    }

    /// <summary>
    ///     Get information about BSP room generation
    /// </summary>
    /// <returns>Information about the BSP room generation algorithm</returns>
    [HttpGet("info")]
    [ProducesResponseType(typeof(ApiResponse<object>), 200)]
    public IActionResult GetInfo()
    {
        var info = new
        {
            Algorithm = "Binary Space Partitioning (BSP)",
            Description = "Recursively divides space into smaller regions and places rooms in leaf nodes",
            DefaultParams = new DungeonGenerationParams(),
            Features = new[]
            {
                "Procedural dungeon generation",
                "Configurable room sizes",
                "Automatic corridor generation",
                "Reproducible with seeds",
                "Adjustable complexity via depth parameter"
            },
            Usage = new
            {
                GenerateDefault = "GET /api/bsp-rooms/generate?seed=12345",
                GenerateCustom = "POST /api/bsp-rooms/generate with DungeonGenerationParams body"
            }
        };

        return Ok(ApiResponse<object>.Ok(info));
    }

    /// <summary>
    ///     Generate multiple dungeons for comparison
    /// </summary>
    /// <param name="count">Number of dungeons to generate (max 10)</param>
    /// <param name="width">Width of each dungeon</param>
    /// <param name="height">Height of each dungeon</param>
    /// <returns>List of generated dungeons</returns>
    [HttpGet("generate-batch")]
    [ProducesResponseType(typeof(ApiResponse<List<DungeonLayoutResponse>>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public IActionResult GenerateBatch(
        [FromQuery] int count = 3,
        [FromQuery] int width = 80,
        [FromQuery] int height = 60)
    {
        try
        {
            if (count is < 1 or > 10)
            {
                return BadRequest(ApiResponse<object>.Fail("Count must be between 1 and 10"));
            }

            var dungeons = new List<DungeonLayoutResponse>();

            for (var i = 0; i < count; i++)
            {
                var @params = new DungeonGenerationParams
                {
                    Width = width,
                    Height = height,
                    Seed = i // Use index as seed for variety
                };

                var generator = new BspRoomGenerator(
                    @params.Seed,
                    @params.MinRoomSize,
                    @params.MaxRoomSize,
                    @params.CorridorWidth
                );

                var dungeon = generator.GenerateDungeon(@params.Width, @params.Height, @params.MaxDepth);

                dungeons.Add(new DungeonLayoutResponse
                {
                    Width = dungeon.Width,
                    Height = dungeon.Height,
                    Seed = @params.Seed,
                    Params = @params,
                    Rooms = [.. dungeon.Rooms.Select(r => new RoomDto
                    {
                        X = r.X,
                        Y = r.Y,
                        Width = r.Width,
                        Height = r.Height,
                        CenterX = r.CenterX,
                        CenterY = r.CenterY
                    })],
                    Corridors = [.. dungeon.Corridors.Select(c => new CorridorDto
                    {
                        Width = c.Width,
                        Points = [.. c.Points.Select(p => new PointDto
                        {
                            X = p.X,
                            Y = p.Y
                        })]
                    })]
                });
            }

            logger.LogInformation("Generated batch of {Count} dungeons", count);

            return Ok(ApiResponse<List<DungeonLayoutResponse>>.Ok(dungeons));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating dungeon batch");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }
}
