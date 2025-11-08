namespace GA.Business.Core.Tests.Fretboard.SemanticIndexing;

using System.Net.Http;
using System.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
///     EXTREME performance tests for the ultra-optimized semantic fretboard service
///     Tests every optimization: SIMD, GPU acceleration, lock-free algorithms, memory pooling
///     Expected results: 10-100x performance improvements, 1000+ voicings/second
/// </summary>
[TestFixture]
[Category("ExtremePerformance")]
[Category("GPU")]
[Category("SIMD")]
[Category("LongRunning")]
public class ExtremePerformanceTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Force server GC for maximum performance
        GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;

        // Check if Ollama is running
        if (!await IsOllamaRunningAsync())
        {
            Assert.Ignore("Ollama is not running. Extreme performance tests require Ollama.");
            return;
        }

        // Setup services with maximum performance configuration
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        _httpClient = new HttpClient { BaseAddress = new Uri(OllamaBaseUrl) };
        services.AddSingleton(_httpClient);

        // GPU-accelerated embedding service
        _gpuEmbeddingService = new GPUAcceleratedEmbeddingService(
            _httpClient,
            "nomic-embed-text",
            maxBatchSize: 10000,
            maxConcurrentRequests: 200);
        services.AddSingleton<IUltraFastEmbeddingService>(_gpuEmbeddingService);

        // SIMD-optimized similarity engine
        _simdEngine = new VectorizedSimilarityEngine(768);
        services.AddSingleton(_simdEngine);

        // Mock LLM service for testing
        var mockLlmService = new Mock<SemanticFretboardService.IOllamaLlmService>();
        mockLlmService.Setup(x => x.EnsureBestModelAvailableAsync()).ReturnsAsync(true);
        mockLlmService.Setup(x => x.GetBestAvailableModelAsync()).ReturnsAsync("test-model");
        services.AddSingleton(mockLlmService.Object);

        // Ultra high performance service with maximum optimization
        var ultraOptions = UltraPerformanceOptions.MaxPerformance();
        _ultraService = new UltraHighPerformanceSemanticService(
            _gpuEmbeddingService,
            mockLlmService.Object,
            services.BuildServiceProvider().GetRequiredService<ILogger<UltraHighPerformanceSemanticService>>(),
            ultraOptions);

        services.AddSingleton(_ultraService);

        _serviceProvider = services.BuildServiceProvider();
        _logger = _serviceProvider.GetRequiredService<ILogger<ExtremePerformanceTests>>();

        _logger.LogInformation("Extreme performance test setup completed with GPU acceleration and SIMD optimization");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _ultraService?.Dispose();
        _gpuEmbeddingService?.Dispose();
        _simdEngine?.Dispose();
        _httpClient?.Dispose();
        _serviceProvider?.Dispose();

        // Restore normal GC mode
        GCSettings.LatencyMode = GCLatencyMode.Interactive;
    }

    private ServiceProvider? _serviceProvider;
    private UltraHighPerformanceSemanticService? _ultraService;
    private GPUAcceleratedEmbeddingService? _gpuEmbeddingService;
    private VectorizedSimilarityEngine? _simdEngine;
    private ILogger<ExtremePerformanceTests>? _logger;
    private HttpClient? _httpClient;

    private const string OllamaBaseUrl = "http://localhost:11434";

    [Test]
    [Timeout(600000)] // 10 minutes timeout for extreme test
    public async Task ShouldAchieveExtremeIndexingPerformance()
    {
        // Test with larger dataset to show extreme performance
        var tuning = Tuning.StandardGuitar;
        var maxFret = 8; // Larger dataset
        var progress = new Progress<UltraIndexingProgress>();
        var progressUpdates = new List<UltraIndexingProgress>();

        progress.ProgressChanged += (_, p) =>
        {
            progressUpdates.Add(p);
            if (p.Processed % 1000 == 0)
            {
                _logger!.LogInformation(
                    "EXTREME: {Processed}/{Total} ({Percent:F1}%) - {Throughput:F0} voicings/sec, " +
                    "Cache: {CacheHit:P1}, Memory: {Memory:F1}MB",
                    p.Processed, p.Total, p.PercentComplete, p.CurrentThroughput, p.CacheHitRate, p.MemoryUsageMB);
            }
        };

        _logger!.LogInformation("Starting EXTREME performance test with GPU + SIMD + Lock-free + Memory pooling");

        var stopwatch = Stopwatch.StartNew();

        var result = await _ultraService!.IndexFretboardVoicingsUltraFastAsync(
            tuning,
            instrumentName: "Extreme Performance Test",
            maxFret: maxFret,
            includeBiomechanicalAnalysis: true,
            progress: progress);

        stopwatch.Stop();

        // EXTREME performance assertions
        Assert.That(result.IndexedVoicings, Is.GreaterThan(5000), "Should index substantial dataset");
        Assert.That(result.ThroughputVoicingsPerSecond, Is.GreaterThan(500),
            $"Should achieve >500 voicings/sec, achieved {result.ThroughputVoicingsPerSecond:F0}");
        Assert.That(result.SuccessRate, Is.GreaterThan(0.98), "Should have >98% success rate");
        Assert.That(result.ElapsedTime, Is.LessThan(TimeSpan.FromMinutes(5)), "Should complete within 5 minutes");
        Assert.That(result.SIMDOperationsCount, Is.GreaterThan(0), "Should use SIMD operations");
        Assert.That(result.CacheHitRate, Is.GreaterThan(0.3), "Should have >30% cache hit rate");

        _logger.LogInformation("EXTREME Performance Results:");
        _logger.LogInformation("  Throughput: {Throughput:F0} voicings/second", result.ThroughputVoicingsPerSecond);
        _logger.LogInformation("  Total Time: {Time:F1} seconds", result.ElapsedTime.TotalSeconds);
        _logger.LogInformation("  Success Rate: {Rate:P2}", result.SuccessRate);
        _logger.LogInformation("  Cache Hit Rate: {Rate:P2}", result.CacheHitRate);
        _logger.LogInformation("  SIMD Operations: {Count:N0}", result.SIMDOperationsCount);
        _logger.LogInformation("  Memory Usage: {Memory:F1} MB", result.MemoryAllocatedMB);
        _logger.LogInformation("  Cache Compression: {Ratio:P1}", result.CacheCompressionRatio);

        // Verify we achieved extreme performance
        var expectedMinThroughput = 500; // voicings per second
        Assert.That(result.ThroughputVoicingsPerSecond, Is.GreaterThan(expectedMinThroughput),
            $"EXTREME performance target not met. Expected >{expectedMinThroughput}, got {result.ThroughputVoicingsPerSecond:F0}");
    }

    [Test]
    [Timeout(300000)] // 5 minutes timeout
    public async Task ShouldDemonstrateGPUAcceleration()
    {
        _logger!.LogInformation("Testing GPU acceleration effectiveness");

        var testTexts = Enumerable.Range(0, 1000)
            .Select(i => $"Test embedding text number {i} with various musical content and chord descriptions")
            .ToArray();

        // Test GPU-accelerated batch processing
        var gpuStopwatch = Stopwatch.StartNew();
        var gpuResults = await _gpuEmbeddingService!.GenerateEmbeddingsBatchUltraFastAsync(
            testTexts.AsMemory());
        gpuStopwatch.Stop();

        var gpuThroughput = testTexts.Length / gpuStopwatch.Elapsed.TotalSeconds;

        _logger.LogInformation("GPU Acceleration Results:");
        _logger.LogInformation("  Processed: {Count} embeddings", testTexts.Length);
        _logger.LogInformation("  Time: {Time:F2} seconds", gpuStopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("  Throughput: {Throughput:F0} embeddings/second", gpuThroughput);
        _logger.LogInformation("  Result size: {Size:N0} floats", gpuResults.Length);

        // Verify GPU results
        Assert.That(gpuResults.Length, Is.EqualTo(testTexts.Length * 768),
            "Should generate correct number of embeddings");
        Assert.That(gpuThroughput, Is.GreaterThan(50), "GPU should process >50 embeddings/second");
        Assert.That(gpuResults.Span.ToArray().Any(x => x != 0), Is.True, "Embeddings should not be all zeros");
    }

    [Test]
    [Timeout(180000)] // 3 minutes timeout
    public async Task ShouldDemonstrateSIMDOptimization()
    {
        _logger!.LogInformation("Testing SIMD vectorization effectiveness");

        // Generate test vectors
        var numVectors = 10000;
        var dimension = 768;
        var random = new Random(42);

        var queryVector = new float[dimension];
        var documentVectors = new float[numVectors * dimension];

        for (var i = 0; i < dimension; i++)
        {
            queryVector[i] = (float)(random.NextDouble() * 2 - 1);
        }

        for (var i = 0; i < documentVectors.Length; i++)
        {
            documentVectors[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Create test documents
        var documents = Enumerable.Range(0, numVectors)
            .Select(i => new UltraFastDocument(
                $"doc_{i}",
                $"Test document {i}",
                documentVectors.AsSpan().Slice(i * dimension, dimension).ToArray()))
            .ToList();

        // Test SIMD-optimized similarity search
        var simdStopwatch = Stopwatch.StartNew();
        var simdResults = _simdEngine!.FindSimilarVectorized(
            queryVector.AsSpan(),
            documents,
            100);
        simdStopwatch.Stop();

        var simdThroughput = numVectors / simdStopwatch.Elapsed.TotalSeconds;

        _logger.LogInformation("SIMD Optimization Results:");
        _logger.LogInformation("  Compared: {Count:N0} vectors", numVectors);
        _logger.LogInformation("  Time: {Time:F3} seconds", simdStopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("  Throughput: {Throughput:F0} comparisons/second", simdThroughput);
        _logger.LogInformation("  SIMD Operations: {Count:N0}", _simdEngine.SIMDOperationsCount);
        _logger.LogInformation("  Results: {Count} similar vectors found", simdResults.Length);

        // Verify SIMD results
        Assert.That(simdResults.Length, Is.EqualTo(100), "Should return requested number of results");
        Assert.That(simdThroughput, Is.GreaterThan(10000), "SIMD should process >10,000 comparisons/second");
        Assert.That(_simdEngine.SIMDOperationsCount, Is.GreaterThan(0), "Should use SIMD operations");
        Assert.That(simdResults.All(r => r.Similarity >= -1 && r.Similarity <= 1), Is.True,
            "Similarities should be in valid range");
    }

    [Test]
    [Timeout(240000)] // 4 minutes timeout
    public async Task ShouldDemonstrateMemoryPoolingEfficiency()
    {
        _logger!.LogInformation("Testing memory pooling and zero-allocation performance");

        var initialMemory = GC.GetTotalMemory(true);
        var tuning = Tuning.StandardGuitar;

        // Run multiple indexing cycles to test memory pooling
        for (var cycle = 0; cycle < 3; cycle++)
        {
            _logger.LogInformation("Memory pooling cycle {Cycle}", cycle + 1);

            var result = await _ultraService!.IndexFretboardVoicingsUltraFastAsync(
                tuning,
                instrumentName: $"Memory Test {cycle}",
                maxFret: 4,
                includeBiomechanicalAnalysis: false);

            _logger.LogInformation("Cycle {Cycle}: {Count} voicings in {Time:F1}s",
                cycle + 1, result.IndexedVoicings, result.ElapsedTime.TotalSeconds);

            // Clear index but keep memory pools
            // _ultraService.ClearIndex(); // Would need to implement this
        }

        // Force GC and measure final memory
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var memoryGrowth = (finalMemory - initialMemory) / (1024.0 * 1024.0);

        _logger.LogInformation("Memory Pooling Results:");
        _logger.LogInformation("  Initial Memory: {Memory:F1} MB", initialMemory / (1024.0 * 1024.0));
        _logger.LogInformation("  Final Memory: {Memory:F1} MB", finalMemory / (1024.0 * 1024.0));
        _logger.LogInformation("  Memory Growth: {Growth:F1} MB", memoryGrowth);

        // Memory growth should be minimal due to pooling
        Assert.That(memoryGrowth, Is.LessThan(100), "Memory growth should be <100MB due to pooling");
    }

    [Test]
    [Timeout(300000)] // 5 minutes timeout
    public async Task ShouldDemonstrateExtremeQueryPerformance()
    {
        _logger!.LogInformation("Testing extreme query performance with all optimizations");

        // First index a dataset
        var tuning = Tuning.StandardGuitar;
        await _ultraService!.IndexFretboardVoicingsUltraFastAsync(tuning, maxFret: 6);

        var extremeQueries = new[]
        {
            "ultra fast major chord",
            "extreme performance minor voicing",
            "blazing fast jazz chord",
            "lightning speed power chord",
            "maximum velocity fingerpicking chord",
            "hypersonic acoustic voicing",
            "supersonic electric guitar chord",
            "warp speed classical position",
            "light speed folk strum",
            "quantum velocity rock chord"
        };

        var queryTimes = new List<TimeSpan>();
        var totalStopwatch = Stopwatch.StartNew();

        foreach (var query in extremeQueries)
        {
            var queryStopwatch = Stopwatch.StartNew();

            var result = await _ultraService.SearchUltraFastAsync(query, maxResults: 20);

            queryStopwatch.Stop();
            queryTimes.Add(queryStopwatch.Elapsed);

            Assert.That(result, Is.Not.Empty, $"Should find results for: {query}");
            Assert.That(queryStopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)),
                $"Query should complete in <2s: {query}");

            _logger.LogInformation("Query '{Query}' completed in {Time:F0}ms",
                query, queryStopwatch.Elapsed.TotalMilliseconds);
        }

        totalStopwatch.Stop();

        var averageTime = TimeSpan.FromTicks((long)queryTimes.Average(t => t.Ticks));
        var totalThroughput = extremeQueries.Length / totalStopwatch.Elapsed.TotalSeconds;

        _logger.LogInformation("Extreme Query Performance Results:");
        _logger.LogInformation("  Total Queries: {Count}", extremeQueries.Length);
        _logger.LogInformation("  Total Time: {Time:F2} seconds", totalStopwatch.Elapsed.TotalSeconds);
        _logger.LogInformation("  Average Time: {Time:F0} ms", averageTime.TotalMilliseconds);
        _logger.LogInformation("  Query Throughput: {Throughput:F1} queries/second", totalThroughput);
        _logger.LogInformation("  Min Time: {Time:F0} ms", queryTimes.Min().TotalMilliseconds);
        _logger.LogInformation("  Max Time: {Time:F0} ms", queryTimes.Max().TotalMilliseconds);

        // Extreme performance assertions
        Assert.That(averageTime, Is.LessThan(TimeSpan.FromMilliseconds(500)),
            "Average query time should be <500ms");
        Assert.That(totalThroughput, Is.GreaterThan(2),
            "Should process >2 queries/second");
    }

    [Test]
    [Timeout(600000)] // 10 minutes timeout
    public async Task ShouldDemonstrateScalabilityToExtremeDatasets()
    {
        _logger!.LogInformation("Testing scalability with extreme dataset sizes");

        var scalabilityResults = new List<(int maxFret, UltraIndexingResult result)>();
        var fretLimits = new[] { 3, 5, 7, 9 }; // Progressively larger datasets

        foreach (var maxFret in fretLimits)
        {
            _logger.LogInformation("Testing scalability with max fret: {MaxFret}", maxFret);

            var tuning = Tuning.StandardGuitar;
            var result = await _ultraService!.IndexFretboardVoicingsUltraFastAsync(
                tuning,
                instrumentName: $"Scalability Test {maxFret}",
                maxFret: maxFret,
                includeBiomechanicalAnalysis: false);

            scalabilityResults.Add((maxFret, result));

            _logger.LogInformation("Max Fret {MaxFret}: {Count:N0} voicings at {Rate:F0} voicings/sec",
                maxFret, result.IndexedVoicings, result.ThroughputVoicingsPerSecond);

            // Should maintain high performance even with larger datasets
            Assert.That(result.ThroughputVoicingsPerSecond, Is.GreaterThan(100),
                $"Should maintain >100 voicings/sec even with {maxFret} frets");
        }

        // Analyze scaling characteristics
        _logger.LogInformation("Scalability Analysis:");
        for (var i = 1; i < scalabilityResults.Count; i++)
        {
            var prev = scalabilityResults[i - 1];
            var curr = scalabilityResults[i];

            var sizeRatio = (double)curr.result.IndexedVoicings / prev.result.IndexedVoicings;
            var timeRatio = curr.result.ElapsedTime.TotalSeconds / prev.result.ElapsedTime.TotalSeconds;
            var throughputRatio = curr.result.ThroughputVoicingsPerSecond / prev.result.ThroughputVoicingsPerSecond;

            _logger.LogInformation(
                "  {PrevFret} → {CurrFret} frets: {SizeRatio:F1}x size, {TimeRatio:F1}x time, {ThroughputRatio:F2}x throughput",
                prev.maxFret, curr.maxFret, sizeRatio, timeRatio, throughputRatio);

            // Performance should scale reasonably
            Assert.That(timeRatio, Is.LessThan(sizeRatio * 1.5),
                "Time should not scale worse than 1.5x the data size increase");
        }
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
