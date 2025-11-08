namespace GaApi.Services;

using System.Diagnostics;
using Microsoft.Extensions.Options;
using Models;
using MongoDB.Bson;
using MongoDB.Driver;

/// <summary>
///     Enhanced vector search service that supports multiple strategies (MongoDB, In-Memory, CUDA)
/// </summary>
public class EnhancedVectorSearchService : IDisposable
{
    private readonly IMongoDatabase _database;
    private readonly LocalEmbeddingService? _localEmbedding;
    private readonly ILogger<EnhancedVectorSearchService> _logger;
    private readonly VectorSearchOptions _options;
    private readonly MongoDbSettings _settings;
    private readonly VectorSearchStrategyManager _strategyManager;
    private bool _isInitialized;

    public EnhancedVectorSearchService(
        VectorSearchStrategyManager strategyManager,
        LocalEmbeddingService localEmbedding,
        IOptions<MongoDbSettings> settings,
        IOptions<VectorSearchOptions> options,
        ILogger<EnhancedVectorSearchService> logger)
    {
        _strategyManager = strategyManager;
        _localEmbedding = localEmbedding;
        _settings = settings.Value;
        _options = options.Value;
        _logger = logger;

        var client = new MongoClient(_settings.ConnectionString);
        _database = client.GetDatabase(_settings.DatabaseName);
    }

    public void Dispose()
    {
        _strategyManager?.Dispose();
    }

