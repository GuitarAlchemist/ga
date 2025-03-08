namespace GA.Data.MongoDB.Services.DocumentServices.Rag;

using Business.Core.Scales;
using Embeddings;
using Microsoft.Extensions.Logging;
using Models.Rag;

[UsedImplicitly]
public sealed class ChordRagSyncService(
    ILogger<ChordRagSyncService> logger,
    MongoDbService mongoDb,
    IEmbeddingService embeddingService)
    : ChordSyncService(logger, mongoDb), IRagSyncService<ChordRagEmbedding>
{
    private readonly MongoDbService _mongoDb = mongoDb;
    private readonly IEmbeddingService _embeddingService = embeddingService;

    public override async Task<bool> SyncAsync()
    {
        try
        {
            var documents = ChordTemplateFactory.CreateAllChordTemplates()
                .Select(template => new ChordRagEmbedding
                {
                    Name = template.PitchClassSet.Name,
                    Root = template.PitchClassSet.Notes.First().ToString(),
                    Quality = DetermineQuality(template),
                    Intervals = template.PitchClassSet.Notes
                        .Skip(1)
                        .Select(n => template.PitchClassSet.Notes.First().GetInterval(n).ToString())
                        .ToList(),
                    Notes = template.PitchClassSet.Notes.Select(n => n.ToString()).ToList(),
                    RelatedScales = GetRelatedScales(template),
                    CommonProgressions = GetCommonProgressions(template),
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                })
                .ToList();

            // Generate embeddings
            foreach (var doc in documents)
            {
                doc.GenerateSearchText();
                doc.Embedding = (await _embeddingService.GenerateEmbeddingAsync(doc.SearchText)).ToArray();
            }

            await _mongoDb.ChordsRag.DeleteManyAsync(Builders<ChordRagEmbedding>.Filter.Empty);
            await _mongoDb.ChordsRag.InsertManyAsync(documents);

            logger.LogInformation("Successfully synced {Count} chord RAG documents", documents.Count);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error syncing RAG chords");
            return false;
        }
    }

    public override async Task<long> GetCountAsync() =>
        await _mongoDb.ChordsRag.CountDocumentsAsync(Builders<ChordRagEmbedding>.Filter.Empty);
}
