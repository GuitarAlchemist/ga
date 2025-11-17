namespace GA.Business.Core.Fretboard.Voicings.Search;

using Core;

/// <summary>
/// Enhanced voicing search service with support for multiple search strategies
/// including GPU-accelerated ILGPU and in-memory search
/// </summary>
public class EnhancedVoicingSearchService(
    VoicingIndexingService indexingService,
    IVoicingSearchStrategy searchStrategy)
{
    /// <summary>
    /// Gets the name of the current search strategy
    /// </summary>
    public string StrategyName => searchStrategy.Name;

    /// <summary>
    /// Gets whether the search strategy is available
    /// </summary>
    public bool IsAvailable => searchStrategy.IsAvailable;

    /// <summary>
    /// Gets whether the service is initialized
    /// </summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// Gets the number of indexed documents
    /// </summary>
    public int DocumentCount => indexingService.DocumentCount;

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    public VoicingSearchPerformance Performance => searchStrategy.Performance;

    /// <summary>
    /// Initialize the service with embeddings for all indexed documents
    /// </summary>
    /// <param name="embeddingGenerator">Function to generate embeddings from text</param>
    public async Task InitializeEmbeddingsAsync(
        Func<string, Task<double[]>> embeddingGenerator,
        CancellationToken cancellationToken = default)
    {
        if (!searchStrategy.IsAvailable)
            throw new InvalidOperationException($"Search strategy '{searchStrategy.Name}' is not available");

        var documents = indexingService.Documents;
        var voicingEmbeddings = new List<VoicingEmbedding>(documents.Count);

        // Process all embeddings in parallel for maximum speed
        var embeddingTasks = documents.Select(async doc =>
        {
            var embedding = await embeddingGenerator(doc.SearchableText);

            return new VoicingEmbedding(
                doc.Id,
                doc.ChordName ?? "Unknown",
                doc.VoicingType,
                doc.Position,
                doc.Difficulty,
                doc.ModeName,
                doc.ModalFamily,
                doc.SemanticTags,
                doc.PrimeFormId ?? "",
                doc.TranslationOffset,
                doc.Diagram,
                doc.MidiNotes,
                doc.PitchClassSet,
                doc.IntervalClassVector,
                doc.MinFret,
                doc.MaxFret,
                doc.BarreRequired,
                doc.HandStretch,
                doc.YamlAnalysis,
                embedding);
        }).ToList();

        // Wait for all embeddings to complete
        var results = await Task.WhenAll(embeddingTasks);
        voicingEmbeddings.AddRange(results);

        await searchStrategy.InitializeAsync(voicingEmbeddings);
        IsInitialized = true;
    }

    /// <summary>
    /// Search for voicings using natural language query
    /// </summary>
    public async Task<List<VoicingSearchResult>> SearchAsync(
        string query,
        Func<string, Task<double[]>> embeddingGenerator,
        int topK = 10,
        VoicingSearchFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        var queryEmbedding = await embeddingGenerator(query);

        if (filters != null)
        {
            return await searchStrategy.HybridSearchAsync(queryEmbedding, filters, topK);
        }

        return await searchStrategy.SemanticSearchAsync(queryEmbedding, topK);
    }

    /// <summary>
    /// Find voicings similar to a given voicing
    /// </summary>
    public async Task<List<VoicingSearchResult>> FindSimilarAsync(
        string voicingId,
        int topK = 10,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();

        return await searchStrategy.FindSimilarVoicingsAsync(voicingId, topK);
    }

    /// <summary>
    /// Get search statistics
    /// </summary>
    public VoicingSearchStats GetStats()
    {
        return searchStrategy.GetStats();
    }

    private void EnsureInitialized()
    {
        if (!IsInitialized)
            throw new InvalidOperationException("Service not initialized. Call InitializeEmbeddingsAsync first.");
    }
}
