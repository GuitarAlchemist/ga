namespace GA.Business.Core.Tests.Fretboard.SemanticIndexing;

using System.Net.Http;
using Data.SemanticKernel.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
///     Performance comparison tests between original and optimized semantic fretboard services
///     Measures the impact of parallel processing, batching, and caching optimizations
/// </summary>
[TestFixture]
[Category("Performance")]
[Category("Optimization")]
[Category("Ollama")]
public class OptimizedIndexingPerformanceTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Check if Ollama is running
        if (!await IsOllamaRunningAsync())
        {
            Assert.Ignore("Ollama is not running. Performance comparison tests require Ollama.");
            return;
        }

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        _httpClient = new HttpClient { BaseAddress = new Uri(OllamaBaseUrl) };
        services.AddSingleton(_httpClient);

        // Original embedding service
        services.AddSingleton<OllamaTextEmbeddingGeneration>(provider =>
            new OllamaTextEmbeddingGeneration(_httpClient, EmbeddingModel));
        services.AddSingleton<OllamaEmbeddingService>(provider =>
            new OllamaEmbeddingService(provider.GetRequiredService<OllamaTextEmbeddingGeneration>()));
        services.AddSingleton<SemanticSearchService.IEmbeddingService>(provider =>
            provider.GetRequiredService<OllamaEmbeddingService>());

        // Batch embedding service
        services.AddSingleton<BatchOllamaEmbeddingService>(provider =>
            new BatchOllamaEmbeddingService(_httpClient, EmbeddingModel, 10,
                provider.GetRequiredService<ILogger<BatchOllamaEmbeddingService>>()));
        services.AddSingleton<OptimizedSemanticFretboardService.IBatchEmbeddingService>(provider =>
            provider.GetRequiredService<BatchOllamaEmbeddingService>());

        // Search services (separate instances for fair comparison)
        services.AddSingleton<SemanticSearchService>(provider =>
            new SemanticSearchService(provider.GetRequiredService<SemanticSearchService.IEmbeddingService>()));

        // Mock LLM service for both
        var mockLlmService = new Mock<SemanticFretboardService.IOllamaLlmService>();
        mockLlmService.Setup(x => x.EnsureBestModelAvailableAsync()).ReturnsAsync(true);
        mockLlmService.Setup(x => x.GetBestAvailableModelAsync()).ReturnsAsync("test-model");
        services.AddSingleton(mockLlmService.Object);

        // Original service
        services.AddSingleton<SemanticFretboardService>();

        // Optimized service
        services.AddSingleton<OptimizedSemanticFretboardService>();

        _serviceProvider = services.BuildServiceProvider();
        _originalService = _serviceProvider.GetRequiredService<SemanticFretboardService>();
        _optimizedService = _serviceProvider.GetRequiredService<OptimizedSemanticFretboardService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<OptimizedIndexingPerformanceTests>>();

        _logger.LogInformation("Performance comparison test setup completed");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
        _serviceProvider?.Dispose();
    }

    private ServiceProvider? _serviceProvider;
    private SemanticFretboardService? _originalService;
    private OptimizedSemanticFretboardService? _optimizedService;
    private ILogger<OptimizedIndexingPerformanceTests>? _logger;
    private HttpClient? _httpClient;

    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string EmbeddingModel = "nomic-embed-text";

    [Test]
    [Timeout(1800000)] // 30 minutes timeout
    public async Task ShouldShowSignificantSpeedupWithOptimizations()
    {
        var tuning = Tuning.StandardGuitar;
        var maxFret = 5; // Reasonable size for comparison

        _logger!.LogInformation("Starting performance comparison test");

        // Test original service
        _logger.LogInformation("Testing original service...");
        var originalStopwatch = Stopwatch.StartNew();

        var originalResult = await _originalService!.IndexFretboardVoicingsAsync(
            tuning,
            instrumentName: "Original Test",
            maxFret: maxFret,
            includeBiomechanicalAnalysis: false); // Disable for fair comparison

        originalStopwatch.Stop();

        // Clear and test optimized service
        _logger.LogInformation("Testing optimized service...");
        var optimizedStopwatch = Stopwatch.StartNew();

        var optimizedResult = await _optimizedService!.IndexFretboardVoicingsAsync(
            tuning,
            instrumentName: "Optimized Test",
            maxFret: maxFret,
            includeBiomechanicalAnalysis: false);

        optimizedStopwatch.Stop();

        // Calculate performance improvements
        var speedupRatio = originalStopwatch.Elapsed.TotalSeconds / optimizedStopwatch.Elapsed.TotalSeconds;
        var throughputImprovement = optimizedResult.IndexingRate / originalResult.IndexingRate;

        _logger.LogInformation("Performance Comparison Results:");
        _logger.LogInformation("Original Service:");
        _logger.LogInformation("  Time: {Time:F1}s", originalStopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("  Rate: {Rate:F1} voicings/sec", originalResult.IndexingRate);
        _logger.LogInformation("  Indexed: {Count} voicings", originalResult.IndexedVoicings);

        _logger.LogInformation("Optimized Service:");
        _logger.LogInformation("  Time: {Time:F1}s", optimizedStopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("  Rate: {Rate:F1} voicings/sec", optimizedResult.IndexingRate);
        _logger.LogInformation("  Indexed: {Count} voicings", optimizedResult.IndexedVoicings);

        _logger.LogInformation("Performance Improvements:");
        _logger.LogInformation("  Speedup: {Speedup:F1}x faster", speedupRatio);
        _logger.LogInformation("  Throughput: {Throughput:F1}x higher", throughputImprovement);

        // Assertions
        Assert.That(optimizedResult.IndexedVoicings, Is.EqualTo(originalResult.IndexedVoicings),
            "Both services should index the same number of voicings");

        Assert.That(speedupRatio, Is.GreaterThan(1.5),
            $"Optimized service should be at least 1.5x faster, was {speedupRatio:F1}x");

        Assert.That(optimizedResult.IndexingRate, Is.GreaterThan(originalResult.IndexingRate),
            "Optimized service should have higher throughput");

        Assert.That(optimizedResult.SuccessRate, Is.GreaterThanOrEqualTo(originalResult.SuccessRate),
            "Optimized service should maintain or improve success rate");
    }

    [Test]
    [Timeout(600000)] // 10 minutes timeout
    public async Task ShouldDemonstrateEffectiveEmbeddingCaching()
    {
        var tuning = Tuning.StandardGuitar;
        var maxFret = 4;

        _logger!.LogInformation("Testing embedding caching effectiveness");

        // First run - populate cache
        _logger.LogInformation("First run (populating cache)...");
        var firstRunStopwatch = Stopwatch.StartNew();

        var firstResult = await _optimizedService!.IndexFretboardVoicingsAsync(
            tuning,
            instrumentName: "Cache Test 1",
            maxFret: maxFret,
            includeBiomechanicalAnalysis: false);

        firstRunStopwatch.Stop();
        var firstRunCacheStats = _optimizedService.GetCacheStatistics();

        // Clear index but keep cache
        _optimizedService.ClearIndex();

        // Second run - should benefit from cache
        _logger.LogInformation("Second run (using cache)...");
        var secondRunStopwatch = Stopwatch.StartNew();

        var secondResult = await _optimizedService.IndexFretboardVoicingsAsync(
            tuning,
            instrumentName: "Cache Test 2",
            maxFret: maxFret,
            includeBiomechanicalAnalysis: false);

        secondRunStopwatch.Stop();
        var secondRunCacheStats = _optimizedService.GetCacheStatistics();

        var cacheSpeedup = firstRunStopwatch.Elapsed.TotalSeconds / secondRunStopwatch.Elapsed.TotalSeconds;

        _logger.LogInformation("Cache Performance Results:");
        _logger.LogInformation("First Run (no cache):");
        _logger.LogInformation("  Time: {Time:F1}s", firstRunStopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("  Rate: {Rate:F1} voicings/sec", firstResult.IndexingRate);
        _logger.LogInformation("  Cache entries: {Count}", firstRunCacheStats.CachedEmbeddings);

        _logger.LogInformation("Second Run (with cache):");
        _logger.LogInformation("  Time: {Time:F1}s", secondRunStopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("  Rate: {Rate:F1} voicings/sec", secondResult.IndexingRate);
        _logger.LogInformation("  Cache entries: {Count}", secondRunCacheStats.CachedEmbeddings);

        _logger.LogInformation("Cache Effectiveness:");
        _logger.LogInformation("  Speedup: {Speedup:F1}x faster", cacheSpeedup);
        _logger.LogInformation("  Cache memory: {Memory:F1} MB", secondRunCacheStats.CacheMemoryUsageMB);

        // Assertions
        Assert.That(secondRunCacheStats.CachedEmbeddings, Is.GreaterThan(0),
            "Cache should contain embeddings after first run");

        Assert.That(cacheSpeedup, Is.GreaterThan(1.2),
            $"Second run should be at least 1.2x faster due to caching, was {cacheSpeedup:F1}x");

        Assert.That(secondResult.IndexedVoicings, Is.EqualTo(firstResult.IndexedVoicings),
            "Both runs should index the same number of voicings");
    }

    [Test]
    [Timeout(300000)] // 5 minutes timeout
    public async Task ShouldScaleWellWithConcurrency()
    {
        var tuning = Tuning.StandardGuitar;
        var maxFret = 4;

        _logger!.LogInformation("Testing concurrency scaling");

        // Test different concurrency levels
        var concurrencyLevels = new[] { 1, 2, 4, 8 };
        var results = new List<(int concurrency, IndexingResult result, TimeSpan elapsed)>();

        foreach (var concurrency in concurrencyLevels)
        {
            _logger.LogInformation("Testing with {Concurrency} concurrent workers", concurrency);

            // Create optimized service with specific concurrency
            var optimizedService = new OptimizedSemanticFretboardService(
                _serviceProvider!.GetRequiredService<SemanticSearchService>(),
                _serviceProvider.GetRequiredService<OptimizedSemanticFretboardService.IBatchEmbeddingService>(),
                _serviceProvider.GetRequiredService<SemanticFretboardService.IOllamaLlmService>(),
                _serviceProvider.GetRequiredService<ILogger<OptimizedSemanticFretboardService>>(),
                new OptimizationOptions(MaxConcurrency: concurrency, BatchSize: 25, EnableCaching: false));

            var stopwatch = Stopwatch.StartNew();

            var result = await optimizedService.IndexFretboardVoicingsAsync(
                tuning,
                instrumentName: $"Concurrency Test {concurrency}",
                maxFret: maxFret,
                includeBiomechanicalAnalysis: false);

            stopwatch.Stop();
            results.Add((concurrency, result, stopwatch.Elapsed));

            optimizedService.ClearIndex();

            _logger.LogInformation("Concurrency {Level}: {Time:F1}s, {Rate:F1} voicings/sec",
                concurrency, stopwatch.Elapsed.TotalSeconds, result.IndexingRate);
        }

        // Analyze scaling
        var baseResult = results[0];
        _logger.LogInformation("Concurrency Scaling Analysis:");
        _logger.LogInformation("Baseline (1 worker): {Time:F1}s, {Rate:F1} voicings/sec",
            baseResult.elapsed.TotalSeconds, baseResult.result.IndexingRate);

        foreach (var (concurrency, result, elapsed) in results.Skip(1))
        {
            var speedup = baseResult.elapsed.TotalSeconds / elapsed.TotalSeconds;
            var efficiency = speedup / concurrency;

            _logger.LogInformation("{Concurrency} workers: {Speedup:F1}x speedup, {Efficiency:P1} efficiency",
                concurrency, speedup, efficiency);

            // Should see some speedup with more workers
            Assert.That(speedup, Is.GreaterThan(1.0),
                $"Should see speedup with {concurrency} workers");

            // Efficiency should be reasonable (at least 30% for higher concurrency)
            if (concurrency <= 4)
            {
                Assert.That(efficiency, Is.GreaterThan(0.3),
                    $"Efficiency should be reasonable with {concurrency} workers");
            }
        }
    }

    [Test]
    [Timeout(180000)] // 3 minutes timeout
    public async Task ShouldOptimizeBatchSizeEffectively()
    {
        var tuning = Tuning.StandardGuitar;
        var maxFret = 3; // Smaller dataset for batch size testing

        _logger!.LogInformation("Testing batch size optimization");

        var batchSizes = new[] { 10, 25, 50, 100 };
        var results = new List<(int batchSize, IndexingResult result, TimeSpan elapsed)>();

        foreach (var batchSize in batchSizes)
        {
            _logger.LogInformation("Testing with batch size {BatchSize}", batchSize);

            var optimizedService = new OptimizedSemanticFretboardService(
                _serviceProvider!.GetRequiredService<SemanticSearchService>(),
                _serviceProvider.GetRequiredService<OptimizedSemanticFretboardService.IBatchEmbeddingService>(),
                _serviceProvider.GetRequiredService<SemanticFretboardService.IOllamaLlmService>(),
                _serviceProvider.GetRequiredService<ILogger<OptimizedSemanticFretboardService>>(),
                new OptimizationOptions(MaxConcurrency: 4, BatchSize: batchSize, EnableCaching: false));

            var stopwatch = Stopwatch.StartNew();

            var result = await optimizedService.IndexFretboardVoicingsAsync(
                tuning,
                instrumentName: $"Batch Test {batchSize}",
                maxFret: maxFret,
                includeBiomechanicalAnalysis: false);

            stopwatch.Stop();
            results.Add((batchSize, result, stopwatch.Elapsed));

            optimizedService.ClearIndex();

            _logger.LogInformation("Batch size {Size}: {Time:F1}s, {Rate:F1} voicings/sec",
                batchSize, stopwatch.Elapsed.TotalSeconds, result.IndexingRate);
        }

        // Find optimal batch size
        var optimalResult = results.OrderByDescending(r => r.result.IndexingRate).First();

        _logger.LogInformation("Batch Size Analysis:");
        _logger.LogInformation("Optimal batch size: {Size} ({Rate:F1} voicings/sec)",
            optimalResult.batchSize, optimalResult.result.IndexingRate);

        // All batch sizes should complete successfully
        foreach (var (batchSize, result, elapsed) in results)
        {
            Assert.That(result.SuccessRate, Is.GreaterThan(0.9),
                $"Batch size {batchSize} should have high success rate");
            Assert.That(result.IndexedVoicings, Is.GreaterThan(0),
                $"Batch size {batchSize} should index voicings");
        }

        // Optimal batch size should be reasonable
        Assert.That(optimalResult.batchSize, Is.InRange(25, 100),
            "Optimal batch size should be in reasonable range");
    }

    /// <summary>
    ///     Check if Ollama is running
    /// </summary>
    private async Task<bool> IsOllamaRunningAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync($"{OllamaBaseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
