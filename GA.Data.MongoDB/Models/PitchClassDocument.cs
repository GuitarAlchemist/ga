namespace GA.Data.MongoDB.Models;

using global::MongoDB.Bson.Serialization.Attributes;
using JetBrains.Annotations;

[PublicAPI]
public class PitchClassDocument : DocumentBase
{
    [BsonElement("value")]
    public required int Value { get; set; }
    
    [BsonElement("name")]
    public required string Name { get; set; }
    
    [BsonElement("notes")]
    public required List<string> Notes { get; set; }
}