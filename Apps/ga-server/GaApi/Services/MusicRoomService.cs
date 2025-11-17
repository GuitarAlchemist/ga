namespace GaApi.Services;

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using GA.BSP.Core.Spatial;
using GA.Business.Core.Atonal;
using Microsoft.Extensions.Caching.Distributed;
using Models;
using MongoDB.Driver;
// using GA.Business.Core.Spatial // REMOVED - namespace does not exist;

/// <summary>
///     Service for managing music room generation and persistence
/// </summary>
public class MusicRoomService
{
    private readonly IDistributedCache _cache;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private readonly IMongoCollection<RoomGenerationJob> _jobs;
    private readonly ILogger<MusicRoomService> _logger;
    private readonly IMongoCollection<MusicRoomDocument> _roomLayouts;
    private bool _indexesCreated;

    public MusicRoomService(
        MongoDbService mongoDbService,
        ILogger<MusicRoomService> logger,
        IHttpClientFactory httpClientFactory,
        IDistributedCache cache)
    {
        var database = mongoDbService.Database;
        _roomLayouts = database.GetCollection<MusicRoomDocument>("musicRoomLayouts");
        _jobs = database.GetCollection<RoomGenerationJob>("roomGenerationJobs");
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _cache = cache;

        // Don't create indexes in constructor - do it lazily on first use
    }

    /// <summary>
    ///     Create MongoDB indexes for efficient querying (called lazily on first use)
    /// </summary>
    private async Task EnsureIndexesAsync()
    {
        if (_indexesCreated)
        {
            return;
        }

        await _indexLock.WaitAsync();
        try
        {
            if (_indexesCreated)
            {
                return;
            }

            // Index on floor and seed for quick lookups
            var floorSeedIndex = Builders<MusicRoomDocument>.IndexKeys
                .Ascending(x => x.Floor)
                .Ascending(x => x.Seed);
            await _roomLayouts.Indexes.CreateOneAsync(new CreateIndexModel<MusicRoomDocument>(floorSeedIndex));

            // Index on job status for queue processing
            var statusIndex = Builders<RoomGenerationJob>.IndexKeys
                .Ascending(x => x.Status)
                .Ascending(x => x.CreatedAt);
            await _jobs.Indexes.CreateOneAsync(new CreateIndexModel<RoomGenerationJob>(statusIndex));

            _indexesCreated = true;
            _logger.LogInformation("MongoDB indexes created successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create MongoDB indexes (they may already exist)");
            _indexesCreated = true; // Don't keep trying
        }
        finally
        {
            _indexLock.Release();
        }
    }

