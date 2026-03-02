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
    private readonly Dictionary<string, IVectorSearchStrategy> _strategies = [];
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
    public Dictionary<string, VectorSearchPerformance> GetAvailableStrategies() =>
        _strategies
            .Where(kvp => kvp.Value.IsAvailable)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Performance);

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
