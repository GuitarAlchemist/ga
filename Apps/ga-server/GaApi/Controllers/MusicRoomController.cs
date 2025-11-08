namespace GaApi.Controllers;

using System.Runtime.CompilerServices;
using GA.BSP.Core.Spatial;
using GA.Business.Core.Atonal;
using Microsoft.AspNetCore.RateLimiting;
using Models;
using Services;
// using GA.Business.Core.Spatial // REMOVED - namespace does not exist;

/// <summary>
///     API controller for music theory-based room generation in BSP DOOM Explorer
/// </summary>
[ApiController]
[Route("api/music-rooms")]
[EnableRateLimiting("fixed")]
public class MusicRoomController(
    ILogger<MusicRoomController> logger,
    MusicRoomService musicRoomService)
    : ControllerBase
{
    /// <summary>
    ///     Generate rooms for a specific floor in the BSP DOOM Explorer pyramid
    ///     This endpoint generates and persists the layout to MongoDB
    /// </summary>
    /// <param name="floor">Floor number (0-5)</param>
    /// <param name="floorSize">Size of the floor</param>
    /// <param name="seed">Optional seed for reproducible generation</param>
    /// <returns>Music theory rooms with BSP layout</returns>
    [HttpGet("floor/{floor}")]
    [ProducesResponseType(typeof(ApiResponse<MusicFloorResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 500)]
    public async Task<IActionResult> GenerateFloorRooms(
        int floor,
        [FromQuery] int floorSize = 100,
        [FromQuery] int? seed = null)
    {
        try
        {
            if (floor is < 0 or > 5)
            {
                return BadRequest(ApiResponse<object>.Fail("Floor must be between 0 and 5"));
            }

            logger.LogInformation("Generating music rooms for floor {Floor}, size={FloorSize}, seed={Seed}",
                floor, floorSize, seed);

            // Generate and persist to MongoDB
            var document = await musicRoomService.GenerateAndPersistAsync(floor, floorSize, seed);

            // Convert document to response
            var response = ConvertDocumentToResponse(document);

            logger.LogInformation("Generated {RoomCount} music rooms for floor {Floor}",
                response.Rooms.Count, floor);

            return Ok(ApiResponse<MusicFloorResponse>.Ok(response));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating music rooms for floor {Floor}", floor);
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Stream room generation for progressive rendering (memory-efficient)
    /// </summary>
    /// <param name="floor">Floor number (0-5)</param>
    /// <param name="floorSize">Size of the floor</param>
    /// <param name="seed">Optional seed for reproducible generation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream of music rooms</returns>
    [HttpGet("floor/{floor}/stream")]
    [ProducesResponseType(typeof(IAsyncEnumerable<MusicRoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async IAsyncEnumerable<MusicRoomDto> GenerateFloorRoomsStream(
        int floor,
        [FromQuery] int floorSize = 100,
        [FromQuery] int? seed = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (floor is < 0 or > 5)
        {
            logger.LogWarning("Invalid floor number: {Floor}", floor);
            yield break;
        }

        logger.LogInformation("Streaming music rooms for floor {Floor}, size={FloorSize}, seed={Seed}",
            floor, floorSize, seed);

        var roomCount = 0;
        await foreach (var room in musicRoomService.GenerateRoomsStreamAsync(floor, floorSize, seed, cancellationToken))
        {
            roomCount++;
            yield return room;

            if (roomCount % 10 == 0)
            {
                logger.LogDebug("Streamed {RoomCount} rooms so far", roomCount);
            }
        }

        logger.LogInformation("Completed streaming {RoomCount} rooms for floor {Floor}", roomCount, floor);
    }

    /// <summary>
    ///     Get music theory data for a specific floor
    /// </summary>
    private FloorMusicData GetMusicDataForFloor(int floor)
    {
        return floor switch
        {
            0 => new FloorMusicData
            {
                FloorName = "Set Classes",
                TotalItems = SetClass.Items.Count,
                Categories =
                [
                    "Chromatic", "Diatonic", "Pentatonic", "Hexatonic",
                    "Octatonic", "Whole Tone", "Augmented", "Diminished"
                ],
                Items = SetClass.Items.Select(sc => new MusicItemData
                {
                    Name = sc.ToString(),
                    Category = sc.IsModal ? "Modal" : "Atonal",
                    PitchClasses = sc.PrimeForm.Select(pc => pc.Value).ToList(),
                    Description = $"Cardinality: {sc.Cardinality.Value}, ICV: {sc.IntervalClassVector}"
                }).ToList()
            },
            1 => new FloorMusicData
            {
                FloorName = "Forte Codes",
                TotalItems = ForteNumber.Items.Count,
                Categories =
                [
                    "Triads (3-x)", "Tetrads (4-x)", "Pentachords (5-x)",
                    "Hexachords (6-x)", "Septachords (7-x)", "Octachords (8-x)"
                ],
                Items = ForteNumber.Items.Select(fn => new MusicItemData
                {
                    Name = fn.ToString(),
                    Category = $"{fn.Cardinality.Value}-note sets",
                    Description = $"Forte Number: {fn}"
                }).ToList()
            },
            2 => new FloorMusicData
            {
                FloorName = "Prime Forms",
                TotalItems = 200, // Sample
                Categories =
                [
                    "Major Triads", "Minor Triads", "Diminished", "Augmented",
                    "Suspended", "Seventh Chords", "Extended Chords"
                ],
                Items = PitchClassSet.Items
                    .Where(pcs => pcs.IsPrimeForm)
                    .Take(200)
                    .Select(pcs => new MusicItemData
                    {
                        Name = pcs.Name,
                        Category = pcs.IsModal ? "Modal" : "Atonal",
                        PitchClasses = pcs.Select(pc => pc.Value).ToList(),
                        Description = $"Prime Form: {pcs.Id}"
                    }).ToList()
            },
            3 => new FloorMusicData
            {
                FloorName = "Chords",
                TotalItems = 350,
                Categories =
                [
                    "Major", "Minor", "Dominant 7th", "Major 7th",
                    "Minor 7th", "Diminished 7th", "Half-Diminished"
                ],
                Items = [] // Would be populated from chord database
            },
            4 => new FloorMusicData
            {
                FloorName = "Chord Inversions",
                TotalItems = 100, // Sample of 4096
                Categories =
                [
                    "Root Position", "1st Inversion", "2nd Inversion",
                    "3rd Inversion", "Drop 2", "Drop 3"
                ],
                Items = []
            },
            5 => new FloorMusicData
            {
                FloorName = "Chord Voicings",
                TotalItems = 200, // Sample of 100k+
                Categories =
                [
                    "Jazz Voicings", "Classical Voicings", "Rock Voicings",
                    "CAGED System", "Position-Based", "String Sets"
                ],
                Items = []
            },
            _ => throw new ArgumentException($"Invalid floor: {floor}")
        };
    }

    /// <summary>
    ///     Calculate room generation parameters based on floor
    /// </summary>
    private RoomParameters CalculateRoomParameters(int floor, int floorSize, int totalItems)
    {
        // Adjust room sizes based on number of items
        var targetRoomCount = Math.Min(totalItems, floor switch
        {
            0 => 93,
            1 => 115,
            2 => 200,
            3 => 350,
            4 => 100,
            5 => 200,
            _ => 100
        });

        var maxDepth = (int)Math.Ceiling(Math.Log2(targetRoomCount));

        return new RoomParameters
        {
            MinRoomSize = floor switch
            {
                0 => 12,
                1 => 10,
                2 => 8,
                3 => 8,
                4 => 6,
                5 => 4,
                _ => 8
            },
            MaxRoomSize = floor switch
            {
                0 => 24,
                1 => 20,
                2 => 18,
                3 => 16,
                4 => 12,
                5 => 10,
                _ => 16
            },
            MaxDepth = maxDepth
        };
    }

    /// <summary>
    ///     Assign music data to generated rooms
    /// </summary>
    private List<MusicRoomDto> AssignMusicDataToRooms(
        List<Room> rooms,
        FloorMusicData musicData,
        int floor)
    {
        var musicRooms = new List<MusicRoomDto>();
        var categories = musicData.Categories;

        for (var i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var categoryIndex = i % categories.Count;
            var category = categories[categoryIndex];

            // Get items for this category
            var categoryItems = musicData.Items
                .Where(item => item.Category.Contains(category, StringComparison.OrdinalIgnoreCase))
                .Take(20)
                .ToList();

            musicRooms.Add(new MusicRoomDto
            {
                Id = $"floor{floor}_room{i}",
                X = room.X,
                Y = room.Y,
                Width = room.Width,
                Height = room.Height,
                CenterX = room.CenterX,
                CenterY = room.CenterY,
                Floor = floor,
                Category = category,
                Items = categoryItems.Select(item => item.Name).ToList(),
                Color = GetCategoryColor(categoryIndex, categories.Count),
                Description = $"{category} - Floor {floor}"
            });
        }

        return musicRooms;
    }

    /// <summary>
    ///     Get color for category based on index
    /// </summary>
    private string GetCategoryColor(int index, int total)
    {
        var hue = index / (double)total * 360;
        return $"hsl({hue}, 70%, 50%)";
    }

    /// <summary>
    ///     Queue a room generation job for background processing
    /// </summary>
    [HttpPost("queue")]
    [ProducesResponseType(typeof(ApiResponse<RoomGenerationJob>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    public async Task<IActionResult> QueueGeneration([FromBody] QueueGenerationRequest request)
    {
        try
        {
            if (request.Floor is < 0 or > 5)
            {
                return BadRequest(ApiResponse<object>.Fail("Floor must be between 0 and 5"));
            }

            var job = await musicRoomService.QueueGenerationAsync(
                request.Floor,
                request.FloorSize,
                request.Seed);

            return Ok(ApiResponse<RoomGenerationJob>.Ok(job));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error queueing room generation");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get job status
    /// </summary>
    [HttpGet("jobs/{jobId}")]
    [ProducesResponseType(typeof(ApiResponse<RoomGenerationJob>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> GetJobStatus(string jobId)
    {
        try
        {
            var job = await musicRoomService.GetJobAsync(jobId);
            if (job == null)
            {
                return NotFound(ApiResponse<object>.Fail($"Job {jobId} not found"));
            }

            return Ok(ApiResponse<RoomGenerationJob>.Ok(job));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting job status");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Process a queued job
    /// </summary>
    [HttpPost("jobs/{jobId}/process")]
    [ProducesResponseType(typeof(ApiResponse<MusicFloorResponse>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 400)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> ProcessJob(string jobId)
    {
        try
        {
            var document = await musicRoomService.ProcessJobAsync(jobId);
            var response = ConvertDocumentToResponse(document);

            return Ok(ApiResponse<MusicFloorResponse>.Ok(response));
        }
        catch (ArgumentException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing job");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get all pending jobs
    /// </summary>
    [HttpGet("jobs/pending")]
    [ProducesResponseType(typeof(ApiResponse<List<RoomGenerationJob>>), 200)]
    public async Task<IActionResult> GetPendingJobs([FromQuery] int limit = 10)
    {
        try
        {
            var jobs = await musicRoomService.GetPendingJobsAsync(limit);
            return Ok(ApiResponse<List<RoomGenerationJob>>.Ok(jobs));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting pending jobs");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Get all layouts for a floor
    /// </summary>
    [HttpGet("floor/{floor}/layouts")]
    [ProducesResponseType(typeof(ApiResponse<List<MusicRoomDocument>>), 200)]
    public async Task<IActionResult> GetFloorLayouts(int floor, [FromQuery] int limit = 10)
    {
        try
        {
            var layouts = await musicRoomService.GetLayoutsForFloorAsync(floor, limit);
            return Ok(ApiResponse<List<MusicRoomDocument>>.Ok(layouts));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting floor layouts");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Delete a layout
    /// </summary>
    [HttpDelete("layouts/{layoutId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object>), 404)]
    public async Task<IActionResult> DeleteLayout(string layoutId)
    {
        try
        {
            var deleted = await musicRoomService.DeleteLayoutAsync(layoutId);
            if (!deleted)
            {
                return NotFound(ApiResponse<object>.Fail($"Layout {layoutId} not found"));
            }

            return Ok(ApiResponse<bool>.Ok(true));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting layout");
            return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
        }
    }

    /// <summary>
    ///     Convert MongoDB document to API response
    /// </summary>
    private MusicFloorResponse ConvertDocumentToResponse(MusicRoomDocument document)
    {
        return new MusicFloorResponse
        {
            Floor = document.Floor,
            FloorName = document.FloorName,
            FloorSize = document.FloorSize,
            TotalItems = document.TotalItems,
            Categories = document.Categories,
            Rooms = document.Rooms.Select(r => new MusicRoomDto
            {
                Id = r.Id,
                X = r.X,
                Y = r.Y,
                Width = r.Width,
                Height = r.Height,
                CenterX = r.CenterX,
                CenterY = r.CenterY,
                Floor = r.Floor,
                Category = r.Category,
                Items = r.Items,
                Color = r.Color,
                Description = r.Description
            }).ToList(),
            Corridors = document.Corridors.Select(c => new CorridorDto
            {
                Width = c.Width,
                Points = c.Points.Select(p => new PointDto { X = p.X, Y = p.Y }).ToList()
            }).ToList(),
            Seed = document.Seed
        };
    }

    private class FloorMusicData
    {
        public string FloorName { get; set; } = "";
        public int TotalItems { get; set; }
        public List<string> Categories { get; set; } = [];
        public List<MusicItemData> Items { get; set; } = [];
    }

    private class MusicItemData
    {
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public List<int> PitchClasses { get; set; } = [];
        public string Description { get; set; } = "";
    }

    private class RoomParameters
    {
        public int MinRoomSize { get; set; }
        public int MaxRoomSize { get; set; }
        public int MaxDepth { get; set; }
    }
}

/// <summary>
///     Request model for queueing room generation
/// </summary>
public class QueueGenerationRequest
{
    public int Floor { get; set; }
    public int FloorSize { get; set; } = 100;
    public int? Seed { get; set; }
}
