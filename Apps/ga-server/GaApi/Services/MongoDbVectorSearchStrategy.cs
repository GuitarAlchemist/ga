namespace GaApi.Services;

/// <summary>
///     MongoDB-based vector search strategy (existing implementation)
/// </summary>
public class MongoDbVectorSearchStrategy(
    VectorSearchService vectorSearchService,
    ILogger<MongoDbVectorSearchStrategy> logger)
    : IVectorSearchStrategy
{
    public string Name => "MongoDB";
    public bool IsAvailable => true; // MongoDB is always available if configured

    public VectorSearchPerformance Performance => new(
        TimeSpan.FromMilliseconds(50), // Network + DB overhead
        0, // Uses MongoDB's memory
        false,
        true);

    public async Task InitializeAsync(IEnumerable<ChordEmbedding> chords)
    {
        // MongoDB strategy doesn't need initialization - data is already in DB
        logger.LogInformation("MongoDB vector search strategy ready (using existing database)");
        await Task.CompletedTask;
    }

    public Task<List<ChordSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10,
        int numCandidates = 100) =>
        // Delegate to existing VectorSearchService
        // This would need to be adapted to work with the embedding directly
        throw new NotImplementedException("Adapt existing VectorSearchService.SemanticSearchAsync");

    public async Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100) =>
        await vectorSearchService.FindSimilarChordsAsync(chordId, limit, numCandidates);

    public Task<List<ChordSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        ChordSearchFilters filters,
        int limit = 10,
        int numCandidates = 100) =>
        // Adapt existing hybrid search
        throw new NotImplementedException("Adapt existing VectorSearchService.HybridSearchAsync");

    public VectorSearchStats GetStats() =>
        new(
            427254, // Known from database
            0, // MongoDB handles memory
            TimeSpan.FromMilliseconds(50), // Estimated
            0); // Would need to track this
}
