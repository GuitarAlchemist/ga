namespace GaApi.Services;

using System.Diagnostics;
using Microsoft.Extensions.Options;

/// <summary>
///     Manages different vector search strategies and automatically selects the best available option
/// </summary>
public class VectorSearchStrategyManager : IDisposable
{
    private readonly ILogger<VectorSearchStrategyManager> _logger;
    private readonly VectorSearchOptions _options;
    private readonly Dictionary<string, IVectorSearchStrategy> _strategies = new();
    private readonly Lock _strategyLock = new();

    public VectorSearchStrategyManager(
        ILogger<VectorSearchStrategyManager> logger,
        IOptions<VectorSearchOptions> options,
        IEnumerable<IVectorSearchStrategy> strategies)
    {
        _logger = logger;
        _options = options.Value;

        // Register all available strategies
        foreach (var strategy in strategies)
        {
            _strategies[strategy.Name] = strategy;
            _logger.LogInformation("Registered vector search strategy: {StrategyName} (Available: {IsAvailable})",
                strategy.Name, strategy.IsAvailable);
        }
    }

    /// <summary>
    ///     Gets the currently active strategy
    /// </summary>
    public IVectorSearchStrategy? ActiveStrategy { get; private set; }

    public void Dispose()
    {
        foreach (var strategy in _strategies.Values)
        {
            if (strategy is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    ///     Gets all available strategies with their performance characteristics
    /// </summary>
    public Dictionary<string, VectorSearchPerformance> GetAvailableStrategies()
    {
        return _strategies
            .Where(kvp => kvp.Value.IsAvailable)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Performance);
    }

    /// <summary>
    ///     Initialize the best available strategy with chord data
    /// </summary>
    public async Task InitializeAsync(IEnumerable<ChordEmbedding> chords)
    {
        lock (_strategyLock)
        {
            if (ActiveStrategy != null)
            {
                _logger.LogWarning("Vector search strategy already initialized");
                return;
            }

            ActiveStrategy = SelectBestStrategy();
            if (ActiveStrategy == null)
            {
                throw new InvalidOperationException("No vector search strategy is available");
            }
        }

        _logger.LogInformation("Initializing vector search with strategy: {StrategyName}", ActiveStrategy.Name);
        await ActiveStrategy.InitializeAsync(chords);

        var stats = ActiveStrategy.GetStats();
        _logger.LogInformation(
            "Vector search initialized: {ChordCount} chords, {MemoryMB}MB memory, Strategy: {StrategyName}",
            stats.TotalChords, stats.MemoryUsageMb, ActiveStrategy.Name);
    }

    /// <summary>
    ///     Switch to a specific strategy (if available)
    /// </summary>
    public async Task SwitchStrategyAsync(string strategyName, IEnumerable<ChordEmbedding> chords)
    {
        if (!_strategies.TryGetValue(strategyName, out var newStrategy))
        {
            throw new ArgumentException($"Strategy '{strategyName}' not found");
        }

        if (!newStrategy.IsAvailable)
        {
            throw new InvalidOperationException($"Strategy '{strategyName}' is not available on this system");
        }

        lock (_strategyLock)
        {
            ActiveStrategy = newStrategy;
        }

        _logger.LogInformation("Switching to vector search strategy: {StrategyName}", strategyName);
        await newStrategy.InitializeAsync(chords);
    }

    /// <summary>
    ///     Perform semantic search using the active strategy
    /// </summary>
    public async Task<List<ChordSearchResult>> SemanticSearchAsync(
        double[] queryEmbedding,
        int limit = 10,
        int numCandidates = 100)
    {
        EnsureInitialized();
        return await ActiveStrategy!.SemanticSearchAsync(queryEmbedding, limit, numCandidates);
    }

    /// <summary>
    ///     Find similar chords using the active strategy
    /// </summary>
    public async Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100)
    {
        EnsureInitialized();
        return await ActiveStrategy!.FindSimilarChordsAsync(chordId, limit, numCandidates);
    }

    /// <summary>
    ///     Perform hybrid search using the active strategy
    /// </summary>
    public async Task<List<ChordSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        ChordSearchFilters filters,
        int limit = 10,
        int numCandidates = 100)
    {
        EnsureInitialized();
        return await ActiveStrategy!.HybridSearchAsync(queryEmbedding, filters, limit, numCandidates);
    }

