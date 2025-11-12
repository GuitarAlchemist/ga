namespace GA.Business.Core.Fretboard.Voicings.Search;

using System.Diagnostics;
using Core;
using Microsoft.Extensions.Logging;

/// <summary>
/// Enhanced voicing search service with support for multiple search strategies
/// including GPU-accelerated ILGPU and in-memory search
/// </summary>
public class EnhancedVoicingSearchService
{
    private readonly ILogger<EnhancedVoicingSearchService> _logger;
    private readonly VoicingIndexingService _indexingService;
    private readonly IVoicingSearchStrategy _searchStrategy;
    private bool _isInitialized;

    public EnhancedVoicingSearchService(
        ILogger<EnhancedVoicingSearchService> logger,
        VoicingIndexingService indexingService,
        IVoicingSearchStrategy searchStrategy)
    {
        _logger = logger;
        _indexingService = indexingService;
        _searchStrategy = searchStrategy;
    }

    /// <summary>
    /// Gets the name of the current search strategy
    /// </summary>
    public string StrategyName => _searchStrategy.Name;

    /// <summary>
    /// Gets whether the search strategy is available
    /// </summary>
    public bool IsAvailable => _searchStrategy.IsAvailable;

    /// <summary>
    /// Gets whether the service is initialized
    /// </summary>
    public bool IsInitialized => _isInitialized;

    /// <summary>
    /// Gets the number of indexed documents
    /// </summary>
    public int DocumentCount => _indexingService.DocumentCount;

    /// <summary>
    /// Gets performance statistics
    /// </summary>
    public VoicingSearchPerformance Performance => _searchStrategy.Performance;

    /// <summary>
    /// Initialize the service with embeddings for all indexed documents
    /// </summary>
    /// <param name="embeddingGenerator">Function to generate embeddings from text</param>
    public async Task InitializeEmbeddingsAsync(
        Func<string, Task<double[]>> embeddingGenerator,
        CancellationToken cancellationToken = default)
    {
        if (!_searchStrategy.IsAvailable)
            throw new InvalidOperationException($"Search strategy '{_searchStrategy.Name}' is not available");

        _logger.LogInformation("Initializing voicing embeddings using {Strategy} strategy...", _searchStrategy.Name);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var documents = _indexingService.Documents;
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

            _logger.LogInformation("Loaded {Count} pre-generated embeddings", voicingEmbeddings.Count);

            await _searchStrategy.InitializeAsync(voicingEmbeddings);
            _isInitialized = true;

            stopwatch.Stop();
            _logger.LogInformation(
                "Initialized {Count} voicing embeddings using {Strategy} in {ElapsedMs}ms",
                voicingEmbeddings.Count, _searchStrategy.Name, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize voicing embeddings");
            throw;
        }
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

        _logger.LogInformation("Searching voicings for query: {Query}", query);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var queryEmbedding = await embeddingGenerator(query);

            List<VoicingSearchResult> results;
            if (filters != null)
            {
                results = await _searchStrategy.HybridSearchAsync(queryEmbedding, filters, topK);
            }
            else
            {
                results = await _searchStrategy.SemanticSearchAsync(queryEmbedding, topK);
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "Found {Count} results for query '{Query}' in {ElapsedMs}ms using {Strategy}",
                results.Count, query, stopwatch.ElapsedMilliseconds, _searchStrategy.Name);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching voicings for query: {Query}", query);
            throw;
        }
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

        _logger.LogInformation("Finding voicings similar to: {VoicingId}", voicingId);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var results = await _searchStrategy.FindSimilarVoicingsAsync(voicingId, topK);

            stopwatch.Stop();
            _logger.LogInformation(
                "Found {Count} similar voicings to '{VoicingId}' in {ElapsedMs}ms using {Strategy}",
                results.Count, voicingId, stopwatch.ElapsedMilliseconds, _searchStrategy.Name);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding similar voicings for: {VoicingId}", voicingId);
            throw;
        }
    }

    /// <summary>
    /// Get search statistics
    /// </summary>
    public VoicingSearchStats GetStats()
    {
        return _searchStrategy.GetStats();
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
            throw new InvalidOperationException("Service not initialized. Call InitializeEmbeddingsAsync first.");
    }
}

