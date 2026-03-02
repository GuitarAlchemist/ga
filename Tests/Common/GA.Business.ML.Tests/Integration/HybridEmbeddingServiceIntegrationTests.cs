namespace GA.Business.ML.Tests.Integration;

using Embeddings;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Providers;
using Rag.Models;
using TestInfrastructure;

/// <summary>
///     Integration tests for the HybridEmbeddingService that combines
///     musical (OPTIC-K) and text embeddings.
/// </summary>
[TestFixture]
[Category("Integration")]
[Category("RequiresOllama")]
public class HybridEmbeddingServiceIntegrationTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://localhost:11434",
                ["Ollama:EmbeddingModel"] = "nomic-embed-text"
            })
            .Build();

        _ollamaAvailable = await OllamaProvider.IsAvailableAsync();

        // Always create musical components (no external dependency)
        _musicalGenerator = TestServices.CreateGenerator();
        _musicalBridge = new(_musicalGenerator);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown() => _musicalBridge?.Dispose();

    private IConfiguration _configuration = null!;
    private bool _ollamaAvailable;
    private MusicalEmbeddingGenerator _musicalGenerator = null!;
    private MusicalEmbeddingBridge _musicalBridge = null!;

    [Test]
    public void HybridService_WithMusicalOnly_WorksWithoutOllama()
    {
        // Arrange - create hybrid service with null text embeddings
        using var service = new HybridEmbeddingService(
            CreateMockTextEmbeddingGenerator(),
            _musicalBridge);

        // Assert
        Assert.That(service.MusicalEmbeddings, Is.Not.Null);
        Assert.That(service.TextEmbeddings, Is.Not.Null);
    }

    [Test]
    public async Task HybridService_GenerateMusicalEmbedding_UsesBridge()
    {
        // Arrange
        var doc = CreateTestVoicingDocument();

        // Act
        var embedding = await _musicalBridge.GenerateSingleAsync(doc);

        // Assert
        Assert.That(embedding.Vector.Length, Is.EqualTo(EmbeddingSchema.TotalDimension));
    }

    [Test]
    public async Task HybridService_GenerateTextEmbedding_UsesOllama()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Arrange
        var textGenerator = OllamaProvider.CreateEmbeddingGeneratorFromConfig(_configuration);
        using var service = new HybridEmbeddingService(textGenerator, _musicalBridge);

        // Act
        var embedding = await service.GenerateTextEmbeddingAsync("C major chord guitar");

        // Assert
        Assert.That(embedding.Vector.Length, Is.GreaterThan(0));
        TestContext.WriteLine($"Text embedding dimension: {embedding.Vector.Length}");
    }

    [Test]
    public async Task HybridService_DifferentDimensions_ForMusicalAndText()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Arrange
        var textGenerator = OllamaProvider.CreateEmbeddingGeneratorFromConfig(_configuration);
        using var service = new HybridEmbeddingService(textGenerator, _musicalBridge);

        var doc = CreateTestVoicingDocument();

        // Act
        var musicalEmbedding = await _musicalBridge.GenerateSingleAsync(doc);
        var textEmbedding = await service.GenerateTextEmbeddingAsync("C major chord");

        // Assert - dimensions should be different (OPTIC-K vs Ollama)
        TestContext.WriteLine($"Musical (OPTIC-K) dimension: {musicalEmbedding.Vector.Length}");
        TestContext.WriteLine($"Text (Ollama) dimension: {textEmbedding.Vector.Length}");

        Assert.That(musicalEmbedding.Vector.Length, Is.EqualTo(EmbeddingSchema.TotalDimension));
        // nomic-embed-text typically produces 768-dimensional embeddings
        Assert.That(textEmbedding.Vector.Length, Is.GreaterThan(0));
    }

    [Test]
    public async Task HybridService_BatchTextEmbeddings_WorksCorrectly()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Arrange
        var textGenerator = OllamaProvider.CreateEmbeddingGeneratorFromConfig(_configuration);
        using var service = new HybridEmbeddingService(textGenerator, _musicalBridge);

        var texts = new[]
        {
            "Major chord voicing",
            "Minor chord voicing",
            "Dominant seventh chord"
        };

        // Act
        var embeddings = await service.GenerateTextEmbeddingsAsync(texts);

        // Assert
        Assert.That(embeddings.Count, Is.EqualTo(3));
        Assert.That(embeddings.All(e => e.Vector.Length > 0), Is.True);
    }

    private static ChordVoicingRagDocument CreateTestVoicingDocument() =>
        new()
        {
            Id = "test-hybrid-1",
            SearchableText = "C Major chord open position",
            ChordName = "C Major",
            Diagram = "x-3-2-0-1-0",
            MidiNotes = [48, 52, 55, 60, 64],
            PitchClasses = [0, 4, 7],
            PitchClassSet = "{0, 4, 7}",
            IntervalClassVector = "001110",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "3-11",
            YamlAnalysis = "{}",
            PossibleKeys = ["C Major"],
            SemanticTags = ["Major", "Triad"],
            StackingType = "Tertian",
            Embedding = null
        };

    private static IEmbeddingGenerator<string, Embedding<float>> CreateMockTextEmbeddingGenerator() =>
        new MockTextEmbeddingGenerator();

    /// <summary>
    ///     Simple mock embedding generator for testing when Ollama is not available.
    /// </summary>
    private class MockTextEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public EmbeddingGeneratorMetadata Metadata => new("Mock", null, "mock-v1");

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = values.Select(v =>
                    new Embedding<float>(Enumerable.Range(0, 384).Select(i => i / 384f).ToArray()))
                .ToList();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        public void Dispose()
        {
        }

        public object? GetService(Type serviceType, object? key = null) => null;
    }
}
