namespace GA.Business.Core.Tests.Fretboard.Voicings.Search;

using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Generation;
using GA.Business.Core.Fretboard.Voicings.Search;
using NUnit.Framework;

[TestFixture]
public class EnhancedVoicingSearchIntegrationTests
{
    private VoicingIndexingService? _indexingService;
    private EnhancedVoicingSearchService? _searchService;
    private CpuVoicingSearchStrategy? _cpuStrategy;

    [SetUp]
    public async Task Setup()
    {
        _indexingService = new VoicingIndexingService();
        _cpuStrategy = new CpuVoicingSearchStrategy();
        _searchService = new EnhancedVoicingSearchService(_indexingService, _cpuStrategy);

        // Pre-index a small set of voicings
        var fretboard = Fretboard.Default;
        var voicings = VoicingGenerator.GenerateAllVoicings(fretboard, windowSize: 3, minPlayedNotes: 3)
            .Take(50)
            .ToList();
        
        var vectorCollection = new RelativeFretVectorCollection(6, 5);
        await _indexingService.IndexVoicingsAsync(voicings, vectorCollection);

        // Initialize with a mock embedding generator
        await _searchService.InitializeEmbeddingsAsync(MockEmbeddingGenerator);
    }

    private Task<double[]> MockEmbeddingGenerator(string text)
    {
        // Simple deterministic embedding based on text hash
        var hash = text.GetHashCode();
        var random = new Random(hash);
        var embedding = new double[384];
        for (int i = 0; i < 384; i++) embedding[i] = random.NextDouble();
        
        // Normalize
        var mag = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < 384; i++) embedding[i] /= mag;
        
        return Task.FromResult(embedding);
    }

    [Test]
    public async Task SearchAsync_SimpleQuery_ReturnsResults()
    {
        // Act
        var results = await _searchService!.SearchAsync("jazz chord", MockEmbeddingGenerator, topK: 5);

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
        var filters = new VoicingSearchFilters(Difficulty: "Beginner");
        var results = await _searchService!.SearchAsync("dreamy chord", MockEmbeddingGenerator, topK: 10, filters: filters);

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
        var results = await _searchService!.FindSimilarAsync(targetId, topK: 5);

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Any(r => r.Document.Id == targetId), Is.False, "Should not return the query voicing itself");
    }
}
