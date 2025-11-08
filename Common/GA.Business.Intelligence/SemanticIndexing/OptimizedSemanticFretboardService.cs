namespace GA.Business.Intelligence.SemanticIndexing;

using GA.Business.Core;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Primitives;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Threading.Channels;
using GA.Data.SemanticKernel.Embeddings;

/// <summary>
/// High-performance semantic fretboard service with parallel processing, batching, and caching
/// Optimized for speed with concurrent embedding generation and intelligent deduplication
/// </summary>
public class OptimizedSemanticFretboardService
{
    private readonly SemanticSearchService _searchService;
    private readonly IBatchEmbeddingService _batchEmbeddingService;
    private readonly SemanticFretboardService.IOllamaLlmService _llmService;
    private readonly ILogger<OptimizedSemanticFretboardService> _logger;
    private readonly Dictionary<string, Fretboard> _fretboards = new();
    private readonly ConcurrentDictionary<string, float[]> _embeddingCache = new();
    private bool _isIndexed = false;

    // Performance tuning parameters
    private readonly int _maxConcurrency;
    private readonly int _batchSize;
    private readonly bool _enableCaching;

    public OptimizedSemanticFretboardService(
        SemanticSearchService searchService,
        IBatchEmbeddingService batchEmbeddingService,
        SemanticFretboardService.IOllamaLlmService llmService,
        ILogger<OptimizedSemanticFretboardService> logger,
        OptimizationOptions? options = null)
    {
        _searchService = searchService;
        _batchEmbeddingService = batchEmbeddingService;
        _llmService = llmService;
        _logger = logger;

        var opts = options ?? new OptimizationOptions();
        _maxConcurrency = opts.MaxConcurrency == -1 ? Environment.ProcessorCount : opts.MaxConcurrency;
        _batchSize = opts.BatchSize;
        _enableCaching = opts.EnableCaching;
    }

    /// <summary>
    /// High-performance indexing with parallel processing and batching
    /// </summary>
    public async Task<IndexingResult> IndexFretboardVoicingsAsync(
        Tuning tuning,
        string instrumentName = "Guitar",
        int maxFret = 12,
        bool includeBiomechanicalAnalysis = true,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting optimized fretboard indexing for {Instrument} with {Tuning}", instrumentName, tuning);

        try
        {
            var fretboard = new Fretboard(tuning, 24); // Standard 24-fret guitar
            _fretboards[instrumentName] = fretboard;

            var generator = new FretboardChordsGenerator(fretboard);
            var allVoicings = generator.GetChordPositions().ToList();

            // Filter voicings by max fret upfront
            var filteredVoicings = allVoicings
                .Where(voicing => voicing
                    .OfType<Position.Played>()
                    .Select(p => p.Location.Fret.Value)
                    .DefaultIfEmpty(0)
                    .Max() <= maxFret)
                .ToList();

            _logger.LogInformation("Generated {Total} voicings, {Filtered} within fret limit",
                allVoicings.Count, filteredVoicings.Count);

            var indexed = 0;
            var errors = 0;
            var totalVoicings = filteredVoicings.Count;

            // Create a channel for producer-consumer pattern
            var channel = Channel.CreateBounded<VoicingBatch>(new BoundedChannelOptions(10)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
            });

            // Producer task: analyze voicings and create batches
            var producerTask = Task.Run(async () =>
            {
                try
                {
                    var batch = new List<VoicingAnalysis>();
                    var processedCount = 0;

                    foreach (var voicing in filteredVoicings)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            break;

                        try
                        {
                            var analysis = FretboardChordAnalyzer.AnalyzeChordVoicing(
                                voicing,
                                fretboard,
                                includeBiomechanicalAnalysis);

                            var document = SemanticDocumentGenerator.GenerateFretboardVoicingDocument(analysis);

                            batch.Add(new VoicingAnalysis(analysis, document));
                            processedCount++;

                            // Send batch when full
                            if (batch.Count >= _batchSize)
                            {
                                await channel.Writer.WriteAsync(new VoicingBatch(batch.ToArray()), cancellationToken);
                                batch.Clear();
                            }

                            // Report progress
                            if (processedCount % 200 == 0)
                            {
                                progress?.Report(new IndexingProgress(indexed, totalVoicings, errors));
                            }
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Increment(ref errors);
                            _logger.LogWarning(ex, "Error analyzing voicing");
                        }
                    }

                    // Send remaining batch
                    if (batch.Count > 0)
                    {
                        await channel.Writer.WriteAsync(new VoicingBatch(batch.ToArray()), cancellationToken);
                    }

                    channel.Writer.Complete();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Producer task failed");
                    channel.Writer.Complete(ex);
                }
            });

