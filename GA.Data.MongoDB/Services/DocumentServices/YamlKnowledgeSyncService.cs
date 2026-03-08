namespace GA.Data.MongoDB.Services.DocumentServices;

using GA.Business.Config;
using GA.Data.MongoDB.Models.Rag;
using GA.Data.MongoDB.Services.Embeddings;
using Microsoft.Extensions.Logging;

/// <summary>
/// Ingests all content YAML files from GA.Business.Config into the
/// <c>yaml_knowledge</c> MongoDB collection as embedded RAG documents.
///
/// Each YAML entry (technique, progression, concept…) becomes one document
/// with a text embedding for semantic retrieval by the chatbot agents.
/// </summary>
[UsedImplicitly]
public class YamlKnowledgeSyncService(
    ILogger<YamlKnowledgeSyncService> logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService) : ISyncService<YamlKnowledgeDocument>
{
    public async Task<bool> SyncAsync()
    {
        try
        {
            var entries = YamlKnowledgeLoader.LoadAllKnowledgeEntries().ToList();
            logger.LogInformation("Loaded {Count} knowledge entries from YAML files", entries.Count);

            var documents = new List<YamlKnowledgeDocument>(entries.Count);
            var now = DateTime.UtcNow;

            foreach (var entry in entries)
            {
                var doc = new YamlKnowledgeDocument
                {
                    EntryName  = entry.Name,
                    Content    = entry.Content,
                    Category   = entry.Category,
                    SourceFile = entry.SourceFile,
                    Tags       = [.. entry.Tags],
                    CreatedAt  = now,
                    UpdatedAt  = now
                };

                doc.GenerateSearchText();

                try
                {
                    var embedding = await embeddingService.GenerateEmbeddingAsync(doc.SearchText);
                    doc.Embedding = [.. embedding];
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Embedding failed for '{Name}' in {File} — stored without embedding",
                        entry.Name, entry.SourceFile);
                }

                documents.Add(doc);
            }

            var collection = mongoDb.YamlKnowledge;
            await collection.DeleteManyAsync(Builders<YamlKnowledgeDocument>.Filter.Empty);
            await collection.InsertManyAsync(documents);

            logger.LogInformation(
                "Synced {Count} YAML knowledge documents into MongoDB", documents.Count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing YAML knowledge documents");
            return false;
        }
    }

    public async Task<long> GetCountAsync() =>
        await mongoDb.YamlKnowledge
            .CountDocumentsAsync(Builders<YamlKnowledgeDocument>.Filter.Empty);
}