    /// <summary>
    ///     Queue a room generation job
    /// </summary>
    public async Task<RoomGenerationJob> QueueGenerationAsync(int floor, int floorSize, int? seed = null)
    {
        await EnsureIndexesAsync();

        _logger.LogInformation("Queueing room generation for floor {Floor}, size={FloorSize}, seed={Seed}",
            floor, floorSize, seed);

        var job = new RoomGenerationJob
        {
            Floor = floor,
            FloorSize = floorSize,
            Seed = seed,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _jobs.InsertOneAsync(job);

        _logger.LogInformation("Queued job {JobId} for floor {Floor}", job.Id, floor);

        return job;
    }

    /// <summary>
    ///     Get job status
    /// </summary>
    public async Task<RoomGenerationJob?> GetJobAsync(string jobId)
    {
        return await _jobs.Find(j => j.Id == jobId).FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Get all pending jobs
    /// </summary>
    public async Task<List<RoomGenerationJob>> GetPendingJobsAsync(int limit = 10)
    {
        return await _jobs
            .Find(j => j.Status == JobStatus.Pending)
            .SortBy(j => j.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    /// <summary>
    ///     Process a queued job
    /// </summary>
    public async Task<MusicRoomDocument> ProcessJobAsync(string jobId)
    {
        var job = await GetJobAsync(jobId);
        if (job == null)
        {
            throw new ArgumentException($"Job {jobId} not found");
        }

        if (job.Status != JobStatus.Pending)
        {
            throw new InvalidOperationException($"Job {jobId} is not pending (status: {job.Status})");
        }

        _logger.LogInformation("Processing job {JobId} for floor {Floor}", jobId, job.Floor);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Update job status to processing
            var updateDef = Builders<RoomGenerationJob>.Update
                .Set(j => j.Status, JobStatus.Processing)
                .Set(j => j.StartedAt, DateTime.UtcNow);
            await _jobs.UpdateOneAsync(j => j.Id == jobId, updateDef);

            // Generate the room layout
            var layout = await GenerateAndPersistAsync(job.Floor, job.FloorSize, job.Seed);

            stopwatch.Stop();

            // Update job status to completed
            updateDef = Builders<RoomGenerationJob>.Update
                .Set(j => j.Status, JobStatus.Completed)
                .Set(j => j.CompletedAt, DateTime.UtcNow)
                .Set(j => j.ResultId, layout.Id)
                .Set(j => j.ProcessingTimeMs, stopwatch.ElapsedMilliseconds);
            await _jobs.UpdateOneAsync(j => j.Id == jobId, updateDef);

            _logger.LogInformation("Completed job {JobId} in {ElapsedMs}ms", jobId, stopwatch.ElapsedMilliseconds);

            return layout;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex, "Failed to process job {JobId}", jobId);

            // Update job status to failed
            var updateDef = Builders<RoomGenerationJob>.Update
                .Set(j => j.Status, JobStatus.Failed)
                .Set(j => j.CompletedAt, DateTime.UtcNow)
                .Set(j => j.Error, ex.Message)
                .Set(j => j.ProcessingTimeMs, stopwatch.ElapsedMilliseconds);
            await _jobs.UpdateOneAsync(j => j.Id == jobId, updateDef);

            throw;
        }
    }

    /// <summary>
    ///     Generate and persist a room layout
    /// </summary>
    public async Task<MusicRoomDocument> GenerateAndPersistAsync(int floor, int floorSize, int? seed = null)
    {
        await EnsureIndexesAsync();

        _logger.LogInformation("Generating room layout for floor {Floor}, size={FloorSize}, seed={Seed}",
            floor, floorSize, seed);

        // Check if layout already exists
        var existing = await GetLayoutAsync(floor, floorSize, seed);
        if (existing != null)
        {
            _logger.LogInformation("Found existing layout for floor {Floor}, seed={Seed}", floor, seed);
            return existing;
        }

        // Get music data for this floor
        var musicData = GetMusicDataForFloor(floor);

        // Calculate room parameters
        var roomParams = CalculateRoomParameters(floor, floorSize, musicData.TotalItems);

        // Generate BSP dungeon layout
        var generator = new BspRoomGenerator(
            seed,
            roomParams.MinRoomSize,
            roomParams.MaxRoomSize
        );

        var dungeon = generator.GenerateDungeon(floorSize, floorSize, roomParams.MaxDepth);

        // Assign music data to rooms (now async)
        var musicRooms = await AssignMusicDataToRooms(dungeon.Rooms, musicData, floor);

        // Create document
        var document = new MusicRoomDocument
        {
            Floor = floor,
            FloorName = musicData.FloorName,
            FloorSize = floorSize,
            Seed = seed,
            TotalItems = musicData.TotalItems,
            Categories = musicData.Categories,
            Rooms = [.. musicRooms.Select(r => new RoomData
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
            })],
            Corridors = [.. dungeon.Corridors.Select(c => new CorridorData
            {
                Width = c.Width,
                Points = [.. c.Points.Select(p => new PointData { X = p.X, Y = p.Y })]
            })],
            GenerationParams = new GenerationParamsData
            {
                MinRoomSize = roomParams.MinRoomSize,
                MaxRoomSize = roomParams.MaxRoomSize,
                MaxDepth = roomParams.MaxDepth,
                CorridorWidth = 1
            }
        };

        // Persist to MongoDB
        await _roomLayouts.InsertOneAsync(document);

        _logger.LogInformation("Persisted layout {LayoutId} for floor {Floor} with {RoomCount} rooms",
            document.Id, floor, document.Rooms.Count);

        return document;
    }

    /// <summary>
    ///     Stream room generation for progressive rendering (memory-efficient)
    /// </summary>
    public async IAsyncEnumerable<MusicRoomDto> GenerateRoomsStreamAsync(
        int floor,
        int floorSize,
        int? seed = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Streaming room generation for floor {Floor}, size={FloorSize}, seed={Seed}",
            floor, floorSize, seed);

        // Get music data for this floor
        var musicData = GetMusicDataForFloor(floor);

        // Calculate room parameters
        var roomParams = CalculateRoomParameters(floor, floorSize, musicData.TotalItems);

        // Generate BSP dungeon layout
        var generator = new BspRoomGenerator(
            seed,
            roomParams.MinRoomSize,
            roomParams.MaxRoomSize
        );

        var dungeon = generator.GenerateDungeon(floorSize, floorSize, roomParams.MaxDepth);

        // Get all music items for this floor
        var allItems = await GetMusicItemsForFloor(floor);
        var itemsPerRoom = Math.Max(1, allItems.Count / Math.Max(1, dungeon.Rooms.Count));

        var categories = musicData.Categories;
        var roomCount = 0;

        // Stream rooms one at a time
        for (var i = 0; i < dungeon.Rooms.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Room streaming cancelled after {RoomCount} rooms", roomCount);
                yield break;
            }

            var room = dungeon.Rooms[i];
            var categoryIndex = i % categories.Count;
            var category = categories[categoryIndex];

            // Assign a subset of items to this room
            var startIndex = i * itemsPerRoom;
            var endIndex = Math.Min(startIndex + itemsPerRoom, allItems.Count);
            var roomItems = allItems.Skip(startIndex).Take(endIndex - startIndex).ToList();

            roomCount++;

            yield return new MusicRoomDto
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
                Items = roomItems,
                Color = GetCategoryColor(categoryIndex, categories.Count),
                Description = $"{category} - {roomItems.Count} items"
            };