            // Consumer tasks: process batches with embeddings
            var consumerTasks = Enumerable.Range(0, _maxConcurrency)
                .Select(i => Task.Run(async () =>
                {
                    await foreach (var batch in channel.Reader.ReadAllAsync(cancellationToken))
                    {
                        try
                        {
                            await ProcessVoicingBatchAsync(batch);
                            Interlocked.Add(ref indexed, batch.Analyses.Length);
                        }
                        catch (Exception ex)
                        {
                            Interlocked.Add(ref errors, batch.Analyses.Length);
                            _logger.LogWarning(ex, "Error processing batch");
                        }
                    }
                }))
                .ToArray();

            // Wait for all tasks to complete
            await Task.WhenAll(producerTask);
            await Task.WhenAll(consumerTasks);

            stopwatch.Stop();
            _isIndexed = true;

            var result = new IndexingResult(
                InstrumentName: instrumentName,
                TuningName: tuning.ToString(),
                TotalVoicings: totalVoicings,
                IndexedVoicings: indexed,
                Errors: errors,
                ElapsedTime: stopwatch.Elapsed,
                IndexSize: _searchService.GetStatistics().TotalDocuments);

            _logger.LogInformation("Optimized indexing completed: {Result}", result);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index fretboard voicings");
            throw;
        }
    }

    /// <summary>
    /// Process a batch of voicings with optimized embedding generation
    /// </summary>
    private async Task ProcessVoicingBatchAsync(VoicingBatch batch)
    {
        var texts = batch.Analyses.Select(a => a.Document.Content).ToArray();
        var embeddings = new float[texts.Length][];

        // Check cache first if enabled
        if (_enableCaching)
        {
            var cachedEmbeddings = new List<(int index, float[] embedding)>();
            var uncachedIndices = new List<int>();

            for (var i = 0; i < texts.Length; i++)
            {
                var hash = ComputeContentHash(texts[i]);
                if (_embeddingCache.TryGetValue(hash, out var cachedEmbedding))
                {
                    cachedEmbeddings.Add((i, cachedEmbedding));
                }
                else
                {
                    uncachedIndices.Add(i);
                }
            }

            // Use cached embeddings
            foreach (var (index, embedding) in cachedEmbeddings)
            {
                embeddings[index] = embedding;
            }

            // Generate embeddings for uncached items
            if (uncachedIndices.Count > 0)
            {
                var uncachedTexts = uncachedIndices.Select(i => texts[i]).ToArray();
                var newEmbeddings = await _batchEmbeddingService.GenerateBatchEmbeddingsAsync(uncachedTexts);

                for (var i = 0; i < uncachedIndices.Count; i++)
                {
                    var originalIndex = uncachedIndices[i];
                    var embedding = newEmbeddings[i];
                    embeddings[originalIndex] = embedding;

                    // Cache the new embedding
                    var hash = ComputeContentHash(texts[originalIndex]);
                    _embeddingCache.TryAdd(hash, embedding);
                }
            }
        }
        else
        {
            // Generate all embeddings without caching
            embeddings = await _batchEmbeddingService.GenerateBatchEmbeddingsAsync(texts);
        }

        // Index all documents in the batch
        for (var i = 0; i < batch.Analyses.Length; i++)
        {
            var analysis = batch.Analyses[i];
            var embedding = embeddings[i];

            var indexedDoc = new SemanticSearchService.IndexedDocument(
                analysis.Document.Id,
                analysis.Document.Content,
                analysis.Document.Category,
                analysis.Document.Metadata,
                embedding);

            // Add to search service (this is thread-safe with ConcurrentBag)
            await _searchService.IndexDocumentDirectAsync(indexedDoc);
        }
    }

    /// <summary>
    /// Compute a hash for content to enable caching
    /// </summary>
    private static string ComputeContentHash(string content)
    {
        // Use a simple hash for now - could be improved with SHA256 for better distribution
        return content.Length.ToString() + "_" + content.GetHashCode().ToString();
    }

    /// <summary>
    /// Process natural language query (same as original but with optimized service)
    /// </summary>
    public async Task<QueryResult> ProcessNaturalLanguageQueryAsync(
        string naturalLanguageQuery,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        if (!_isIndexed)
            throw new InvalidOperationException("Fretboard voicings must be indexed before querying");

        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Processing natural language query: {Query}", naturalLanguageQuery);

        try
        {
            await _llmService.EnsureBestModelAvailableAsync();

            var searchResults = await _searchService.SearchAsync(
                naturalLanguageQuery,
                maxResults * 2);

            var context = PrepareContextForLlm(searchResults, naturalLanguageQuery);
            var llmResponse = await _llmService.ProcessNaturalLanguageQueryAsync(
                naturalLanguageQuery,
                context);

            stopwatch.Stop();

            var result = new QueryResult(
                Query: naturalLanguageQuery,
                SearchResults: searchResults.Take(maxResults).ToList(),
                LlmInterpretation: llmResponse,
                ElapsedTime: stopwatch.Elapsed,
                ModelUsed: await _llmService.GetBestAvailableModelAsync());

            _logger.LogInformation("Query processed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process natural language query");
            throw;
        }
    }

    /// <summary>
    /// Get index statistics
    /// </summary>
    public SemanticSearchService.IndexStatistics GetIndexStatistics() => _searchService.GetStatistics();

    /// <summary>
    /// Clear index and cache
    /// </summary>
    public void ClearIndex()
    {
        _searchService.Clear();
        _embeddingCache.Clear();
        _isIndexed = false;
        _fretboards.Clear();
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStatistics GetCacheStatistics() => new(
        CachedEmbeddings: _embeddingCache.Count,
        CacheMemoryUsageMB: _embeddingCache.Count * 768 * sizeof(float) / (1024.0 * 1024.0));

    /// <summary>
    /// Prepare context for LLM processing
    /// </summary>
    private static string PrepareContextForLlm(List<SemanticSearchService.SearchResult> searchResults, string query)
    {
        var context = $"User Query: {query}\n\nRelevant Chord Voicings Found:\n\n";

        for (var i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            context += $"{i + 1}. {result.Content}\n";
            context += $"   Relevance Score: {result.Score:F2}\n";
            context += $"   Match Reason: {result.MatchReason}\n\n";
        }

        context += "\nPlease analyze these chord voicings and provide a helpful response to the user's query. ";
        context += "Focus on the most relevant voicings and explain why they match the user's request. ";
        context += "Include practical playing advice if appropriate.";

        return context;
    }
}

