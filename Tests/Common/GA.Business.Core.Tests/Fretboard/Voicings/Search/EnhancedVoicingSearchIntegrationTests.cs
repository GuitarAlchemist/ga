namespace GA.Business.Core.Tests.Fretboard.Voicings.Search;

using Domain.Core.Instruments.Positions;
using Domain.Core.Instruments.Primitives;
using Domain.Services.Fretboard.Voicings.Generation;
using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Filtering;

[TestFixture]
public class EnhancedVoicingSearchIntegrationTests
{
    [SetUp]
    public async Task Setup()
    {
        _indexingService = new();
        _cpuStrategy = new();
        _searchService = new(_indexingService, _cpuStrategy);
        // Pre-index a small set of voicings
        var fretboard = Fretboard.Default;
        var voicings = VoicingGenerator.GenerateAllVoicings(fretboard, 3, 3)
            .Take(50)
            .ToList();
        var vectorCollection = new RelativeFretVectorCollection(6);
        await _indexingService.IndexVoicingsAsync(voicings, vectorCollection);
        // Initialize with a mock embedding generator
        await _searchService.InitializeEmbeddingsAsync(MockEmbeddingGenerator);
    }

    private VoicingIndexingService? _indexingService;
    private EnhancedVoicingSearchService? _searchService;
    private CpuVoicingSearchStrategy? _cpuStrategy;

    private Task<double[]> MockEmbeddingGenerator(string text)
    {
        // Simple deterministic embedding based on text hash
        var hash = text.GetHashCode();
        var random = new Random(hash);
        var embedding = new double[384];
        for (var i = 0; i < 384; i++)
        {
            embedding[i] = random.NextDouble();
        }

        // Normalize
        var mag = Math.Sqrt(embedding.Sum(x => x * x));
        for (var i = 0; i < 384; i++)
        {
            embedding[i] /= mag;
        }

        return Task.FromResult(embedding);
    }

    [Test]
    public async Task SearchAsync_SimpleQuery_ReturnsResults()
    {
        // Act
        var results = await _searchService!.SearchAsync("jazz chord", MockEmbeddingGenerator, 5);
        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.GreaterThan(0));
        Assert.That(results.Count, Is.LessThanOrEqualTo(5));
        var first = results.First();
        Assert.That(first.Score, Is.GreaterThan(0));
        Assert.That(first.Document.Id, Is.Not.Empty);
    }

    [Test]
    public async Task HybridSearch_WithFilters_RestrictsResults()
    {
        // Act
        var filters = new VoicingSearchFilters("Beginner");
        var results = await _searchService!.SearchAsync("dreamy chord", MockEmbeddingGenerator, 10, filters);
        // Assert
        Assert.That(results.All(r => r.Document.Difficulty == "Beginner"), Is.True);
    }

    [Test]
    public async Task FindSimilar_ReturnsDifferentVoicings()
    {
        // Arrange
        var allDocs = _indexingService!.Documents.ToList();
        var targetId = allDocs.First().Id;
        // Act
        var results = await _searchService!.FindSimilarAsync(targetId, 5);
        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Any(r => r.Document.Id == targetId), Is.False,
            "Should not return the query voicing itself");
    }
}
