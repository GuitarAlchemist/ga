namespace GA.Business.Core.Tests.Fretboard.SemanticIndexing;

using System.Net.Http;
using Data.SemanticKernel.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
///     Performance tests for semantic fretboard indexing and querying with real Ollama models
///     Measures indexing speed, query response times, and memory usage
/// </summary>
[TestFixture]
[Category("Performance")]
[Category("Ollama")]
[Category("LongRunning")]
public class SemanticFretboardPerformanceTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Check if Ollama is running
        if (!await IsOllamaRunningAsync())
        {
            Assert.Ignore("Ollama is not running. Performance tests require Ollama.");
            return;
        }

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

        _httpClient = new HttpClient { BaseAddress = new Uri(OllamaBaseUrl) };
        services.AddSingleton(_httpClient);

        // Real embedding service
        services.AddSingleton<OllamaTextEmbeddingGeneration>(provider =>
            new OllamaTextEmbeddingGeneration(_httpClient, EmbeddingModel));
        services.AddSingleton<OllamaEmbeddingService>(provider =>
            new OllamaEmbeddingService(provider.GetRequiredService<OllamaTextEmbeddingGeneration>()));
        services.AddSingleton<SemanticSearchService.IEmbeddingService>(provider =>
            provider.GetRequiredService<OllamaEmbeddingService>());

        services.AddSingleton<SemanticSearchService>();

        // Real LLM service
        services.Configure<OllamaLlmOptions>(options =>
        {
            options.BaseUrl = OllamaBaseUrl;
            options.Temperature = 0.7;
        });
        services.AddSingleton<OllamaLlmService>();
        services.AddSingleton<SemanticFretboardService.IOllamaLlmService>(provider =>
            provider.GetRequiredService<OllamaLlmService>());

        services.AddSingleton<SemanticFretboardService>();

        _serviceProvider = services.BuildServiceProvider();
        _semanticService = _serviceProvider.GetRequiredService<SemanticFretboardService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<SemanticFretboardPerformanceTests>>();

        _logger.LogInformation("Performance test setup completed");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
        _serviceProvider?.Dispose();
    }

    private ServiceProvider? _serviceProvider;
    private SemanticFretboardService? _semanticService;
    private ILogger<SemanticFretboardPerformanceTests>? _logger;
    private HttpClient? _httpClient;

    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string EmbeddingModel = "nomic-embed-text";

    [Test]
    [Timeout(1800000)] // 30 minutes timeout for full indexing
    public async Task ShouldIndexLargeFretboardDatasetWithinReasonableTime()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        var maxFret = 12; // Full fretboard
        var progress = new Progress<IndexingProgress>();
        var progressUpdates = new List<IndexingProgress>();
        var memoryBefore = GC.GetTotalMemory(true);

        progress.ProgressChanged += (_, p) =>
        {
            progressUpdates.Add(p);
            if (p.Indexed % 500 == 0)
            {
                _logger!.LogInformation("Progress: {Indexed}/{Total} ({Percent:F1}%) - {Rate:F0} voicings/sec",
                    p.Indexed, p.Total, p.PercentComplete,
                    progressUpdates.Count > 1 ? p.Indexed / (DateTime.Now - startTime).TotalSeconds : 0);
            }
        };

        var startTime = DateTime.Now;

        // Act
        var result = await _semanticService!.IndexFretboardVoicingsAsync(
            tuning,
            instrumentName: "Standard Guitar",
            maxFret: maxFret,
            includeBiomechanicalAnalysis: true,
            progress: progress);

        var memoryAfter = GC.GetTotalMemory(true);
        var memoryUsed = (memoryAfter - memoryBefore) / (1024 * 1024); // MB

        // Assert performance requirements
        Assert.That(result.IndexedVoicings, Is.GreaterThan(1000), "Should index substantial number of voicings");
        Assert.That(result.IndexingRate, Is.GreaterThan(10), "Should index at least 10 voicings per second");
        Assert.That(result.SuccessRate, Is.GreaterThan(0.95), "Should have very high success rate");
        Assert.That(result.ElapsedTime, Is.LessThan(TimeSpan.FromMinutes(20)), "Should complete within 20 minutes");

        // Memory usage should be reasonable
        Assert.That(memoryUsed, Is.LessThan(2000), "Should use less than 2GB memory");

        _logger!.LogInformation("Performance Results:");
        _logger.LogInformation("  Indexed: {Count:N0} voicings", result.IndexedVoicings);
        _logger.LogInformation("  Time: {Time:F1} seconds", result.ElapsedTime.TotalSeconds);
        _logger.LogInformation("  Rate: {Rate:F1} voicings/second", result.IndexingRate);
        _logger.LogInformation("  Success Rate: {Rate:P2}", result.SuccessRate);
        _logger.LogInformation("  Memory Used: {Memory:F1} MB", memoryUsed);

        // Verify index statistics
        var stats = _semanticService.GetIndexStatistics();
        Assert.That(stats.TotalDocuments, Is.EqualTo(result.IndexedVoicings));
        Assert.That(stats.EmbeddingDimension, Is.EqualTo(768));
    }

    [Test]
    [Timeout(300000)] // 5 minutes timeout
    public async Task ShouldProvideSubSecondQueryResponseTimes()
    {
        // Arrange - Index a reasonable dataset first
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(tuning, maxFret: 7);

        var testQueries = new[]
        {
            "easy major chords",
            "jazz seventh chords",
            "minor barre chords",
            "open string voicings",
            "power chords for rock",
            "fingerpicking chords",
            "sus chords",
            "diminished voicings",
            "add9 chords",
            "folk guitar chords"
        };

        var queryTimes = new List<TimeSpan>();

        // Act & Assert
        foreach (var query in testQueries)
        {
            var stopwatch = Stopwatch.StartNew();

            var result = await _semanticService.ProcessNaturalLanguageQueryAsync(query, maxResults: 10);

            stopwatch.Stop();
            queryTimes.Add(stopwatch.Elapsed);

            // Individual query assertions
            Assert.That(result.SearchResults, Is.Not.Empty, $"Should find results for: {query}");
            Assert.That(result.LlmInterpretation, Is.Not.Null.And.Not.Empty, $"Should get LLM response for: {query}");
            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(10)),
                $"Query '{query}' took too long: {stopwatch.Elapsed.TotalSeconds:F2}s");

            _logger!.LogInformation("Query '{Query}' completed in {Time:F2}s", query, stopwatch.Elapsed.TotalSeconds);
        }

        // Overall performance assertions
        var averageTime = TimeSpan.FromTicks((long)queryTimes.Average(t => t.Ticks));
        var maxTime = queryTimes.Max();
        var minTime = queryTimes.Min();

        Assert.That(averageTime, Is.LessThan(TimeSpan.FromSeconds(5)),
            $"Average query time should be under 5 seconds, was {averageTime.TotalSeconds:F2}s");
        Assert.That(maxTime, Is.LessThan(TimeSpan.FromSeconds(15)),
            $"No query should take more than 15 seconds, max was {maxTime.TotalSeconds:F2}s");

        _logger!.LogInformation("Query Performance Summary:");
        _logger.LogInformation("  Average: {Avg:F2}s", averageTime.TotalSeconds);
        _logger.LogInformation("  Min: {Min:F2}s", minTime.TotalSeconds);
        _logger.LogInformation("  Max: {Max:F2}s", maxTime.TotalSeconds);
        _logger.LogInformation("  Total Queries: {Count}", testQueries.Length);
    }

    [Test]
    [Timeout(600000)] // 10 minutes timeout
    public async Task ShouldScaleLinearlyWithDatasetSize()
    {
        // Test indexing performance at different scales
        var testSizes = new[] { 3, 5, 7 }; // Max fret limits
        var results = new List<(int maxFret, IndexingResult result)>();

        foreach (var maxFret in testSizes)
        {
            _logger!.LogInformation("Testing indexing performance with max fret: {MaxFret}", maxFret);

            // Clear previous index
            _semanticService!.ClearIndex();

            var tuning = Tuning.StandardGuitar;
            var result = await _semanticService.IndexFretboardVoicingsAsync(
                tuning,
                maxFret: maxFret,
                includeBiomechanicalAnalysis: false); // Faster without biomechanical analysis

            results.Add((maxFret, result));

            _logger.LogInformation("Max Fret {MaxFret}: {Count} voicings in {Time:F1}s ({Rate:F1}/sec)",
                maxFret, result.IndexedVoicings, result.ElapsedTime.TotalSeconds, result.IndexingRate);
        }

        // Analyze scaling
        for (var i = 1; i < results.Count; i++)
        {
            var prev = results[i - 1];
            var curr = results[i];

            var sizeRatio = (double)curr.result.IndexedVoicings / prev.result.IndexedVoicings;
            var timeRatio = curr.result.ElapsedTime.TotalSeconds / prev.result.ElapsedTime.TotalSeconds;

            _logger!.LogInformation("Scaling from {PrevFret} to {CurrFret} frets:", prev.maxFret, curr.maxFret);
            _logger.LogInformation("  Size ratio: {SizeRatio:F2}x", sizeRatio);
            _logger.LogInformation("  Time ratio: {TimeRatio:F2}x", timeRatio);

            // Time should scale reasonably with size (not exponentially)
            Assert.That(timeRatio, Is.LessThan(sizeRatio * 2),
                "Indexing time should not scale exponentially with dataset size");
        }
    }

    [Test]
    [Timeout(300000)] // 5 minutes timeout
    public async Task ShouldMaintainPerformanceUnderConcurrentQueries()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(tuning, maxFret: 5);

        var queries = new[]
        {
            "major chords",
            "minor chords",
            "seventh chords",
            "sus chords",
            "power chords"
        };

        var concurrentTasks = 5;
        var queriesPerTask = 3;

        // Act - Run concurrent queries
        var tasks = Enumerable.Range(0, concurrentTasks)
            .Select(async taskId =>
            {
                var taskResults = new List<(string query, TimeSpan elapsed)>();

                for (var i = 0; i < queriesPerTask; i++)
                {
                    var query = queries[i % queries.Length];
                    var stopwatch = Stopwatch.StartNew();

                    var result = await _semanticService.ProcessNaturalLanguageQueryAsync(
                        $"{query} task {taskId}");

                    stopwatch.Stop();
                    taskResults.Add((query, stopwatch.Elapsed));

                    Assert.That(result.SearchResults, Is.Not.Empty);
                    Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
                }

                return taskResults;
            });

        var allResults = await Task.WhenAll(tasks);

        // Assert
        var flatResults = allResults.SelectMany(r => r).ToList();
        var averageTime = TimeSpan.FromTicks((long)flatResults.Average(r => r.elapsed.Ticks));
        var maxTime = flatResults.Max(r => r.elapsed);

        Assert.That(averageTime, Is.LessThan(TimeSpan.FromSeconds(8)),
            "Concurrent queries should maintain reasonable average response time");
        Assert.That(maxTime, Is.LessThan(TimeSpan.FromSeconds(20)),
            "No concurrent query should take excessively long");

        _logger!.LogInformation("Concurrent Query Performance:");
        _logger.LogInformation("  Total queries: {Count}", flatResults.Count);
        _logger.LogInformation("  Average time: {Avg:F2}s", averageTime.TotalSeconds);
        _logger.LogInformation("  Max time: {Max:F2}s", maxTime.TotalSeconds);
    }

    [Test]
    [Timeout(180000)] // 3 minutes timeout
    public async Task ShouldMaintainMemoryUsageUnderRepeatedOperations()
    {
        // Test for memory leaks during repeated operations
        var tuning = Tuning.StandardGuitar;
        var iterations = 10;
        var memoryMeasurements = new List<long>();

        for (var i = 0; i < iterations; i++)
        {
            // Clear and re-index
            _semanticService!.ClearIndex();

            await _semanticService.IndexFretboardVoicingsAsync(tuning, maxFret: 4);

            // Perform some queries
            await _semanticService.ProcessNaturalLanguageQueryAsync("test query " + i);

            // Force garbage collection and measure memory
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var memory = GC.GetTotalMemory(false);
            memoryMeasurements.Add(memory);

            _logger!.LogInformation("Iteration {Iteration}: {Memory:F1} MB",
                i + 1, memory / (1024.0 * 1024.0));
        }

        // Assert memory is not growing significantly
        var firstMemory = memoryMeasurements[0];
        var lastMemory = memoryMeasurements.Last();
        var memoryGrowth = (double)(lastMemory - firstMemory) / firstMemory;

        Assert.That(memoryGrowth, Is.LessThan(0.5), // Less than 50% growth
            $"Memory usage should not grow significantly. Growth: {memoryGrowth:P2}");

        _logger!.LogInformation("Memory Growth Analysis:");
        _logger.LogInformation("  First: {First:F1} MB", firstMemory / (1024.0 * 1024.0));
        _logger.LogInformation("  Last: {Last:F1} MB", lastMemory / (1024.0 * 1024.0));
        _logger.LogInformation("  Growth: {Growth:P2}", memoryGrowth);
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
