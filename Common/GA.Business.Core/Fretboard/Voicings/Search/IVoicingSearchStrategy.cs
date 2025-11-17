namespace GA.Business.Core.Fretboard.Voicings.Search;

using Core;

/// <summary>
/// Strategy interface for different voicing search implementations
/// </summary>
public interface IVoicingSearchStrategy
{
    /// <summary>
    /// Gets the name of this search strategy
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets whether this strategy is available on the current system
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Gets the expected performance characteristics
    /// </summary>
    VoicingSearchPerformance Performance { get; }

    /// <summary>
    /// Initialize the strategy with voicing data
    /// </summary>
    Task InitializeAsync(IEnumerable<VoicingEmbedding> voicings);

    /// <summary>
    /// Perform semantic search using natural language query
    /// </summary>
    Task<List<VoicingSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10);

    /// <summary>
    /// Find voicings similar to a specific voicing
    /// </summary>
    Task<List<VoicingSearchResult>> FindSimilarVoicingsAsync(
        string voicingId,
        int limit = 10);

    /// <summary>
    /// Hybrid search with filters
    /// </summary>
    Task<List<VoicingSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        VoicingSearchFilters filters,
        int limit = 10);

    /// <summary>
    /// Get memory usage statistics
    /// </summary>
    VoicingSearchStats GetStats();
}

