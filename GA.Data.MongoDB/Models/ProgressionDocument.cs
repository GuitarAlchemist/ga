using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class ProgressionDocument : DocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("key")]
    public required string Key { get; set; }

    [BsonElement("chords")]
    public List<string> Chords { get; set; } = [];

    [BsonElement("romanNumerals")]
    public List<string> RomanNumerals { get; set; } = [];

    [BsonElement("category")]
    public required string Category { get; set; } // e.g., "Jazz", "Blues", "Pop"

    [BsonElement("description")]
    public string? Description { get; set; }
}