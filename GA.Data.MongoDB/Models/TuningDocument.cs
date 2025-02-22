namespace GA.Data.MongoDB.Models;

using global::MongoDB.Bson.Serialization.Attributes;

[PublicAPI]
public class TuningDocument : DocumentBase
{
    [BsonElement("name")]
    public string Name { get; set; } = null!;

    [BsonElement("notes")]
    public List<string> Notes { get; set; } = [];

    [BsonElement("isStandard")]
    public bool IsStandard { get; set; }

    [BsonElement("description")]
    public string? Description { get; set; }
}