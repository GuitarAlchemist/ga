using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

using MongoDB.Driver;

namespace GA.Data.MongoDB.Models;

public class InstrumentDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("tunings")]
    public List<TuningDocument> Tunings { get; set; } = [];

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = [];

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}

public record SyncResult(
    int NotesAdded,
    int IntervalsAdded,
    int KeysAdded,
    int ScalesAdded,
    int PitchClassesAdded,
    List<string> Errors
);