namespace GA.Data.MongoDB.Models;

[PublicAPI]
public abstract record DocumentBase
{
    // Parameterless constructor for MongoDB serialization

    [BsonId] public ObjectId Id { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public Dictionary<string, string> Metadata { get; init; } = [];
}
