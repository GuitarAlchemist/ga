namespace GA.BSP.Service.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

/// <summary>
///     MongoDB document for persisted music room layouts
/// </summary>
public class MusicRoomDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    ///     Floor number (0-5)
    /// </summary>
    [BsonElement("floor")]
    public int Floor { get; set; }

    /// <summary>
    ///     Floor name (e.g., "Set Classes", "Forte Codes")
    /// </summary>
    [BsonElement("floorName")]
    public string FloorName { get; set; } = "";

    /// <summary>
    ///     Size of the floor
    /// </summary>
    [BsonElement("floorSize")]
    public int FloorSize { get; set; }

    /// <summary>
    ///     Seed used for generation (for reproducibility)
    /// </summary>
    [BsonElement("seed")]
    public int? Seed { get; set; }

    /// <summary>
    ///     Total number of music items on this floor
    /// </summary>
    [BsonElement("totalItems")]
    public int TotalItems { get; set; }

    /// <summary>
    ///     Music categories on this floor
    /// </summary>
    [BsonElement("categories")]
    public List<string> Categories { get; set; } = [];

    /// <summary>
    ///     List of rooms
    /// </summary>
    [BsonElement("rooms")]
    public List<RoomData> Rooms { get; set; } = [];

    /// <summary>
    ///     List of corridors
    /// </summary>
    [BsonElement("corridors")]
    public List<CorridorData> Corridors { get; set; } = [];

    /// <summary>
    ///     Timestamp when this layout was generated
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Version of the generation algorithm
    /// </summary>
    [BsonElement("version")]
    public string Version { get; set; } = "1.0";

    /// <summary>
    ///     Generation parameters used
    /// </summary>
    [BsonElement("generationParams")]
    public GenerationParamsData GenerationParams { get; set; } = new();
}

/// <summary>
///     Room data for MongoDB storage
/// </summary>
public class RoomData
{
    [BsonElement("id")] public string Id { get; set; } = "";

    [BsonElement("x")] public int X { get; set; }

    [BsonElement("y")] public int Y { get; set; }

    [BsonElement("width")] public int Width { get; set; }

    [BsonElement("height")] public int Height { get; set; }

    [BsonElement("centerX")] public int CenterX { get; set; }

    [BsonElement("centerY")] public int CenterY { get; set; }

    [BsonElement("floor")] public int Floor { get; set; }

    [BsonElement("category")] public string Category { get; set; } = "";

    [BsonElement("items")] public List<string> Items { get; set; } = [];

    [BsonElement("color")] public string Color { get; set; } = "";

    [BsonElement("description")] public string Description { get; set; } = "";
}

/// <summary>
///     Corridor data for MongoDB storage
/// </summary>
public class CorridorData
{
    [BsonElement("points")] public List<PointData> Points { get; set; } = [];

    [BsonElement("width")] public int Width { get; set; }
}

/// <summary>
///     Point data for MongoDB storage
/// </summary>
public class PointData
{
    [BsonElement("x")] public float X { get; set; }

    [BsonElement("y")] public float Y { get; set; }
}

/// <summary>
///     Generation parameters for MongoDB storage
/// </summary>
public class GenerationParamsData
{
    [BsonElement("minRoomSize")] public int MinRoomSize { get; set; }

    [BsonElement("maxRoomSize")] public int MaxRoomSize { get; set; }

    [BsonElement("maxDepth")] public int MaxDepth { get; set; }

    [BsonElement("corridorWidth")] public int CorridorWidth { get; set; }
}

/// <summary>
///     Room generation job for queuing
/// </summary>
public class RoomGenerationJob
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    ///     Floor number to generate
    /// </summary>
    [BsonElement("floor")]
    public int Floor { get; set; }

    /// <summary>
    ///     Floor size
    /// </summary>
    [BsonElement("floorSize")]
    public int FloorSize { get; set; }

    /// <summary>
    ///     Optional seed
    /// </summary>
    [BsonElement("seed")]
    public int? Seed { get; set; }

    /// <summary>
    ///     Job status
    /// </summary>
    [BsonElement("status")]
    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>
    ///     When the job was created
    /// </summary>
    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     When the job started processing
    /// </summary>
    [BsonElement("startedAt")]
    public DateTime? StartedAt { get; set; }

    /// <summary>
    ///     When the job completed
    /// </summary>
    [BsonElement("completedAt")]
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    ///     Error message if failed
    /// </summary>
    [BsonElement("error")]
    public string? Error { get; set; }

    /// <summary>
    ///     Reference to the generated room layout
    /// </summary>
    [BsonElement("resultId")]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? ResultId { get; set; }

    /// <summary>
    ///     Processing time in milliseconds
    /// </summary>
    [BsonElement("processingTimeMs")]
    public long? ProcessingTimeMs { get; set; }
}

/// <summary>
///     Job status enumeration
/// </summary>
public enum JobStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}
