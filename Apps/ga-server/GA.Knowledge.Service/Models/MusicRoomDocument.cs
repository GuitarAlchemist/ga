using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GA.Knowledge.Service.Models;

/// <summary>
/// Represents a music room layout document in MongoDB
/// </summary>
public class MusicRoomDocument
{
    /// <summary>
    /// MongoDB document ID
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Unique room identifier
    /// </summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// Name of the room
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Room description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Room dimensions (width, height, depth)
    /// </summary>
    public RoomDimensions Dimensions { get; set; } = new();

    /// <summary>
    /// Layout configuration
    /// </summary>
    public RoomLayout Layout { get; set; } = new();

    /// <summary>
    /// Acoustic properties
    /// </summary>
    public AcousticProperties Acoustics { get; set; } = new();

    /// <summary>
    /// When the room was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the room was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Room metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Room dimensions
/// </summary>
public class RoomDimensions
{
    public double Width { get; set; }
    public double Height { get; set; }
    public double Depth { get; set; }
}

/// <summary>
/// Room layout configuration
/// </summary>
public class RoomLayout
{
    public string LayoutType { get; set; } = string.Empty;
    public List<RoomObject> Objects { get; set; } = new();
}

/// <summary>
/// Object in the room
/// </summary>
public class RoomObject
{
    public string Type { get; set; } = string.Empty;
    public Position Position { get; set; } = new();
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// 3D position
/// </summary>
public class Position
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Z { get; set; }
}

/// <summary>
/// Acoustic properties of the room
/// </summary>
public class AcousticProperties
{
    public double ReverbTime { get; set; }
    public double Absorption { get; set; }
    public double Reflection { get; set; }
}
