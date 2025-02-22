using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class ArpeggioDocument : DocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("root")]
    public required string Root { get; set; }

    [BsonElement("intervals")]
    public required List<string> Intervals { get; set; }

    [BsonElement("notes")]
    public required List<string> Notes { get; set; }

    [BsonElement("category")]
    public string? Category { get; set; } // Major, Minor, Diminished, etc.

    [BsonElement("description")]
    public string? Description { get; set; }
}