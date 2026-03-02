namespace GA.Data.MongoDB.Services;

using GA.Domain.Repositories;
using GA.Domain.Core.Theory.Tabs;
using GA.Data.MongoDB.Models;
using global::MongoDB.Bson;
using global::MongoDB.Driver;
using global::System.Collections.Generic;
using global::System.Linq;
using global::System.Threading.Tasks;

public class MongoTabCorpusRepository : ITabCorpusRepository
{
    private readonly MongoDbService _db;

    public MongoTabCorpusRepository(MongoDbService db)
    {
        _db = db;
    }

    public async Task SaveAsync(TabCorpusItem item)
    {
        var now = DateTime.UtcNow;
        var id = string.IsNullOrWhiteSpace(item.Id) ? ObjectId.GenerateNewId().ToString() : item.Id;
        var createdAt = item.CreatedAt == default ? now : item.CreatedAt;

        var entity = new TabCorpusDocument
        {
            Id = id,
            SourceId = item.SourceId,
            ExternalId = item.ExternalId,
            Content = item.Content,
            Format = item.Format,
            CreatedAt = createdAt,
            UpdatedAt = now,
            Metadata = item.Metadata.ToDictionary(k => k.Key, v => (object)v.Value)
        };

        FilterDefinition<TabCorpusDocument> filter;
        if (!string.IsNullOrEmpty(item.Id))
        {
            filter = Builders<TabCorpusDocument>.Filter.Eq(x => x.Id, item.Id);
        }
        else
        {
             filter = Builders<TabCorpusDocument>.Filter.And(
                Builders<TabCorpusDocument>.Filter.Eq(x => x.SourceId, item.SourceId),
                Builders<TabCorpusDocument>.Filter.Eq(x => x.ExternalId, item.ExternalId)
            );
        }

        await _db.Tabs.ReplaceOneAsync(filter, entity, new ReplaceOptions { IsUpsert = true });
    }

    public async Task<TabCorpusItem?> GetByIdAsync(string id)
    {
        var entity = await _db.Tabs.Find(x => x.Id == id).FirstOrDefaultAsync();
        return entity == null ? null : MapToItem(entity);
    }

    public async Task<IEnumerable<TabCorpusItem>> GetAllAsync()
    {
        // Limit to reasonable number or just return all? For corpus, generic GetAll is dangerous. 
        // But requested by interface.
        var entities = await _db.Tabs.Find(_ => true).Limit(1000).ToListAsync(); 
        return entities.Select(MapToItem);
    }

    public async Task<bool> ExistsAsync(string id)
    {
        return await _db.Tabs.Find(x => x.Id == id).AnyAsync();
    }

    public async Task<long> CountAsync()
    {
        return await _db.Tabs.CountDocumentsAsync(_ => true);
    }

    private TabCorpusItem MapToItem(TabCorpusDocument entity)
    {
        var now = DateTime.UtcNow;

        return new TabCorpusItem
        {
            Id = entity.Id,
            CreatedAt = entity.CreatedAt == default ? now : entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt == default ? now : entity.UpdatedAt,
            SourceId = entity.SourceId,
            ExternalId = entity.ExternalId,
            Content = entity.Content,
            Format = entity.Format,
            Metadata = entity.Metadata.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? "")
        };
    }
}
