namespace GA.Data.MongoDB.Services;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Domain.Repositories;
using GA.Domain.Core.Theory.Harmony.Progressions;
using GA.Data.MongoDB.Models;
using global::MongoDB.Bson;
using global::MongoDB.Driver;

public class MongoProgressionCorpusRepository : IProgressionCorpusRepository
{
    private readonly MongoDbService _db;

    public MongoProgressionCorpusRepository(MongoDbService db)
    {
        _db = db;
    }

    public async Task SaveAsync(ProgressionCorpusItem item)
    {
        var now = DateTime.UtcNow;
        var id = string.IsNullOrWhiteSpace(item.Id) ? ObjectId.GenerateNewId().ToString() : item.Id;
        var createdAt = item.CreatedAt == default ? now : item.CreatedAt;

        // Simple serialization: store chord IDs (placeholder logic preserved)
        var entity = new BsonDocument
        {
            ["_id"] = id,
            ["style"] = item.StyleLabel,
            ["source"] = item.Source,
            ["chordIds"] = new BsonArray(), // placeholder for proper mapping
            ["metadata"] = new BsonDocument(item.Metadata.ToDictionary(k => k.Key, v => (object)v.Value)),
            ["createdAt"] = createdAt,
            ["updatedAt"] = now
        };

        var collection = _db.Database.GetCollection<BsonDocument>("progression_corpus");
        await collection.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", entity["_id"]), 
            entity, 
            new ReplaceOptions { IsUpsert = true });
    }

    public async Task<IEnumerable<ProgressionCorpusItem>> GetByStyleAsync(string style)
    {
        var collection = _db.Database.GetCollection<BsonDocument>("progression_corpus");
        var filter = Builders<BsonDocument>.Filter.Eq("style", style);
        var docs = await collection.Find(filter).ToListAsync();
        return docs.Select(MapToItem);
    }

    public async Task<IEnumerable<ProgressionCorpusItem>> GetAllAsync()
    {
        var collection = _db.Database.GetCollection<BsonDocument>("progression_corpus");
        var docs = await collection.Find(_ => true).ToListAsync();
        return docs.Select(MapToItem);
    }

    public async Task<long> CountAsync()
    {
        var collection = _db.Database.GetCollection<BsonDocument>("progression_corpus");
        return await collection.CountDocumentsAsync(_ => true);
    }

    private ProgressionCorpusItem MapToItem(BsonDocument doc)
    {
        var now = DateTime.UtcNow;
        var createdAt = doc.TryGetValue("createdAt", out var createdAtValue) && createdAtValue.IsBsonDateTime
            ? createdAtValue.AsBsonDateTime.ToUniversalTime()
            : now;
        var updatedAt = doc.TryGetValue("updatedAt", out var updatedAtValue) && updatedAtValue.IsBsonDateTime
            ? updatedAtValue.AsBsonDateTime.ToUniversalTime()
            : createdAt;

        var metadata = doc.TryGetValue("metadata", out var metadataValue) && metadataValue.IsBsonDocument
            ? metadataValue.AsBsonDocument.ToDictionary(k => k.Name, v => v.Value.ToString())
            : new Dictionary<string, string>();

        return new ProgressionCorpusItem
        {
            Id = doc.GetValue("_id", ObjectId.GenerateNewId().ToString()).ToString(),
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
            StyleLabel = doc.GetValue("style", string.Empty).AsString,
            Source = doc.GetValue("source", string.Empty).AsString,
            Metadata = metadata,
            Chords = [] // Empty for now to satisfy domain model
        };
    }
}
