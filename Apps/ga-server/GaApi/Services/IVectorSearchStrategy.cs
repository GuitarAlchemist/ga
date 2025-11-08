namespace GaApi.Services;

/// <summary>
///     Strategy interface for different vector search implementations
/// </summary>
public interface IVectorSearchStrategy
{
    /// <summary>
    ///     Gets the name of this search strategy
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     Gets whether this strategy is available on the current system
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    ///     Gets the expected performance characteristics
    /// </summary>
    VectorSearchPerformance Performance { get; }

    /// <summary>
    ///     Initialize the strategy with chord data
    /// </summary>
    Task InitializeAsync(IEnumerable<ChordEmbedding> chords);

    /// <summary>
    ///     Perform semantic search using natural language query
    /// </summary>
    Task<List<ChordSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10,
        int numCandidates = 100);

    /// <summary>
    ///     Find chords similar to a specific chord
    /// </summary>
    Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100);

    /// <summary>
    ///     Hybrid search with filters
    /// </summary>
    Task<List<ChordSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        ChordSearchFilters filters,
        int limit = 10,
        int numCandidates = 100);

    /// <summary>
    ///     Get memory usage statistics
    /// </summary>
    VectorSearchStats GetStats();
}

/// <summary>
///     Performance characteristics of a vector search strategy
/// </summary>
public record VectorSearchPerformance(
    TimeSpan ExpectedSearchTime,
    long MemoryUsageMb,
    bool RequiresGpu,
    bool RequiresNetwork);

/// <summary>
///     Search filters for hybrid search
/// </summary>
public record ChordSearchFilters(
    string? Quality = null,
    string? Extension = null,
    string? StackingType = null,
    int? NoteCount = null);

/// <summary>
///     Vector search statistics
/// </summary>
public record VectorSearchStats(
    long TotalChords,
    long MemoryUsageMb,
    TimeSpan AverageSearchTime,
    long TotalSearches);

/// <summary>
///     Chord with embedding data
/// </summary>
public record ChordEmbedding(
    int Id,
    string Name,
    string Quality,
    string Extension,
    string StackingType,
    int NoteCount,
    string Description,
    double[] Embedding);
