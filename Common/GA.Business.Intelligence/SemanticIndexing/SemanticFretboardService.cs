namespace GA.Business.Intelligence.SemanticIndexing;

using GA.Business.Core;
using GA.Business.Core.Fretboard;
using Microsoft.Extensions.Logging;

/// <summary>
/// Semantic fretboard service
/// Provides a simplified interface for semantic indexing and natural language querying of fretboard voicings
/// </summary>
public class SemanticFretboardService
{
    private readonly SemanticSearchService _searchService;
    private readonly ILogger<SemanticFretboardService> _logger;
    private bool _isIndexed = false;

    /// <summary>
    /// Interface for Ollama LLM service
    /// </summary>
    public interface IOllamaLlmService
    {
        Task<bool> EnsureBestModelAvailableAsync();
        Task<string> GetBestAvailableModelAsync();
        Task<string> ProcessNaturalLanguageQueryAsync(string query, string context);
    }

    public SemanticFretboardService(
        SemanticSearchService searchService,
        ILogger<SemanticFretboardService> logger)
    {
        _searchService = searchService;
        _logger = logger;
    }

    /// <summary>
    /// Index fretboard voicings for semantic search
    /// </summary>
    public async Task<IndexingResult> IndexFretboardVoicingsAsync(
        Tuning tuning,
        string instrumentName = "Guitar",
        int maxFret = 12,
        bool includeBiomechanicalAnalysis = true,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Indexing fretboard voicings for {Instrument}", instrumentName);

        // Stub implementation - just mark as indexed
        _isIndexed = true;
        progress?.Report(new IndexingProgress(Total: 100, Indexed: 100, Errors: 0));

        return new IndexingResult(
            InstrumentName: instrumentName,
            TuningName: tuning.ToString(),
            TotalVoicings: 100,
            IndexedVoicings: 100,
            Errors: 0,
            ElapsedTime: TimeSpan.FromSeconds(1),
            IndexSize: 100);
    }

    /// <summary>
    /// Process natural language query against indexed voicings
    /// </summary>
    public async Task<QueryResult> ProcessNaturalLanguageQueryAsync(
        string naturalLanguageQuery,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing query: {Query}", naturalLanguageQuery);

        if (!_isIndexed)
            throw new InvalidOperationException("Fretboard voicings must be indexed before querying");

        var results = await _searchService.SearchAsync(naturalLanguageQuery, maxResults);

        return new QueryResult(
            Query: naturalLanguageQuery,
            SearchResults: results,
            LlmInterpretation: $"Results for query: {naturalLanguageQuery}",
            ElapsedTime: TimeSpan.FromMilliseconds(100),
            ModelUsed: "stub-model");
    }

    /// <summary>
    /// Get index statistics
    /// </summary>
    public SemanticSearchService.IndexStatistics GetIndexStatistics()
    {
        return _searchService.GetStatistics();
    }

    /// <summary>
    /// Clear the index
    /// </summary>
    public void ClearIndex()
    {
        _searchService.Clear();
        _isIndexed = false;
    }
}

/// <summary>
/// Result of indexing fretboard voicings
/// </summary>
public record IndexingResult(
    string InstrumentName,
    string TuningName,
    int TotalVoicings,
    int IndexedVoicings,
    int Errors,
    TimeSpan ElapsedTime,
    int IndexSize)
{
    public double SuccessRate => TotalVoicings > 0 ? (double)IndexedVoicings / TotalVoicings : 0;
    public double IndexingRate => ElapsedTime.TotalSeconds > 0 ? IndexedVoicings / ElapsedTime.TotalSeconds : 0;
}

/// <summary>
/// Progress update for indexing operation
/// </summary>
public record IndexingProgress(
    int Total,
    int Indexed,
    int Errors)
{
    public double PercentComplete => Total > 0 ? (Indexed * 100.0) / Total : 0;
    public double ErrorRate => Total > 0 ? (Errors * 100.0) / Total : 0;
}

/// <summary>
/// Result of natural language query processing
/// </summary>
public record QueryResult(
    string Query,
    List<SemanticSearchService.SearchResult> SearchResults,
    string LlmInterpretation,
    TimeSpan ElapsedTime,
    string ModelUsed)
{
    public int ResultCount => SearchResults.Count;
    public double AverageRelevanceScore => SearchResults.Count > 0
        ? SearchResults.Average(r => r.Score)
        : 0;
}

