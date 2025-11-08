namespace FloorManager.Services;

using System.Text.Json;

public class FloorService(IHttpClientFactory httpClientFactory, ILogger<FloorService> logger)
{
    public async Task<FloorData?> GetFloorAsync(int floorNumber, int floorSize = 80, int? seed = null)
    {
        try
        {
            var client = httpClientFactory.CreateClient("GaApi");
            var url = $"/api/music-rooms/floor/{floorNumber}?floorSize={floorSize}";
            if (seed.HasValue)
            {
                url += $"&seed={seed.Value}";
            }

            logger.LogInformation("Fetching floor {FloorNumber} from {Url}", floorNumber, url);

            var response = await client.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var apiResponse = JsonSerializer.Deserialize<ApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return apiResponse?.Data;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching floor {FloorNumber}", floorNumber);
            return null;
        }
    }

    public async Task<List<FloorData>> GetAllFloorsAsync(int floorSize = 80, int? seed = null)
    {
        var floors = new List<FloorData>();
        for (var i = 0; i < 6; i++)
        {
            var floor = await GetFloorAsync(i, floorSize, seed);
            if (floor != null)
            {
                floors.Add(floor);
            }
        }

        return floors;
    }
}

public class ApiResponse
{
    public bool Success { get; set; }
    public FloorData? Data { get; set; }
    public string? Message { get; set; }
}

public class FloorData
{
    public int Floor { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public int FloorSize { get; set; }
    public int TotalItems { get; set; }
    public List<string> Categories { get; set; } = [];
    public List<Room> Rooms { get; set; } = [];
    public List<Corridor> Corridors { get; set; } = [];
    public int? Seed { get; set; }

    // Computed properties for backward compatibility
    public int Width => FloorSize;
    public int Height => FloorSize;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    public MusicData? MusicData => new()
    {
        FloorName = FloorName,
        TotalItems = TotalItems,
        TargetRooms = Rooms.Count
    };
}

public class Room
{
    public string Id { get; set; } = string.Empty;
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public int Floor { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Items { get; set; } = [];
    public string Color { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Computed property for backward compatibility
    public MusicItem? MusicItem => Items.Count > 0
        ? new MusicItem
        {
            Name = Items[0],
            Category = Category,
            Description = Description
        }
        : null;
}

public class Corridor
{
    public int Width { get; set; }
    public List<Point> Points { get; set; } = [];

    // Computed properties for backward compatibility
    public Point Start => Points.Count > 0 ? Points[0] : new Point();
    public Point End => Points.Count > 1 ? Points[Points.Count - 1] : new Point();
}

public class Point
{
    public float X { get; set; }
    public float Y { get; set; }
}

public class MusicData
{
    public string FloorName { get; set; } = string.Empty;
    public int TotalItems { get; set; }
    public int TargetRooms { get; set; }
}

public class MusicItem
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<int> PitchClasses { get; set; } = [];
    public string Description { get; set; } = string.Empty;
}