/// <summary>
/// Batch of voicing analyses for processing
/// </summary>
internal record VoicingBatch(VoicingAnalysis[] Analyses);

/// <summary>
/// Semantic document for indexing
/// </summary>
internal record SemanticDocument(
    string Id,
    string Content,
    string Category,
    Dictionary<string, string> Metadata);

/// <summary>
/// Voicing analysis with generated document
/// </summary>
internal record VoicingAnalysis(
    object Analysis,
    SemanticDocument Document);

/// <summary>
/// Optimization options for the service
/// </summary>
public record OptimizationOptions(
    int MaxConcurrency = -1, // -1 means use Environment.ProcessorCount
    int BatchSize = 50,
    bool EnableCaching = true);

/// <summary>
/// Cache performance statistics
/// </summary>
public record CacheStatistics(
    int CachedEmbeddings,
    double CacheMemoryUsageMB);

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

/// <summary>
/// Ultra-detailed progress tracking for extreme performance testing
/// </summary>
public record UltraIndexingProgress(
    int Total,
    int Processed,
    int Successful,
    int Errors,
    double CurrentThroughput,
    double CacheHitRate,
    double MemoryUsageMB,
    DateTime StartTime)
{
    public double PercentComplete => Total > 0 ? (Processed * 100.0) / Total : 0;
    public TimeSpan ElapsedTime => DateTime.Now - StartTime;
}
