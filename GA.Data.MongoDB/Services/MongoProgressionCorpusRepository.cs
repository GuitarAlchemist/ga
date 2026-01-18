namespace GA.Data.MongoDB.Services;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Tabs;
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
        // Simple serialization for spike: store chord IDs
        var entity = new BsonDocument
        {
            ["_id"] = string.IsNullOrEmpty(item.Id) ? ObjectId.GenerateNewId().ToString() : item.Id,
            ["style"] = item.StyleLabel,
            ["source"] = item.Source,
            ["chordIds"] = new BsonArray(item.Chords.Select(c => c.Id)),
            ["metadata"] = new BsonDocument(item.Metadata.ToDictionary(k => k.Key, v => (object)v.Value))
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
        return new ProgressionCorpusItem
        {
            Id = doc["_id"].ToString(),
            StyleLabel = doc["style"].AsString,
            Source = doc["source"].AsString,
            Metadata = doc["metadata"].AsBsonDocument.ToDictionary(k => k.Name, v => v.Value.ToString()),
            Chords = new List<GA.Business.Core.Fretboard.Voicings.Search.VoicingDocument>() // IDs only in this spike
        };
    }
}
