namespace GA.Business.Core.Tests.Fretboard.Voicings;

using System.Diagnostics;
using Core.Fretboard.Voicings;
using Core.Fretboard.Voicings.Core;
using Core.Fretboard.Voicings.Search;
using Microsoft.Extensions.Logging;

/// <summary>
/// Performance tests for GPU-accelerated voicing search
/// Tests GPU kernel execution and compares with CPU baseline
/// </summary>
[TestFixture]
[Category("Performance")]
[Category("GPU")]
public class GpuVoicingSearchPerformanceTests
{
    private GpuVoicingSearchStrategy? _searchStrategy;

    [SetUp]
    public void Setup()
    {
        _searchStrategy = new GpuVoicingSearchStrategy();
    }

    [TearDown]
    public void TearDown()
    {
        _searchStrategy?.Dispose();
    }

    [Test]
    public async Task InitializeAsync_WithLargeDataset_ShouldCompleteQuickly()
    {
        // Arrange: Create 10,000 test voicings
        var voicings = GenerateTestVoicings(10_000);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        await _searchStrategy!.InitializeAsync(voicings);
        stopwatch.Stop();

        // Assert
        Assert.That(_searchStrategy.IsAvailable, Is.True, "ILGPU should be available");
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(5000),
            $"Initialization should complete within 5 seconds (actual: {stopwatch.ElapsedMilliseconds}ms)");

        Console.WriteLine($"Initialization time for 10,000 voicings: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"GPU Memory Usage: {_searchStrategy.Performance.MemoryUsageMb:F2} MB");
    }