            // Log progress every 10 rooms
            if (roomCount % 10 == 0)
            {
                _logger.LogDebug("Streamed {RoomCount} rooms so far", roomCount);
            }

            // Backpressure control
            await Task.Delay(1, cancellationToken);
        }

        _logger.LogInformation("Completed streaming {RoomCount} rooms for floor {Floor}", roomCount, floor);
    }

    /// <summary>
    ///     Get a persisted layout
    /// </summary>
    public async Task<MusicRoomDocument?> GetLayoutAsync(int floor, int floorSize, int? seed = null)
    {
        var filter = Builders<MusicRoomDocument>.Filter.And(
            Builders<MusicRoomDocument>.Filter.Eq(x => x.Floor, floor),
            Builders<MusicRoomDocument>.Filter.Eq(x => x.FloorSize, floorSize),
            Builders<MusicRoomDocument>.Filter.Eq(x => x.Seed, seed)
        );

        return await _roomLayouts.Find(filter).FirstOrDefaultAsync();
    }

    /// <summary>
    ///     Get all layouts for a floor
    /// </summary>
    public async Task<List<MusicRoomDocument>> GetLayoutsForFloorAsync(int floor, int limit = 10)
    {
        return await _roomLayouts
            .Find(x => x.Floor == floor)
            .SortByDescending(x => x.CreatedAt)
            .Limit(limit)
            .ToListAsync();
    }

    /// <summary>
    ///     Delete a layout
    /// </summary>
    public async Task<bool> DeleteLayoutAsync(string layoutId)
    {
        var result = await _roomLayouts.DeleteOneAsync(x => x.Id == layoutId);
        return result.DeletedCount > 0;
    }

    // Helper methods from MusicRoomController
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
                ]
            },
            1 => new FloorMusicData
            {
                FloorName = "Forte Codes",
                TotalItems = ForteNumber.Items.Count,
                Categories =
                [
                    "Triads (3-x)", "Tetrads (4-x)", "Pentachords (5-x)",
                    "Hexachords (6-x)", "Septachords (7-x)", "Octachords (8-x)"
                ]
            },
            2 => new FloorMusicData
            {
                FloorName = "Prime Forms",
                TotalItems = 200, // Sample
                Categories =
                [
                    "Major Triads", "Minor Triads", "Diminished", "Augmented",
                    "Suspended", "Seventh Chords", "Extended Chords"
                ]
            },
            3 => new FloorMusicData
            {
                FloorName = "Chords",
                TotalItems = 350,
                Categories =
                [
                    "Major", "Minor", "Dominant 7th", "Major 7th",
                    "Minor 7th", "Diminished 7th", "Half-Diminished"
                ]
            },
            4 => new FloorMusicData
            {
                FloorName = "Chord Inversions",
                TotalItems = 100, // Sample of 4096
                Categories =
                [
                    "Root Position", "1st Inversion", "2nd Inversion",
                    "3rd Inversion", "Drop 2", "Drop 3"
                ]
            },
            5 => new FloorMusicData
            {
                FloorName = "Chord Voicings",
                TotalItems = 200, // Sample of 100k+
                Categories =
                [
                    "Jazz Voicings", "Classical Voicings", "Rock Voicings",
                    "CAGED System", "Position-Based", "String Sets"
                ]
            },
            _ => throw new ArgumentException($"Invalid floor: {floor}")
        };
    }

    private RoomParameters CalculateRoomParameters(int floor, int floorSize, int totalItems)
    {
        var targetRoomCount = Math.Min(totalItems, floor switch
        {
            0 => 93,
            1 => 115,
            _ => 100
        });

        var maxDepth = (int)Math.Ceiling(Math.Log2(targetRoomCount));

        return new RoomParameters
        {
            MinRoomSize = floor switch { 0 => 12, 1 => 10, _ => 8 },
            MaxRoomSize = floor switch { 0 => 24, 1 => 20, _ => 16 },
            MaxDepth = maxDepth
        };
    }

    private async Task<List<MusicRoomDto>> AssignMusicDataToRooms(
        List<Room> rooms,
        FloorMusicData musicData,
        int floor)
    {
        var musicRooms = new List<MusicRoomDto>();
        var categories = musicData.Categories;

        // Get actual music items for this floor (now async)
        var allItems = await GetMusicItemsForFloor(floor);
        var itemsPerRoom = Math.Max(1, allItems.Count / Math.Max(1, rooms.Count));

        for (var i = 0; i < rooms.Count; i++)
        {
            var room = rooms[i];
            var categoryIndex = i % categories.Count;
            var category = categories[categoryIndex];

            // Assign a subset of items to this room
            var startIndex = i * itemsPerRoom;
            var endIndex = Math.Min(startIndex + itemsPerRoom, allItems.Count);
            var roomItems = allItems.Skip(startIndex).Take(endIndex - startIndex).ToList();

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
                Items = roomItems,
                Color = GetCategoryColor(categoryIndex, categories.Count),
                Description = $"{category} - {roomItems.Count} items"
            });
        }

        return musicRooms;
    }

    private async Task<List<string>> GetMusicItemsForFloor(int floor)
    {
        var cacheKey = $"music:floor:{floor}:items";

        try
        {
            // Check Redis cache first
            var cached = await _cache.GetStringAsync(cacheKey);
            if (cached != null)
            {
                _logger.LogInformation("Cache hit for floor {Floor} items", floor);
                return JsonSerializer.Deserialize<List<string>>(cached) ?? [];
            }

            // Get items directly (no HTTP call needed - we're in the same process)
            _logger.LogInformation("Cache miss for floor {Floor} items, fetching from source", floor);
            var items = GetMusicItemsForFloorFallback(floor);

            if (items.Count > 0)
            {
                // Cache for 1 hour
                await _cache.SetStringAsync(cacheKey,
                    JsonSerializer.Serialize(items),
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
                    });
            }

            return items;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching music items for floor {Floor}, using fallback", floor);
            return GetMusicItemsForFloorFallback(floor);
        }
    }

    /// <summary>
    ///     Fallback method to get music items directly (used when API is unavailable)
    /// </summary>
    private List<string> GetMusicItemsForFloorFallback(int floor)
    {
        _logger.LogInformation("Using fallback data access for floor {Floor}", floor);
        return floor switch
        {
            0 => [.. SetClass.Items.Select(sc => sc.ToString())],
            1 => [.. ForteNumber.Items.Select(fn => fn.ToString())],
            2 => [.. SetClass.Items.Take(200).Select(sc => $"Prime: {sc}")],
            3 => [.. SetClass.Items.Take(350).Select(sc => $"Chord: {sc}")],
            4 => [.. SetClass.Items.Take(100).Select(sc => $"Inversion: {sc}")],
            5 => [.. SetClass.Items.Take(200).Select(sc => $"Voicing: {sc}")],
            _ => []
        };
    }

    private string GetCategoryColor(int index, int total)
    {
        var hue = index / (double)total * 360;
        return $"hsl({hue}, 70%, 50%)";
    }

    private class FloorMusicData
    {
        public string FloorName { get; set; } = "";
        public int TotalItems { get; set; }
        public List<string> Categories { get; set; } = [];
    }

    private class RoomParameters
    {
        public int MinRoomSize { get; set; }
        public int MaxRoomSize { get; set; }
        public int MaxDepth { get; set; }
    }
}
