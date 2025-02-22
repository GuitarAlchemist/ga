using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace GA.Data.MongoDB.Models;

public abstract class DocumentBase
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}