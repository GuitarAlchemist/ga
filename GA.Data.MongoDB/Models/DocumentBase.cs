using MongoDB.Bson;

namespace GA.Data.MongoDB.Models;

[PublicAPI]
public abstract record DocumentBase
{
    [BsonId]
    public ObjectId Id { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];

    // Parameterless constructor for MongoDB serialization
    protected DocumentBase() {}
}