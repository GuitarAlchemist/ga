using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public class InstrumentDocument : DocumentBase
{
    [BsonElement("name")]
    public required string Name { get; set; }

    [BsonElement("category")]
    public required string Category { get; set; } = "Other";

    [BsonElement("stringCount")]
    public required int StringCount { get; set; }

    [BsonElement("tunings")]
    public List<TuningDocument> Tunings { get; set; } = [];

    [BsonElement("description")]
    public string? Description { get; set; }

    [BsonElement("family")]
    public string? Family { get; set; } // String, Wind, Percussion, etc.

    [BsonElement("range")]
    public string? Range { get; set; } // Musical range of the instrument
}