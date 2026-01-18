namespace GA.Data.MongoDB.Models;

using global::MongoDB.Bson;
using global::MongoDB.Bson.Serialization.Attributes;
using global::System.Collections.Generic;

public class TabEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    
    [BsonExtraElements]
    public Dictionary<string, object> Metadata { get; set; } = new();
}
