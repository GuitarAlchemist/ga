using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GA.Knowledge.Service.Models;

/// <summary>
/// Represents a room generation job in MongoDB
/// </summary>
public class RoomGenerationJob
{
    /// <summary>
    /// MongoDB document ID
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Unique job identifier
    /// </summary>
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// ID of the room being generated
    /// </summary>
    public string RoomId { get; set; } = string.Empty;

    /// <summary>
    /// Current status of the job
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>
    /// Job progress (0.0 to 1.0)
    /// </summary>
    public double Progress { get; set; }

    /// <summary>
    /// Generation parameters
    /// </summary>
    public GenerationParameters Parameters { get; set; } = new();

    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// When the job was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the job was started
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// When the job was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Job metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

/// <summary>
/// Job status enumeration
/// </summary>
public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// Parameters for room generation
/// </summary>
public class GenerationParameters
{
    /// <summary>
    /// Room type to generate
    /// </summary>
    public string RoomType { get; set; } = string.Empty;

    /// <summary>
    /// Target dimensions
    /// </summary>
    public RoomDimensions TargetDimensions { get; set; } = new();

    /// <summary>
    /// Generation algorithm to use
    /// </summary>
    public string Algorithm { get; set; } = "default";

    /// <summary>
    /// Additional parameters
    /// </summary>
    public Dictionary<string, object> AdditionalParameters { get; set; } = new();
}
