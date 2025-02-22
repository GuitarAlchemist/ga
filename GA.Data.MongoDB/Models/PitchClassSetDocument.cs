using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

public class PitchClassSetDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public int Value { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Cardinality { get; set; }
    public List<int> PitchClasses { get; set; } = new();
    public List<string> Notes { get; set; } = new();
    public string IntervalClassVector { get; set; } = string.Empty;
    public string? ModalFamily { get; set; }
    public bool IsModal { get; set; }
    public bool IsNormalForm { get; set; }
    public bool IsClusterFree { get; set; }
    public string? ScaleVideoUrl { get; set; }
    public string ScalePageUrl { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}