using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class ChordDocument : DocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("root")]
    public required string Root { get; set; }

    [BsonElement("quality")]
    public required string Quality { get; set; } // Major, Minor, Diminished, etc.

    [BsonElement("intervals")]
    public required List<string> Intervals { get; set; }

    [BsonElement("notes")]
    public required List<string> Notes { get; set; }

    [BsonElement("voicings")]
    public List<int[]> Voicings { get; set; } = []; // Guitar fret positions

    [BsonElement("category")]
    public string? Category { get; set; } // Triad, Seventh, Extended, etc.

    [BsonElement("description")]
    public string? Description { get; set; }
}