    /// <summary>
    ///     Get performance statistics for all strategies
    /// </summary>
    public Dictionary<string, VectorSearchStats> GetAllStats()
    {
        var stats = new Dictionary<string, VectorSearchStats>();

        foreach (var kvp in _strategies)
        {
            if (kvp.Value.IsAvailable)
            {
                try
                {
                    stats[kvp.Key] = kvp.Value.GetStats();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get stats for strategy: {StrategyName}", kvp.Key);
                }
            }
        }

        return stats;
    }

    /// <summary>
    ///     Get performance comparison between strategies
    /// </summary>
    public async Task<Dictionary<string, TimeSpan>> BenchmarkStrategiesAsync(
        double[] testEmbedding,
        int iterations = 10)
    {
        var results = new Dictionary<string, TimeSpan>();

        foreach (var kvp in _strategies.Where(s => s.Value.IsAvailable))
        {
            try
            {
                var strategy = kvp.Value;
                var stopwatch = Stopwatch.StartNew();

                for (var i = 0; i < iterations; i++)
                {
                    await strategy.SemanticSearchAsync(testEmbedding);
                }

                stopwatch.Stop();
                results[kvp.Key] = TimeSpan.FromTicks(stopwatch.ElapsedTicks / iterations);

                _logger.LogInformation(
                    "Strategy {StrategyName} benchmark: {AvgTimeMs}ms per search",
                    kvp.Key, results[kvp.Key].TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Benchmark failed for strategy: {StrategyName}", kvp.Key);
            }
        }

        return results;
    }

    private IVectorSearchStrategy? SelectBestStrategy()
    {
        // Strategy selection priority based on configuration and availability
        var preferredOrder = _options.PreferredStrategies ?? ["CUDA", "InMemory", "MongoDB"];

        foreach (var strategyName in preferredOrder)
        {
            if (_strategies.TryGetValue(strategyName, out var strategy) && strategy.IsAvailable)
            {
                _logger.LogInformation("Selected vector search strategy: {StrategyName}", strategyName);
                return strategy;
            }
        }

        // Fallback: select any available strategy
        var fallbackStrategy = _strategies.Values.FirstOrDefault(s => s.IsAvailable);
        if (fallbackStrategy != null)
        {
            _logger.LogInformation("Using fallback vector search strategy: {StrategyName}", fallbackStrategy.Name);
        }

        return fallbackStrategy;
    }

    private void EnsureInitialized()
    {
        if (ActiveStrategy == null)
        {
            throw new InvalidOperationException("Vector search not initialized. Call InitializeAsync first.");
        }
    }
}

/// <summary>
///     Configuration options for vector search strategies
/// </summary>
public class VectorSearchOptions
{
    /// <summary>
    ///     Preferred strategy order (first available will be used)
    /// </summary>
    public string[]? PreferredStrategies { get; set; }

    /// <summary>
    ///     Whether to enable automatic strategy switching based on performance
    /// </summary>
    public bool EnableAutoSwitching { get; set; } = false;

    /// <summary>
    ///     Minimum performance improvement required to switch strategies (in percentage)
    /// </summary>
    public double AutoSwitchThreshold { get; set; } = 20.0;

    /// <summary>
    ///     Whether to preload all available strategies
    /// </summary>
    public bool PreloadStrategies { get; set; } = false;

    /// <summary>
    ///     Maximum memory usage for in-memory strategies (in MB)
    /// </summary>
    public long MaxMemoryUsageMb { get; set; } = 2048;
}

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
        int numCandidates = 100)
    {
        // Delegate to existing VectorSearchService
        // This would need to be adapted to work with the embedding directly
        throw new NotImplementedException("Adapt existing VectorSearchService.SemanticSearchAsync");
    }

    public async Task<List<ChordSearchResult>> FindSimilarChordsAsync(
        int chordId,
        int limit = 10,
        int numCandidates = 100)
    {
        return await vectorSearchService.FindSimilarChordsAsync(chordId, limit, numCandidates);
    }

    public Task<List<ChordSearchResult>> HybridSearchAsync(
        double[] queryEmbedding,
        ChordSearchFilters filters,
        int limit = 10,
        int numCandidates = 100)
    {
        // Adapt existing hybrid search
        throw new NotImplementedException("Adapt existing VectorSearchService.HybridSearchAsync");
    }

    public VectorSearchStats GetStats()
    {
        return new VectorSearchStats(
            427254, // Known from database
            0, // MongoDB handles memory
            TimeSpan.FromMilliseconds(50), // Estimated
            0); // Would need to track this
    }
}