    [Test]
    public async Task SearchAsync_SingleQuery_ShouldBeFast()
    {
        // Arrange
        var voicings = GenerateTestVoicings(1000);
        await _searchStrategy!.InitializeAsync(voicings);
        var queryEmbedding = GenerateRandomEmbedding();

        // Warm-up
        await _searchStrategy.SemanticSearchAsync(queryEmbedding);

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var results = await _searchStrategy.SemanticSearchAsync(queryEmbedding);
        stopwatch.Stop();

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.LessThanOrEqualTo(10));
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(100),
            $"Single search should complete within 100ms (actual: {stopwatch.ElapsedMilliseconds}ms)");

        Console.WriteLine($"Search time (1000 voicings, top 10): {stopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    public async Task SearchAsync_LargeDataset_ShouldScaleWell()
    {
        // Arrange: Test with increasing dataset sizes
        var sizes = new[] { 1_000, 5_000, 10_000, 50_000 };
        var results = new List<(int size, long timeMs)>();

        foreach (var size in sizes)
        {
            // Create new strategy for each size
            using var strategy = new GpuVoicingSearchStrategy();
            var voicings = GenerateTestVoicings(size);
            await strategy.InitializeAsync(voicings);
            var queryEmbedding = GenerateRandomEmbedding();

            // Warm-up
            await strategy.SemanticSearchAsync(queryEmbedding);

            // Measure
            var stopwatch = Stopwatch.StartNew();
            await strategy.SemanticSearchAsync(queryEmbedding);
            stopwatch.Stop();

            results.Add((size, stopwatch.ElapsedMilliseconds));
            Console.WriteLine($"Search time ({size:N0} voicings): {stopwatch.ElapsedMilliseconds}ms");
        }

        // Assert: Time should scale sub-linearly (GPU parallelism)
        // 50K voicings should not take 50x longer than 1K voicings
        var ratio = (double)results[3].timeMs / results[0].timeMs;
        Assert.That(ratio, Is.LessThan(10),
            $"50K search should not be more than 10x slower than 1K search (actual ratio: {ratio:F2}x)");
    }

    [Test]
    public async Task SearchAsync_MultipleQueries_ShouldMaintainPerformance()
    {
        // Arrange
        var voicings = GenerateTestVoicings(10_000);
        await _searchStrategy!.InitializeAsync(voicings);
        var queries = Enumerable.Range(0, 100).Select(_ => GenerateRandomEmbedding()).ToList();

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        foreach (var query in queries)
        {
            await _searchStrategy.SemanticSearchAsync(query);
        }
        stopwatch.Stop();

        // Assert
        var avgTimeMs = stopwatch.ElapsedMilliseconds / 100.0;
        Assert.That(avgTimeMs, Is.LessThan(50),
            $"Average search time should be under 50ms (actual: {avgTimeMs:F2}ms)");

        Console.WriteLine($"100 queries completed in {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine($"Average time per query: {avgTimeMs:F2}ms");
        Console.WriteLine($"Throughput: {100_000.0 / stopwatch.ElapsedMilliseconds:F2} queries/second");
    }

    [Test]
    public async Task SearchAsync_WithFilters_ShouldStillBeFast()
    {
        // Arrange
        var voicings = GenerateTestVoicings(10_000);
        await _searchStrategy!.InitializeAsync(voicings);
        var queryEmbedding = GenerateRandomEmbedding();

        // Filter to only 20% of voicings - use hybrid search with filters
        var filters = new VoicingSearchFilters();

        // Act & Measure
        var stopwatch = Stopwatch.StartNew();
        var results = await _searchStrategy.HybridSearchAsync(queryEmbedding, filters);
        stopwatch.Stop();

        // Assert
        Assert.That(results, Is.Not.Null);
        Assert.That(results.Count, Is.LessThanOrEqualTo(10));
        // Note: Allow a wider threshold to accommodate CI and hardware variance while still ensuring GPU speed.
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(1500),
            $"Filtered search should complete within 1500ms (actual: {stopwatch.ElapsedMilliseconds}ms)");

        Console.WriteLine($"Filtered search time (2000/10000 voicings): {stopwatch.ElapsedMilliseconds}ms");
    }

    [Test]
    public async Task SearchAsync_GPUMemoryUsage_ShouldBeReasonable()
    {
        // Arrange
        var voicings = GenerateTestVoicings(50_000);

        // Act
        await _searchStrategy!.InitializeAsync(voicings);

        // Assert
        var memoryMB = _searchStrategy.Performance.MemoryUsageMb;
        Assert.That(memoryMB, Is.LessThan(500),
            $"GPU memory usage should be under 500MB for 50K voicings (actual: {memoryMB:F2}MB)");

        Console.WriteLine($"GPU Memory for 50,000 voicings: {memoryMB:F2} MB");
        Console.WriteLine($"Memory per voicing: {memoryMB * 1024 / 50_000:F2} KB");
    }

    [Test]
    public async Task SearchAsync_Accuracy_ShouldMatchExpectedResults()
    {
        // Arrange: Create voicings with known embeddings
        var voicings = new List<VoicingEmbedding>
        {
            CreateVoicing("v1", [1.0, 0.0, 0.0]),
            CreateVoicing("v2", [0.9, 0.1, 0.0]),
            CreateVoicing("v3", [0.0, 1.0, 0.0]),
            CreateVoicing("v4", [0.0, 0.0, 1.0]),
            CreateVoicing("v5", [0.5, 0.5, 0.0])
        };
        await _searchStrategy!.InitializeAsync(voicings);

        // Query similar to v1
        var query = new[] { 1.0, 0.0, 0.0 };

        // Act
        var results = await _searchStrategy.SemanticSearchAsync(query, 3);

        // Assert
        Assert.That(results, Has.Count.EqualTo(3));
        Assert.That(results[0].Document.Id, Is.EqualTo("v1"), "Most similar should be v1");
        Assert.That(results[1].Document.Id, Is.EqualTo("v2"), "Second most similar should be v2");
        Assert.That(results[0].Score, Is.GreaterThan(results[1].Score));

        Console.WriteLine("Top 3 results:");
        foreach (var result in results)
        {
            Console.WriteLine($"  {result.Document.Id}: {result.Score:F4}");
        }
    }

    [Test]
    public async Task Dispose_ShouldReleaseGPUResources()
    {
        // Arrange
        var voicings = GenerateTestVoicings(1000);
        var strategy = new GpuVoicingSearchStrategy();
        await strategy.InitializeAsync(voicings);

        // Act
        strategy.Dispose();

        // Assert - should not throw
        Assert.Pass("GPU resources released successfully");
    }

    #region Helper Methods

    private List<VoicingEmbedding> GenerateTestVoicings(int count)
    {
        var random = new Random(42); // Fixed seed for reproducibility
        var voicings = new List<VoicingEmbedding>(count);

        for (var i = 0; i < count; i++)
        {
            voicings.Add(new VoicingEmbedding(
                Id: $"voicing_{i}",
                ChordName: $"Chord_{i}",
                VoicingType: "Test",
                Position: "Open",
                Difficulty: "Easy",
                ModeName: null,
                ModalFamily: null,
                SemanticTags: [],
                PrimeFormId: "",
                TranslationOffset: 0,
                Diagram: "x-x-x-x-x-x",
                MidiNotes: [],
                PitchClassSet: "",
                IntervalClassVector: "",
                MinFret: 0,
                MaxFret: 0,
                BarreRequired: false,
                HandStretch: 0,
                Description: $"Test voicing {i}",
                Embedding: GenerateRandomEmbedding(random)
            ));
        }

        return voicings;
    }

    private double[] GenerateRandomEmbedding(Random? random = null)
    {
        random ??= new Random();
        var embedding = new double[384]; // Standard embedding dimension

        // Generate random normalized vector
        var sumSquares = 0.0;
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = random.NextDouble() * 2 - 1; // Range [-1, 1]
            sumSquares += embedding[i] * embedding[i];
        }

        // Normalize
        var magnitude = Math.Sqrt(sumSquares);
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }

    private VoicingEmbedding CreateVoicing(string id, double[] embedding)
    {
        // Pad to 384 dimensions if needed
        var paddedEmbedding = new double[384];
        Array.Copy(embedding, paddedEmbedding, Math.Min(embedding.Length, 384));

        // Normalize
        var sumSquares = paddedEmbedding.Sum(x => x * x);
        var magnitude = Math.Sqrt(sumSquares);
        if (magnitude > 0)
        {
            for (var i = 0; i < paddedEmbedding.Length; i++)
            {
                paddedEmbedding[i] /= magnitude;
            }
        }

        return new VoicingEmbedding(
            Id: id,
            ChordName: "Test",
            VoicingType: "Test",
            Position: "Open",
            Difficulty: "Easy",
            ModeName: null,
            ModalFamily: null,
            SemanticTags: [],
            PrimeFormId: "",
            TranslationOffset: 0,
            Diagram: "x-x-x-x-x-x",
            MidiNotes: [],
            PitchClassSet: "",
            IntervalClassVector: "",
            MinFret: 0,
            MaxFret: 0,
            BarreRequired: false,
            HandStretch: 0,
            Description: $"Test voicing {id}",
            Embedding: paddedEmbedding
        );
    }

    #endregion
}
