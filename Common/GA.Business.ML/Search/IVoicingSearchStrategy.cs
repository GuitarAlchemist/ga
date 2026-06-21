namespace GA.Business.ML.Search;

using GA.Domain.Services.Fretboard.Voicings.Core;

/// <summary>
///     Describes the vector space a strategy's query parameter inhabits.
///     Callers dispatch on this to pick the right query encoder (musical vs text).
/// </summary>
public enum QueryVectorSpace
{
    /// <summary>
    ///     Pre-weighted + L2-normalized OPTK v4 compact 112-dim vector. Produced by
    ///     <c>MusicalQueryEncoder</c>. Dot product over on-disk vectors is cosine similarity.
    /// </summary>
    OpticCompact112,

    /// <summary>
    ///     Raw text embedding (768-dim nomic, 384-dim MiniLM, or similar). Produced by
    ///     an <c>ITextEmbeddingService</c>. Used by the legacy CPU/GPU strategies.
    /// </summary>
    TextEmbedding,
}

/// <summary>
///     Strategy interface for different voicing search implementations
/// </summary>
public interface IVoicingSearchStrategy
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
    ///     Declares the query-vector space this strategy expects in <see cref="SemanticSearchAsync"/>
    ///     and <see cref="HybridSearchAsync"/>. Callers select their encoder based on this value
    ///     rather than inspecting <see cref="Name"/>.
    /// </summary>
    QueryVectorSpace QuerySpace { get; }

    /// <summary>
    ///     Gets the expected performance characteristics
    /// </summary>
    VoicingSearchPerformance Performance { get; }

    /// <summary>
    ///     Whether this strategy needs <see cref="InitializeAsync"/> / the host warmup before
    ///     it can serve queries. Strategies that load their corpus at construction (e.g. the
    ///     OPTK mmap reader) are ready immediately and return <see langword="false"/>, so the
    ///     service must NOT gate their queries behind a warmup that does nothing for them —
    ///     that gating is the cold-start "Service not initialized" race. Defaults to true so
    ///     existing in-memory strategies (CPU/GPU) keep their initialize-then-serve contract.
    /// </summary>
    bool RequiresWarmup => true;

    /// <summary>
    ///     Initialize the strategy with voicing data
    /// </summary>
    Task InitializeAsync(IEnumerable<VoicingEmbedding> voicings);

    /// <summary>
    ///     Perform semantic search using natural language query
    /// </summary>
    Task<List<VoicingSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Find voicings similar to a specific voicing
    /// </summary>
    Task<List<VoicingSearchResult>> FindSimilarVoicingsAsync(
        string voicingId,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Hybrid search with filters
    /// </summary>
    Task<List<VoicingSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        VoicingSearchFilters filters,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Get memory usage statistics
    /// </summary>
    VoicingSearchStats GetStats();

    /// <summary>
    ///     The filter fields populated on <paramref name="filters"/> that this strategy structurally
    ///     <b>cannot</b> honor — its corpus lacks the backing metadata, so it will silently ignore them.
    ///     Returns field names (e.g. <c>"Difficulty"</c>, <c>"Tags"</c>). Empty for the CPU/GPU
    ///     strategies, which load the full <see cref="VoicingEmbedding"/> and cross
    ///     <see cref="VoicingFilterEngine"/> (every metadata field) plus the shared comfort seam.
    ///     OPTK-mmap overrides this to return its index-bound complement. Consumed by the caller-side
    ///     telemetry writer (the <c>dropped</c> field) so the reduced filter set is observable rather
    ///     than silent — see <c>docs/adr/0002-voicing-filter-parity-cpu-gpu-only.md</c>.
    /// </summary>
    IReadOnlyList<string> UnsupportedPopulatedFilters(VoicingSearchFilters filters) => [];
}
