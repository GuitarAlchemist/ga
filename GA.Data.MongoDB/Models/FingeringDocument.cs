using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class FingeringDocument : DocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("instrument")]
    public required string Instrument { get; set; }

    [BsonElement("positions")]
    public required int[] Positions { get; set; }

    [BsonElement("notes")]
    public required List<string> Notes { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; } // Scale, Chord, etc.

    [BsonElement("difficulty")]
    public string? Difficulty { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}