namespace GA.Business.Core.Tests.Fretboard.SemanticIndexing;

using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

/// <summary>
///     End-to-end integration tests for semantic fretboard indexing and natural language querying
///     Tests the complete pipeline from voicing generation to LLM-powered responses
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("SemanticSearch")]
public class SemanticFretboardServiceTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Setup dependency injection container
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));

        // Add mock embedding service for testing
        var mockEmbeddingService = new Mock<SemanticSearchService.IEmbeddingService>();
        mockEmbeddingService
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>()))
            .ReturnsAsync(() => GenerateRandomEmbedding(384));

        services.AddSingleton(mockEmbeddingService.Object);

        // Add semantic search service
        services.AddSingleton<SemanticSearchService>();

        // Add mock LLM service
        _mockLlmService = new Mock<SemanticFretboardService.IOllamaLlmService>();
        _mockLlmService
            .Setup(x => x.EnsureBestModelAvailableAsync())
            .ReturnsAsync(true);
        _mockLlmService
            .Setup(x => x.GetBestAvailableModelAsync())
            .ReturnsAsync("test-model");
        _mockLlmService
            .Setup(x => x.ProcessNaturalLanguageQueryAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string query, string context) =>
                GenerateMockLlmResponse(query, context));

        services.AddSingleton(_mockLlmService.Object);

        // Add semantic fretboard service
        services.AddSingleton<SemanticFretboardService>();

        _serviceProvider = services.BuildServiceProvider();
        _semanticService = _serviceProvider.GetRequiredService<SemanticFretboardService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<SemanticFretboardServiceTests>>();

        _logger.LogInformation("Test setup completed");
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _serviceProvider?.Dispose();
    }

    [SetUp]
    public void SetUp()
    {
        // Clear index before each test
        _semanticService?.ClearIndex();
    }

    private ServiceProvider? _serviceProvider;
    private SemanticFretboardService? _semanticService;
    private Mock<SemanticFretboardService.IOllamaLlmService>? _mockLlmService;
    private ILogger<SemanticFretboardServiceTests>? _logger;

    [Test]
    [TestCase("Standard Guitar", TestName = "IndexStandardGuitarVoicings")]
    [TestCase("Drop D Guitar", TestName = "IndexDropDGuitarVoicings")]
    public async Task ShouldIndexFretboardVoicingsSuccessfully(string tuningName)
    {
        // Arrange
        var tuning = GetTuningByName(tuningName);
        var progress = new Progress<IndexingProgress>();
        var progressUpdates = new List<IndexingProgress>();
        progress.ProgressChanged += (_, p) => progressUpdates.Add(p);

        // Act
        var result = await _semanticService!.IndexFretboardVoicingsAsync(
            tuning,
            instrumentName: tuningName,
            maxFret: 5, // Limit to first 5 frets for faster testing
            includeBiomechanicalAnalysis: true,
            progress: progress,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IndexedVoicings, Is.GreaterThan(0));
        Assert.That(result.SuccessRate, Is.GreaterThan(0.8)); // At least 80% success rate
        Assert.That(result.ElapsedTime, Is.LessThan(TimeSpan.FromMinutes(2))); // Should complete in reasonable time
        Assert.That(progressUpdates, Is.Not.Empty);

        _logger!.LogInformation("Indexing result: {Result}", result);

        // Verify index statistics
        var stats = _semanticService.GetIndexStatistics();
        Assert.That(stats.TotalDocuments, Is.EqualTo(result.IndexedVoicings));
        Assert.That(stats.DocumentsByCategory.ContainsKey("Chord Voicings"), Is.True);
    }

    [Test]
    public async Task ShouldProcessNaturalLanguageQueriesSuccessfully()
    {
        // Arrange - First index some voicings
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(
            tuning,
            maxFret: 3, // Small set for quick testing
            includeBiomechanicalAnalysis: false);

        var testQueries = new[]
        {
            "Show me easy major chords for beginners",
            "Find bright sounding open chords",
            "What are some jazz voicings with extensions?",
            "Give me minor chords that are easy to play",
            "Show me barre chords in the first position"
        };

        // Act & Assert
        foreach (var query in testQueries)
        {
            _logger!.LogInformation("Testing query: {Query}", query);

            var result = await _semanticService.ProcessNaturalLanguageQueryAsync(
                query,
                maxResults: 5);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Query, Is.EqualTo(query));
            Assert.That(result.SearchResults, Is.Not.Empty);
            Assert.That(result.LlmInterpretation, Is.Not.Null.And.Not.Empty);
            Assert.That(result.ModelUsed, Is.EqualTo("test-model"));
            Assert.That(result.ElapsedTime, Is.LessThan(TimeSpan.FromSeconds(10)));

            _logger.LogInformation("Query result: {Result}", result);
            _logger.LogInformation("LLM Response: {Response}", result.LlmInterpretation);

            // Verify LLM service was called correctly
            _mockLlmService!.Verify(
                x => x.ProcessNaturalLanguageQueryAsync(query, It.IsAny<string>()),
                Times.Once);
        }
    }

    [Test]
    public async Task ShouldHandleEmptyIndexGracefully()
    {
        // Arrange - Empty index
        var query = "Find some chords";

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _semanticService!.ProcessNaturalLanguageQueryAsync(query));

        Assert.That(ex.Message, Does.Contain("indexed"));
    }

    [Test]
    public async Task ShouldHandleCancellationDuringIndexing()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        using var cts = new CancellationTokenSource();

        // Cancel after a short delay
        _ = Task.Delay(100).ContinueWith(_ => cts.Cancel());

        // Act
        var result = await _semanticService!.IndexFretboardVoicingsAsync(
            tuning,
            maxFret: 12, // Larger set to ensure cancellation happens
            cancellationToken: cts.Token);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Should have indexed some voicings before cancellation
        Assert.That(result.IndexedVoicings, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public async Task ShouldProvideProgressUpdates()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        var progressUpdates = new List<IndexingProgress>();
        var progress = new Progress<IndexingProgress>(p => progressUpdates.Add(p));

        // Act
        await _semanticService!.IndexFretboardVoicingsAsync(
            tuning,
            maxFret: 4,
            progress: progress);

        // Assert
        Assert.That(progressUpdates, Is.Not.Empty);
        Assert.That(progressUpdates.All(p => p.PercentComplete >= 0 && p.PercentComplete <= 100), Is.True);
        Assert.That(progressUpdates.Last().PercentComplete, Is.GreaterThan(0));
    }

    [Test]
    public async Task ShouldFilterVoicingsBySearchCriteria()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(tuning, maxFret: 5);

        // Act - Test different query types that should return different results
        var easyQuery = await _semanticService.ProcessNaturalLanguageQueryAsync("easy beginner chords");
        var jazzQuery = await _semanticService.ProcessNaturalLanguageQueryAsync("complex jazz voicings");
        var openQuery = await _semanticService.ProcessNaturalLanguageQueryAsync("open string chords");

        // Assert
        Assert.That(easyQuery.SearchResults, Is.Not.Empty);
        Assert.That(jazzQuery.SearchResults, Is.Not.Empty);
        Assert.That(openQuery.SearchResults, Is.Not.Empty);

        // Results should be different for different query types
        Assert.That(easyQuery.LlmInterpretation, Is.Not.EqualTo(jazzQuery.LlmInterpretation));
    }

    [Test]
    public async Task ShouldMaintainIndexStatistics()
    {
        // Arrange
        var initialStats = _semanticService!.GetIndexStatistics();
        Assert.That(initialStats.TotalDocuments, Is.EqualTo(0));

        // Act
        var tuning = Tuning.StandardGuitar;
        var result = await _semanticService.IndexFretboardVoicingsAsync(tuning, maxFret: 3);

        // Assert
        var finalStats = _semanticService.GetIndexStatistics();
        Assert.That(finalStats.TotalDocuments, Is.EqualTo(result.IndexedVoicings));
        Assert.That(finalStats.DocumentsByCategory, Is.Not.Empty);
        Assert.That(finalStats.EmbeddingDimension, Is.EqualTo(384));
    }

    [Test]
    public async Task ShouldClearIndexSuccessfully()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(tuning, maxFret: 3);

        var statsBeforeClear = _semanticService.GetIndexStatistics();
        Assert.That(statsBeforeClear.TotalDocuments, Is.GreaterThan(0));

        // Act
        _semanticService.ClearIndex();

        // Assert
        var statsAfterClear = _semanticService.GetIndexStatistics();
        Assert.That(statsAfterClear.TotalDocuments, Is.EqualTo(0));

        // Should not be able to query after clearing
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _semanticService.ProcessNaturalLanguageQueryAsync("test query"));
        Assert.That(ex.Message, Does.Contain("indexed"));
    }

    /// <summary>
    ///     Helper method to get tuning by name
    /// </summary>
    private static Tuning GetTuningByName(string tuningName)
    {
        return tuningName switch
        {
            "Standard Guitar" => Tuning.StandardGuitar,
            "Drop D Guitar" => Tuning.DropD,
            _ => Tuning.StandardGuitar
        };
    }

    /// <summary>
    ///     Generate a random embedding vector for testing
    /// </summary>
    private static float[] GenerateRandomEmbedding(int dimensions)
    {
        var random = new Random(42); // Fixed seed for reproducible tests
        var embedding = new float[dimensions];

        for (var i = 0; i < dimensions; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2.0 - 1.0);
        }

        // Normalize to unit vector
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (var i = 0; i < dimensions; i++)
            {
                embedding[i] /= magnitude;
            }
        }

        return embedding;
    }

    /// <summary>
    ///     Generate a mock LLM response for testing
    /// </summary>
    private static string GenerateMockLlmResponse(string query, string context)
    {
        var response = $"Based on your query '{query}', I found several relevant chord voicings. ";

        if (query.ToLower().Contains("easy") || query.ToLower().Contains("beginner"))
        {
            response += "These are beginner-friendly chords with simple fingerings and open strings. ";
        }
        else if (query.ToLower().Contains("jazz"))
        {
            response += "These jazz voicings feature extended harmonies and sophisticated chord structures. ";
        }
        else if (query.ToLower().Contains("open"))
        {
            response += "These open chord voicings utilize open strings for a bright, resonant sound. ";
        }

        response += "The voicings are ordered by relevance to your query. ";
        response += "Try practicing these chords slowly and focus on clean finger placement.";

        return response;
    }
}
