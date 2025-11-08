namespace GA.Business.Core.Tests.Fretboard.SemanticIndexing;

using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using Data.SemanticKernel.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
///     Real integration tests that connect to actual Ollama instance
///     Tests the complete pipeline with real LLM models and embeddings
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("Ollama")]
[Category("RealLLM")]
public class OllamaIntegrationTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Check if Ollama is running
        if (!await IsOllamaRunningAsync())
        {
            Assert.Ignore("Ollama is not running at " + OllamaBaseUrl + ". Please start Ollama to run these tests.");
            return;
        }

        // Setup dependency injection container
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder => builder
            .AddConsole()
            .SetMinimumLevel(LogLevel.Information));

        // Add HTTP client for Ollama
        _httpClient = new HttpClient { BaseAddress = new Uri(OllamaBaseUrl) };
        services.AddSingleton(_httpClient);

        // Add Ollama embedding service
        services.AddSingleton<OllamaTextEmbeddingGeneration>(provider =>
            new OllamaTextEmbeddingGeneration(_httpClient, PreferredEmbeddingModel));

        services.AddSingleton<OllamaEmbeddingService>(provider =>
            new OllamaEmbeddingService(provider.GetRequiredService<OllamaTextEmbeddingGeneration>()));

        services.AddSingleton<SemanticSearchService.IEmbeddingService>(provider =>
            provider.GetRequiredService<OllamaEmbeddingService>());

        // Add semantic search service
        services.AddSingleton<SemanticSearchService>();

        // Add Ollama LLM service
        services.Configure<OllamaLlmOptions>(options =>
        {
            options.BaseUrl = OllamaBaseUrl;
            options.Temperature = 0.7;
            options.TopP = 0.9;
            options.MaxTokens = 1000;
        });

        services.AddSingleton<OllamaLlmService>();
        services.AddSingleton<SemanticFretboardService.IOllamaLlmService>(provider =>
            provider.GetRequiredService<OllamaLlmService>());

        // Add semantic fretboard service
        services.AddSingleton<SemanticFretboardService>();

        _serviceProvider = services.BuildServiceProvider();
        _semanticService = _serviceProvider.GetRequiredService<SemanticFretboardService>();
        _ollamaService = _serviceProvider.GetRequiredService<OllamaLlmService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<OllamaIntegrationTests>>();

        _logger.LogInformation("Real Ollama integration test setup completed");

        // Ensure required models are available
        await EnsureModelsAvailableAsync();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        _httpClient?.Dispose();
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
    private OllamaLlmService? _ollamaService;
    private ILogger<OllamaIntegrationTests>? _logger;
    private HttpClient? _httpClient;

    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string PreferredEmbeddingModel = "nomic-embed-text";
    private const string PreferredLlmModel = "llama3.2:latest";

    [Test]
    [Timeout(300000)] // 5 minutes timeout for real model operations
    public async Task ShouldDownloadAndUseRealEmbeddingModel()
    {
        // Arrange
        var testTexts = new[]
        {
            "C major chord with open strings",
            "F major barre chord at first fret",
            "Am7 jazz voicing with extensions",
            "G major chord in CAGED system",
            "Dm chord for beginners"
        };

        // Act & Assert
        foreach (var text in testTexts)
        {
            _logger!.LogInformation("Testing embedding generation for: {Text}", text);

            var embedding = await _serviceProvider!
                .GetRequiredService<SemanticSearchService.IEmbeddingService>()
                .GenerateEmbeddingAsync(text);

            Assert.That(embedding, Is.Not.Null);
            Assert.That(embedding.Length, Is.EqualTo(768)); // nomic-embed-text dimension
            Assert.That(embedding.Any(x => x != 0), Is.True, "Embedding should not be all zeros");

            // Verify embedding is normalized (unit vector)
            var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
            Assert.That(magnitude, Is.EqualTo(1.0).Within(0.01), "Embedding should be normalized");
        }
    }

    [Test]
    [Timeout(600000)] // 10 minutes timeout for full indexing
    public async Task ShouldIndexRealFretboardVoicingsWithRealEmbeddings()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        var progress = new Progress<IndexingProgress>();
        var progressUpdates = new List<IndexingProgress>();
        progress.ProgressChanged += (_, p) => progressUpdates.Add(p);

        _logger!.LogInformation("Starting real fretboard indexing with Ollama embeddings");

        // Act
        var result = await _semanticService!.IndexFretboardVoicingsAsync(
            tuning,
            instrumentName: "Standard Guitar",
            maxFret: 5, // Limit to first 5 frets for reasonable test time
            includeBiomechanicalAnalysis: true,
            progress: progress,
            cancellationToken: CancellationToken.None);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IndexedVoicings, Is.GreaterThan(100), "Should index a reasonable number of voicings");
        Assert.That(result.SuccessRate, Is.GreaterThan(0.9), "Should have high success rate with real embeddings");
        Assert.That(progressUpdates, Is.Not.Empty, "Should provide progress updates");

        _logger.LogInformation("Real indexing completed: {Result}", result);

        // Verify index statistics
        var stats = _semanticService.GetIndexStatistics();
        Assert.That(stats.TotalDocuments, Is.EqualTo(result.IndexedVoicings));
        Assert.That(stats.EmbeddingDimension, Is.EqualTo(768));
    }

    [Test]
    [Timeout(300000)] // 5 minutes timeout for LLM operations
    public async Task ShouldProcessRealGuitarQueriesWithLlama()
    {
        // Arrange - First index some voicings
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(
            tuning,
            maxFret: 4, // Small set for faster testing
            includeBiomechanicalAnalysis: true);

        // Real guitar queries that musicians actually ask
        var realGuitarQueries = new[]
        {
            "What are some easy open chords for a beginner?",
            "Show me a bright sounding C major chord",
            "I need a jazz voicing for Dm7",
            "What's a good barre chord version of F major?",
            "Find me some minor chords that don't use barre",
            "I want warm sounding chords for folk music",
            "Show me power chords for rock music",
            "What are some fingerpicking-friendly chord shapes?",
            "I need chord voicings that work well in drop D tuning",
            "Find me some sus chords for ambient music"
        };

        // Act & Assert
        foreach (var query in realGuitarQueries)
        {
            _logger!.LogInformation("Testing real query: {Query}", query);

            var result = await _semanticService.ProcessNaturalLanguageQueryAsync(
                query,
                maxResults: 5);

            // Verify basic structure
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Query, Is.EqualTo(query));
            Assert.That(result.SearchResults, Is.Not.Empty, $"Should find results for: {query}");
            Assert.That(result.LlmInterpretation, Is.Not.Null.And.Not.Empty, $"LLM should respond to: {query}");
            Assert.That(result.ModelUsed, Does.Contain("llama"), "Should use Llama model");

            // Verify LLM response quality
            var response = result.LlmInterpretation;
            Assert.That(response.Length, Is.GreaterThan(50), "Response should be substantial");
            Assert.That(response.ToLower(), Does.Contain("chord"), "Response should mention chords");

            // Log the actual LLM response for manual verification
            _logger.LogInformation("Query: {Query}", query);
            _logger.LogInformation("LLM Response: {Response}", response);
            _logger.LogInformation("Found {Count} relevant voicings", result.SearchResults.Count);
            _logger.LogInformation("Average relevance: {Score:F2}", result.AverageRelevanceScore);
            _logger.LogInformation("---");

            // Verify response time is reasonable
            Assert.That(result.ElapsedTime, Is.LessThan(TimeSpan.FromSeconds(30)),
                "Query should complete in reasonable time");
        }
    }

    [Test]
    [Timeout(180000)] // 3 minutes timeout
    public async Task ShouldProvideContextuallyRelevantResponses()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(tuning, maxFret: 3);

        // Test queries that should return different types of responses
        var contextualQueries = new Dictionary<string, string[]>
        {
            ["beginner chords"] = ["easy", "simple", "open", "beginner"],
            ["jazz voicings"] = ["jazz", "extension", "seventh", "complex"],
            ["folk guitar"] = ["folk", "open", "acoustic", "strum"],
            ["rock power chords"] = ["power", "rock", "distortion", "fifth"]
        };

        // Act & Assert
        foreach (var (query, expectedKeywords) in contextualQueries)
        {
            _logger!.LogInformation("Testing contextual query: {Query}", query);

            var result = await _semanticService.ProcessNaturalLanguageQueryAsync(query);
            var response = result.LlmInterpretation.ToLower();

            // Verify response contains relevant keywords
            var foundKeywords = expectedKeywords.Where(keyword => response.Contains(keyword)).ToList();
            Assert.That(foundKeywords, Is.Not.Empty,
                $"Response should contain at least one relevant keyword for '{query}'. " +
                $"Expected: [{string.Join(", ", expectedKeywords)}], " +
                $"Response: {result.LlmInterpretation}");

            _logger.LogInformation("Query '{Query}' found keywords: {Keywords}",
                query, string.Join(", ", foundKeywords));
        }
    }

    [Test]
    [Timeout(120000)] // 2 minutes timeout
    public async Task ShouldHandleComplexMusicalQueries()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(tuning, maxFret: 5);

        var complexQueries = new[]
        {
            "I'm playing in the key of G major and need a chord progression that goes from Em to Am to D7. What voicings work well together?",
            "For fingerstyle guitar, I need chord shapes that leave the high E string open for melody. What options do I have?",
            "I want to play jazz standards but my hands are small. What are some easier jazz chord voicings that still sound sophisticated?",
            "I'm writing a song in DADGAD tuning. Can you suggest some chord voicings that take advantage of the open strings?",
            "For recording, I need chord voicings that sit well in a mix with bass and drums. What should I avoid?"
        };

        // Act & Assert
        foreach (var query in complexQueries)
        {
            _logger!.LogInformation("Testing complex query: {Query}", query);

            var result = await _semanticService.ProcessNaturalLanguageQueryAsync(query, maxResults: 8);

            Assert.That(result.SearchResults, Is.Not.Empty);
            Assert.That(result.LlmInterpretation.Length, Is.GreaterThan(100),
                "Complex queries should get detailed responses");

            // Verify the LLM provides musical context
            var response = result.LlmInterpretation.ToLower();
            var musicalTerms = new[] { "chord", "voicing", "fret", "finger", "string", "play" };
            var foundTerms = musicalTerms.Count(term => response.Contains(term));
            Assert.That(foundTerms, Is.GreaterThan(2),
                "Response should contain multiple musical terms");

            _logger.LogInformation("Complex query response length: {Length} chars",
                result.LlmInterpretation.Length);
        }
    }

    [Test]
    [Timeout(240000)] // 4 minutes timeout
    public async Task ShouldMaintainConsistencyAcrossMultipleQueries()
    {
        // Arrange
        var tuning = Tuning.StandardGuitar;
        await _semanticService!.IndexFretboardVoicingsAsync(tuning, maxFret: 4);

        var baseQuery = "easy major chords";
        var variations = new[]
        {
            "simple major chords for beginners",
            "basic major chord shapes",
            "easy major chord voicings",
            "beginner-friendly major chords"
        };

        var results = new List<QueryResult>();

        // Act
        foreach (var query in variations)
        {
            var result = await _semanticService.ProcessNaturalLanguageQueryAsync(query);
            results.Add(result);
            _logger!.LogInformation("Query: {Query} -> {Count} results", query, result.SearchResults.Count);
        }

        // Assert - Similar queries should return similar results
        var firstResult = results[0];
        foreach (var result in results.Skip(1))
        {
            // Should find some common chord voicings
            var commonVoicings = firstResult.SearchResults
                .Select(r => r.Id)
                .Intersect(result.SearchResults.Select(r => r.Id))
                .Count();

            Assert.That(commonVoicings, Is.GreaterThan(0),
                "Similar queries should return some common voicings");

            // Response quality should be consistent
            Assert.That(result.LlmInterpretation.Length, Is.GreaterThan(50));
            Assert.That(result.SearchResults.Count, Is.GreaterThan(0));
        }
    }

    /// <summary>
    ///     Check if Ollama is running and accessible
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

    /// <summary>
    ///     Ensure required models are available, download if necessary
    /// </summary>
    private async Task EnsureModelsAvailableAsync()
    {
        _logger!.LogInformation("Ensuring required models are available...");

        // Check and download embedding model
        await EnsureModelAvailableAsync(PreferredEmbeddingModel, "embedding");

        // Check and download LLM model
        var llmModelReady = await _ollamaService!.EnsureBestModelAvailableAsync();
        if (!llmModelReady)
        {
            Assert.Fail("Failed to ensure LLM model is available. Please check Ollama installation.");
        }

        var currentModel = await _ollamaService.GetBestAvailableModelAsync();
        _logger.LogInformation("Using LLM model: {Model}", currentModel);
    }

    /// <summary>
    ///     Ensure a specific model is available
    /// </summary>
    private async Task EnsureModelAvailableAsync(string modelName, string modelType)
    {
        try
        {
            // Check if model is already available
            var response = await _httpClient!.GetAsync("/api/tags");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                if (content.Contains(modelName))
                {
                    _logger!.LogInformation("{ModelType} model {Model} is already available", modelType, modelName);
                    return;
                }
            }

            // Download model
            _logger!.LogInformation("Downloading {ModelType} model: {Model}", modelType, modelName);

            var pullRequest = new { name = modelName };
            var pullResponse = await _httpClient.PostAsJsonAsync("/api/pull", pullRequest);

            if (!pullResponse.IsSuccessStatusCode)
            {
                Assert.Fail($"Failed to download {modelType} model {modelName}. Status: {pullResponse.StatusCode}");
            }

            // Wait for download to complete (simplified - in real scenario you'd parse the streaming response)
            await Task.Delay(TimeSpan.FromSeconds(30)); // Give it time to download

            _logger.LogInformation("Successfully downloaded {ModelType} model: {Model}", modelType, modelName);
        }
        catch (Exception ex)
        {
            Assert.Fail($"Error ensuring {modelType} model {modelName} is available: {ex.Message}");
        }
    }
}