    /// <summary>
    ///     Initialize the vector search service with the best available strategy
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            _logger.LogWarning("Vector search service already initialized");
            return;
        }

        _logger.LogInformation("Initializing enhanced vector search service...");
        var stopwatch = Stopwatch.StartNew();

        // Load chord embeddings from MongoDB
        var chords = await LoadChordEmbeddingsAsync();

        // Initialize the strategy manager with chord data
        await _strategyManager.InitializeAsync(chords);

        stopwatch.Stop();
        _isInitialized = true;

        var activeStrategy = _strategyManager.ActiveStrategy;
        _logger.LogInformation(
            "Enhanced vector search initialized in {ElapsedMs}ms using strategy: {StrategyName}",
            stopwatch.ElapsedMilliseconds, activeStrategy?.Name ?? "None");

        // Log performance characteristics
        if (activeStrategy != null)
        {
            var performance = activeStrategy.Performance;
            _logger.LogInformation(
                "Strategy performance: {ExpectedSearchTime}ms search time, {MemoryMB}MB memory, GPU: {RequiresGPU}",
                performance.ExpectedSearchTime.TotalMilliseconds,
                performance.MemoryUsageMb,
                performance.RequiresGpu);
        }
    }

    /// <summary>
    ///     Generate embedding for a text query
    /// </summary>
    public async Task<double[]> GenerateEmbeddingAsync(string text)
    {
        if (_localEmbedding?.IsAvailable == true)
        {
            return await Task.Run(() =>
            {
                var embedding = _localEmbedding.GenerateEmbedding(text);
                return embedding.Select(f => (double)f).ToArray();
            });
        }

        throw new InvalidOperationException("No embedding service available");
    }

    /// <summary>
    ///     Perform semantic search using natural language
    /// </summary>
    public async Task<List<ChordSearchResult>> SemanticSearchAsync(
        string query,
        int limit = 10,
        int numCandidates = 100)
    {
        EnsureInitialized();

        var queryEmbedding = await GenerateEmbeddingAsync(query);
        return await _strategyManager.SemanticSearchAsync(queryEmbedding, limit, numCandidates);
    }

    /// <summary>
    ///     Find chords similar to a specific chord
    /// </summary>
    public async Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100)
    {
        EnsureInitialized();
        return await _strategyManager.FindSimilarChordsAsync(chordId, limit, numCandidates);
    }

    /// <summary>
    ///     Hybrid search combining semantic understanding with filters
    /// </summary>
    public async Task<List<ChordSearchResult>> HybridSearchAsync(
        string query,
        string? quality = null,
        string? extension = null,
        string? stackingType = null,
        int? noteCount = null,
        int limit = 10,
        int numCandidates = 100)
    {
        EnsureInitialized();

        var queryEmbedding = await GenerateEmbeddingAsync(query);
        var filters = new ChordSearchFilters(quality, extension, stackingType, noteCount);

        return await _strategyManager.HybridSearchAsync(queryEmbedding, filters, limit, numCandidates);
    }

    /// <summary>
    ///     Switch to a specific vector search strategy
    /// </summary>
    public async Task SwitchStrategyAsync(string strategyName)
    {
        _logger.LogInformation("Switching vector search strategy to: {StrategyName}", strategyName);

        var chords = await LoadChordEmbeddingsAsync();
        await _strategyManager.SwitchStrategyAsync(strategyName, chords);

        _logger.LogInformation("Successfully switched to strategy: {StrategyName}", strategyName);
    }

    /// <summary>
    ///     Get available strategies and their performance characteristics
    /// </summary>
    public Dictionary<string, VectorSearchPerformance> GetAvailableStrategies()
    {
        return _strategyManager.GetAvailableStrategies();
    }

    /// <summary>
    ///     Get performance statistics for all strategies
    /// </summary>
    public Dictionary<string, VectorSearchStats> GetPerformanceStats()
    {
        return _strategyManager.GetAllStats();
    }

    /// <summary>
    ///     Benchmark all available strategies
    /// </summary>
    public async Task<Dictionary<string, TimeSpan>> BenchmarkStrategiesAsync(int iterations = 10)
    {
        EnsureInitialized();

        // Use a sample embedding for benchmarking
        var testEmbedding = await GenerateEmbeddingAsync("test chord for benchmarking");
        return await _strategyManager.BenchmarkStrategiesAsync(testEmbedding, iterations);
    }

    /// <summary>
    ///     Get current strategy information
    /// </summary>
    public VectorSearchStrategyInfo GetCurrentStrategyInfo()
    {
        var activeStrategy = _strategyManager.ActiveStrategy;
        if (activeStrategy == null)
        {
            return new VectorSearchStrategyInfo("None", false, null, null);
        }

        var stats = activeStrategy.GetStats();
        return new VectorSearchStrategyInfo(
            activeStrategy.Name,
            activeStrategy.IsAvailable,
            activeStrategy.Performance,
            stats);
    }

    private async Task<List<ChordEmbedding>> LoadChordEmbeddingsAsync()
    {
        _logger.LogInformation("Loading chord embeddings from MongoDB...");
        var stopwatch = Stopwatch.StartNew();

        var collection = _database.GetCollection<BsonDocument>(_settings.Collections.Chords);

        // Only load chords that have embeddings
        var filter = Builders<BsonDocument>.Filter.Exists("Embedding");
        var projection = Builders<BsonDocument>.Projection
            .Include("Id")
            .Include("Name")
            .Include("Quality")
            .Include("Extension")
            .Include("StackingType")
            .Include("NoteCount")
            .Include("Description")
            .Include("Embedding");

        var cursor = await collection.FindAsync(filter, new FindOptions<BsonDocument>
        {
            Projection = projection
        });

        var chords = new List<ChordEmbedding>();
        await cursor.ForEachAsync(doc =>
        {
            try
            {
                var embedding = doc.GetValue("Embedding", BsonNull.Value);
                if (embedding != BsonNull.Value && embedding.IsBsonArray)
                {
                    var embeddingArray = embedding.AsBsonArray
                        .Select(x => x.AsDouble)
                        .ToArray();

                    var chord = new ChordEmbedding(
                        doc.GetValue("Id", 0).AsInt32,
                        doc.GetValue("Name", "").AsString,
                        doc.GetValue("Quality", "").AsString,
                        doc.GetValue("Extension", "").AsString,
                        doc.GetValue("StackingType", "").AsString,
                        doc.GetValue("NoteCount", 0).AsInt32,
                        doc.GetValue("Description", "").AsString,
                        embeddingArray);

                    chords.Add(chord);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load chord embedding for document: {DocumentId}",
                    doc.GetValue("_id", "unknown"));
            }
        });

        stopwatch.Stop();
        _logger.LogInformation(
            "Loaded {ChordCount} chord embeddings in {ElapsedMs}ms",
            chords.Count, stopwatch.ElapsedMilliseconds);

        return chords;
    }

    private void EnsureInitialized()
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Vector search service not initialized. Call InitializeAsync first.");
        }
    }
}

/// <summary>
///     Information about the current vector search strategy
/// </summary>
public record VectorSearchStrategyInfo(
    string Name,
    bool IsAvailable,
    VectorSearchPerformance? Performance,
    VectorSearchStats? Stats);
