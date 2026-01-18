namespace GA.Tests;

using GA.Business.Core.Chords;
using GA.Business.ML.Abstractions;
using GA.Business.ML.Text.Internal;
using GA.Data.SemanticKernel.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

/// <summary>
/// Tests for embeddings ingestion pipeline
/// </summary>
[TestFixture]
public class EmbeddingsIngestionTests
{
    private IServiceProvider _serviceProvider = null!;
    private IEmbeddingService _embeddingService = null!;
    private ILogger<EmbeddingsIngestionTests> _logger = null!;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();

        // Add logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Register embedding service (using simple implementation for testing)
        services.AddScoped<IEmbeddingService, SimpleEmbeddingService>();

        _serviceProvider = services.BuildServiceProvider();
        _embeddingService = _serviceProvider.GetRequiredService<IEmbeddingService>();
        _logger = _serviceProvider.GetRequiredService<ILogger<EmbeddingsIngestionTests>>();
    }

    [Test]
    public async Task ShouldGenerateEmbeddingForChordName()
    {
        // Arrange
        var chordName = "C Major";

        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(chordName);

        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding.Length, Is.GreaterThan(0));
        _logger.LogInformation("Generated embedding for '{ChordName}' with dimension {Dimension}",
            chordName, embedding.Length);
    }

    [Test]
    public async Task ShouldGenerateConsistentEmbeddingsForSameText()
    {
        // Arrange
        var text = "G Minor 7";

        // Act
        var embedding1 = await _embeddingService.GenerateEmbeddingAsync(text);
        var embedding2 = await _embeddingService.GenerateEmbeddingAsync(text);

        // Assert
        Assert.That(embedding1, Is.EqualTo(embedding2));
        _logger.LogInformation("Embeddings are consistent for repeated text");
    }

    [Test]
    public async Task ShouldGenerateDifferentEmbeddingsForDifferentText()
    {
        // Arrange
        var text1 = "C Major";
        var text2 = "D Minor";

        // Act
        var embedding1 = await _embeddingService.GenerateEmbeddingAsync(text1);
        var embedding2 = await _embeddingService.GenerateEmbeddingAsync(text2);

        // Assert
        Assert.That(embedding1, Is.Not.EqualTo(embedding2));
        _logger.LogInformation("Embeddings are different for different texts");
    }

    [Test]
    public async Task ShouldGenerateEmbeddingsForMultipleChords()
    {
        // Arrange
        var chords = new[]
        {
            "C Major",
            "C Minor",
            "C Dominant 7",
            "C Major 7",
            "C Minor 7"
        };

        // Act
        var embeddings = new List<float[]>();
        foreach (var chord in chords)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chord);
            embeddings.Add(embedding);
        }

        // Assert
        Assert.That(embeddings, Has.Count.EqualTo(chords.Length));
        Assert.That(embeddings, Is.All.Not.Null);

        // All embeddings should have same dimension
        var firstDimension = embeddings[0].Length;
        Assert.That(embeddings, Is.All.Property("Length").EqualTo(firstDimension));

        _logger.LogInformation("Generated {Count} embeddings with dimension {Dimension}",
            embeddings.Count, firstDimension);
    }

    [Test]
    public async Task ShouldHandleEmptyText()
    {
        // Arrange
        var emptyText = "";

        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(emptyText);

        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding.Length, Is.GreaterThan(0));
        _logger.LogInformation("Generated embedding for empty text with dimension {Dimension}",
            embedding.Length);
    }

    [Test]
    public async Task ShouldGenerateEmbeddingForChordWithIntervals()
    {
        // Arrange
        var chordDescription = "Root: C, Intervals: Major Third, Perfect Fifth";

        // Act
        var embedding = await _embeddingService.GenerateEmbeddingAsync(chordDescription);

        // Assert
        Assert.That(embedding, Is.Not.Null);
        Assert.That(embedding.Length, Is.GreaterThan(0));
        _logger.LogInformation("Generated embedding for chord description with dimension {Dimension}",
            embedding.Length);
    }

    [Test]
    public async Task ShouldMeasureEmbeddingGenerationPerformance()
    {
        // Arrange
        var chords = Enumerable.Range(0, 100)
            .Select(i => $"Chord {i}")
            .ToList();

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var embeddings = new List<float[]>();

        foreach (var chord in chords)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chord);
            embeddings.Add(embedding);
        }

        stopwatch.Stop();

        // Assert
        Assert.That(embeddings, Has.Count.EqualTo(chords.Count));
        var avgTimeMs = stopwatch.ElapsedMilliseconds / (double)chords.Count;

        _logger.LogInformation(
            "Generated {Count} embeddings in {TotalMs}ms (avg {AvgMs:F2}ms per embedding)",
            chords.Count, stopwatch.ElapsedMilliseconds, avgTimeMs);
    }

    [Test]
    public async Task ShouldCalculateCosineSimilarityBetweenEmbeddings()
    {
        // Arrange
        var text1 = "C Major";
        var text2 = "C Major";  // Same text
        var text3 = "D Minor";  // Different text

        // Act
        var embedding1 = await _embeddingService.GenerateEmbeddingAsync(text1);
        var embedding2 = await _embeddingService.GenerateEmbeddingAsync(text2);
        var embedding3 = await _embeddingService.GenerateEmbeddingAsync(text3);

        var similarity12 = CalculateCosineSimilarity(embedding1, embedding2);
        var similarity13 = CalculateCosineSimilarity(embedding1, embedding3);

        // Assert
        Assert.That(similarity12, Is.GreaterThan(similarity13));
        _logger.LogInformation(
            "Similarity between same texts: {Sim12:F4}, different texts: {Sim13:F4}",
            similarity12, similarity13);
    }

    private static double CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            throw new ArgumentException("Embeddings must have same dimension");

        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator > 0 ? dotProduct / denominator : 0;
    }

    [TearDown]
    public void Teardown()
    {
        _serviceProvider?.Dispose();
    }
}

