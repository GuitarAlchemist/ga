namespace GA.DocumentProcessing.Service.Services;

using GA.DocumentProcessing.Service.Models;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

public class MongoDbService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<ProcessedDocument> _documents;

    public MongoDbService(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);
        _documents = _database.GetCollection<ProcessedDocument>(settings.Value.CollectionName);

        // Create indexes
        CreateIndexes();
    }

    private void CreateIndexes()
    {
        // Text index for full-text search
        var textIndexKeys = Builders<ProcessedDocument>.IndexKeys
            .Text(d => d.SourceName)
            .Text(d => d.RawText)
            .Text(d => d.Summary);
        _documents.Indexes.CreateOne(new CreateIndexModel<ProcessedDocument>(textIndexKeys));

        // Index on status for filtering
        var statusIndexKeys = Builders<ProcessedDocument>.IndexKeys.Ascending(d => d.Status);
        _documents.Indexes.CreateOne(new CreateIndexModel<ProcessedDocument>(statusIndexKeys));

        // Index on tags for filtering
        var tagsIndexKeys = Builders<ProcessedDocument>.IndexKeys.Ascending(d => d.Tags);
        _documents.Indexes.CreateOne(new CreateIndexModel<ProcessedDocument>(tagsIndexKeys));

        // Index on created date
        var dateIndexKeys = Builders<ProcessedDocument>.IndexKeys.Descending(d => d.CreatedAt);
        _documents.Indexes.CreateOne(new CreateIndexModel<ProcessedDocument>(dateIndexKeys));
    }

    public IMongoCollection<ProcessedDocument> Documents => _documents;
    public IMongoDatabase Database => _database;
}

