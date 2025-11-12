namespace GA.Analytics.Service.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

[BsonIgnoreExtraElements]
public class Chord
{
    [BsonId] [BsonElement("_id")] public ObjectId MongoId { get; set; }

    [BsonElement("Id")] public int Id { get; set; }

    [BsonElement("Name")] public string Name { get; set; } = string.Empty;

    [BsonElement("Quality")] public string Quality { get; set; } = string.Empty;

    [BsonElement("Extension")] public string Extension { get; set; } = string.Empty;

    [BsonElement("StackingType")] public string StackingType { get; set; } = string.Empty;

    [BsonElement("NoteCount")] public int NoteCount { get; set; }

    [BsonElement("Intervals")] public BsonArray? Intervals { get; set; }

    [BsonElement("PitchClassSet")] public BsonArray? PitchClassSet { get; set; }

    [BsonElement("ParentScale")] public string? ParentScale { get; set; }

    [BsonElement("ScaleDegree")] public int? ScaleDegree { get; set; }

    [BsonElement("Description")] public string? Description { get; set; }

    [BsonElement("ConstructionType")] public string? ConstructionType { get; set; }

    [BsonElement("Embedding")] public double[]? Embedding { get; set; }

    [BsonElement("EmbeddingModel")] public string? EmbeddingModel { get; set; }
}
