namespace GA.Business.ML.Tests.Integration;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Providers;

/// <summary>
///     Integration tests for the Ollama provider.
///     These tests require Ollama to be running locally.
/// </summary>
/// <remarks>
///     To run these tests:
///     1. Install Ollama: https://ollama.ai
///     2. Start Ollama: ollama serve
///     3. Pull required models: ollama pull llama3.2:3b && ollama pull nomic-embed-text
/// </remarks>
[TestFixture]
[Category("Integration")]
[Category("RequiresOllama")]
[Explicit]
public class OllamaProviderIntegrationTests
{
    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        // Build test configuration
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ollama:BaseUrl"] = "http://localhost:11434",
                ["Ollama:ChatModel"] = "llama3.2:3b",
                ["Ollama:EmbeddingModel"] = "nomic-embed-text"
            })
            .Build();

        // Check if Ollama is available
        _ollamaAvailable = await OllamaProvider.IsAvailableAsync();
        if (!_ollamaAvailable)
        {
            TestContext.WriteLine("⚠️  Ollama is not available. Integration tests will be skipped.");
        }
    }

    private IConfiguration _configuration = null!;
    private bool _ollamaAvailable;

    [Test]
    public void CreateChatClient_WhenOllamaRunning_ReturnsClient()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Act
        var client = OllamaProvider.CreateChatClientFromConfig(_configuration);

        // Assert
        Assert.That(client, Is.Not.Null);
        Assert.That(client, Is.InstanceOf<IChatClient>());
    }

    [Test]
    public async Task ChatClient_SendMessage_ReturnsResponse()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Arrange
        var client = OllamaProvider.CreateChatClientFromConfig(_configuration);
        var messages = new[]
        {
            new ChatMessage(ChatRole.User, "Respond with only the word 'Hello' and nothing else.")
        };

        // Act
        var response = await client.GetResponseAsync(messages);

        // Assert
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Messages.Count, Is.GreaterThan(0));
        var responseText = response.Messages.Last().Text ?? "";
        Assert.That(responseText, Is.Not.Empty);
        TestContext.WriteLine($"Response: {responseText}");
    }

    [Test]
    public void CreateEmbeddingGenerator_WhenOllamaRunning_ReturnsGenerator()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Act
        var generator = OllamaProvider.CreateEmbeddingGeneratorFromConfig(_configuration);

        // Assert
        Assert.That(generator, Is.Not.Null);
        Assert.That(generator, Is.InstanceOf<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Test]
    public async Task EmbeddingGenerator_GenerateEmbedding_ReturnsVector()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Arrange
        var generator = OllamaProvider.CreateEmbeddingGeneratorFromConfig(_configuration);
        var texts = new[] { "C major chord open position guitar" };

        // Act
        var embeddings = await generator.GenerateAsync(texts);

        // Assert
        Assert.That(embeddings, Is.Not.Null);
        Assert.That(embeddings.Count, Is.EqualTo(1));
        Assert.That(embeddings[0].Vector.Length, Is.GreaterThan(0));
        TestContext.WriteLine($"Embedding dimension: {embeddings[0].Vector.Length}");
    }

    [Test]
    public async Task EmbeddingGenerator_SimilarTexts_HaveHigherSimilarity()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Arrange
        var generator = OllamaProvider.CreateEmbeddingGeneratorFromConfig(_configuration);
        var texts = new[]
        {
            "C major chord on guitar", // 0
            "C major triad guitar voicing", // 1 - similar to 0
            "Weather forecast for tomorrow" // 2 - unrelated
        };

        // Act
        var embeddings = await generator.GenerateAsync(texts);

        // Assert
        var e0 = embeddings[0].Vector.ToArray();
        var e1 = embeddings[1].Vector.ToArray();
        var e2 = embeddings[2].Vector.ToArray();

        var sim01 = CosineSimilarity(e0, e1); // Guitar chords
        var sim02 = CosineSimilarity(e0, e2); // Guitar vs weather

        TestContext.WriteLine($"Similarity (guitar chord vs guitar voicing): {sim01:F4}");
        TestContext.WriteLine($"Similarity (guitar chord vs weather): {sim02:F4}");

        // Similar texts should have higher cosine similarity
        Assert.That(sim01, Is.GreaterThan(sim02),
            "Similar guitar texts should have higher similarity than unrelated text");
    }

    [Test]
    public async Task EmbeddingGenerator_BatchGeneration_HandlesMultipleTexts()
    {
        // Skip if Ollama not available
        Assume.That(_ollamaAvailable, "Ollama is not running");

        // Arrange
        var generator = OllamaProvider.CreateEmbeddingGeneratorFromConfig(_configuration);
        var texts = Enumerable.Range(1, 5)
            .Select(i => $"Chord voicing number {i}")
            .ToArray();

        // Act
        var embeddings = await generator.GenerateAsync(texts);

        // Assert
        Assert.That(embeddings.Count, Is.EqualTo(5));
        Assert.That(embeddings.All(e => e.Vector.Length > 0), Is.True);
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }

        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }
}
