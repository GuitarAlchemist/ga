namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using System.IO;
using Core.Fretboard.Biomechanics;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Notes.Primitives;

[TestFixture]
public class SqliteBiomechanicalCacheTests
{
    [SetUp]
    public void Setup()
    {
        // Create a temporary database file for each test
        _tempDatabasePath = Path.Combine(Path.GetTempPath(), $"biomech_cache_test_{Guid.NewGuid()}.db");
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up temporary database file
        if (_tempDatabasePath != null && File.Exists(_tempDatabasePath))
        {
            try
            {
                File.Delete(_tempDatabasePath);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    private string? _tempDatabasePath;

    [Test]
    public void Constructor_WithDatabasePath_CreatesDatabase()
    {
        // Arrange & Act
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);

        // Assert
        Assert.That(File.Exists(_tempDatabasePath), Is.True);
    }

    [Test]
    public void Constructor_WithNullPath_UsesInMemoryDatabase()
    {
        // Arrange & Act
        using var cache = new SqliteBiomechanicalCache(null);
        var stats = cache.GetStatistics();

        // Assert
        Assert.That(stats.TotalEntries, Is.EqualTo(0));
    }

    [Test]
    public void TryGet_WhenKeyNotPresent_ReturnsFalse()
    {
        // Arrange
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);
        var key = CreateCacheKey();

        // Act
        var result = cache.TryGet(key, out var analysis);

        // Assert
        Assert.That(result, Is.False);
        Assert.That(analysis, Is.Null);
    }

    [Test]
    public void Set_ThenTryGet_ReturnsTrue()
    {
        // Arrange
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);
        var key = CreateCacheKey();
        var analysis = CreateAnalysis();

        // Act
        cache.Set(key, analysis);
        var result = cache.TryGet(key, out var retrievedAnalysis);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(retrievedAnalysis, Is.Not.Null);
        Assert.That(retrievedAnalysis!.OverallScore, Is.EqualTo(analysis.OverallScore));
        Assert.That(retrievedAnalysis.IsPlayable, Is.EqualTo(analysis.IsPlayable));
    }

    [Test]
    public void Set_MultipleTimes_UpdatesEntry()
    {
        // Arrange
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);
        var key = CreateCacheKey();
        var analysis1 = CreateAnalysis(0.8);
        var analysis2 = CreateAnalysis(0.9);

        // Act
        cache.Set(key, analysis1);
        cache.Set(key, analysis2);
        cache.TryGet(key, out var retrievedAnalysis);

        // Assert
        Assert.That(retrievedAnalysis!.OverallScore, Is.EqualTo(0.9));
    }

    [Test]
    public void Set_WhenCacheFull_EvictsLRU()
    {
        // Arrange
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath, maxSize: 3);
        var key1 = CreateCacheKey(1);
        var key2 = CreateCacheKey(2);
        var key3 = CreateCacheKey(3);
        var key4 = CreateCacheKey(4);
        var analysis = CreateAnalysis();

        // Act
        cache.Set(key1, analysis);
        cache.Set(key2, analysis);
        cache.Set(key3, analysis);

        // Access key2 to make it more recently used
        cache.TryGet(key2, out _);

        // Add key4, should evict key1 (least recently used)
        cache.Set(key4, analysis);

        // Assert
        Assert.That(cache.TryGet(key1, out _), Is.False, "key1 should be evicted");
        Assert.That(cache.TryGet(key2, out _), Is.True, "key2 should still be present");
        Assert.That(cache.TryGet(key3, out _), Is.True, "key3 should still be present");
        Assert.That(cache.TryGet(key4, out _), Is.True, "key4 should be present");
    }

    [Test]
    public void Clear_RemovesAllEntries()
    {
        // Arrange
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);
        var key1 = CreateCacheKey(1);
        var key2 = CreateCacheKey(2);
        var analysis = CreateAnalysis();
        cache.Set(key1, analysis);
        cache.Set(key2, analysis);

        // Act
        cache.Clear();

        // Assert
        Assert.That(cache.TryGet(key1, out _), Is.False);
        Assert.That(cache.TryGet(key2, out _), Is.False);
        var stats = cache.GetStatistics();
        Assert.That(stats.TotalEntries, Is.EqualTo(0));
    }

    [Test]
    public void Invalidate_RemovesAllEntries()
    {
        // Arrange
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);
        var key = CreateCacheKey();
        var analysis = CreateAnalysis();
        cache.Set(key, analysis);

        // Act
        cache.Invalidate(InvalidationReason.UserPreferenceChanged);

        // Assert
        Assert.That(cache.TryGet(key, out _), Is.False);
    }

    [Test]
    public void GetStatistics_ReturnsCorrectCounts()
    {
        // Arrange
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);
        var key1 = CreateCacheKey(1);
        var key2 = CreateCacheKey(2);
        var analysis = CreateAnalysis();

        // Act
        cache.Set(key1, analysis);
        cache.Set(key2, analysis);
        cache.TryGet(key1, out _); // Hit
        cache.TryGet(CreateCacheKey(20), out _); // Miss (different fret)

        var stats = cache.GetStatistics();

        // Assert
        Assert.That(stats.TotalEntries, Is.EqualTo(2));
        Assert.That(stats.Hits, Is.EqualTo(1));
        Assert.That(stats.Misses, Is.EqualTo(1));
        Assert.That(stats.HitRate, Is.EqualTo(0.5).Within(0.01));
    }

    [Test]
    public void Persistence_DataSurvivesDispose()
    {
        // Arrange
        var key = CreateCacheKey();
        var analysis = CreateAnalysis();

        // Act - Create cache, add entry, dispose
        using (var cache = new SqliteBiomechanicalCache(_tempDatabasePath))
        {
            cache.Set(key, analysis);
        }

        // Act - Create new cache instance with same database
        using (var cache = new SqliteBiomechanicalCache(_tempDatabasePath))
        {
            var result = cache.TryGet(key, out var retrievedAnalysis);

            // Assert
            Assert.That(result, Is.True);
            Assert.That(retrievedAnalysis, Is.Not.Null);
            Assert.That(retrievedAnalysis!.OverallScore, Is.EqualTo(analysis.OverallScore));
        }
    }

    [Test]
    public void Persistence_StatisticsResetOnNewInstance()
    {
        // Arrange
        var key = CreateCacheKey();
        var analysis = CreateAnalysis();

        // Act - Create cache, add entry, access it, dispose
        using (var cache = new SqliteBiomechanicalCache(_tempDatabasePath))
        {
            cache.Set(key, analysis);
            cache.TryGet(key, out _);
        }

        // Act - Create new cache instance
        using (var cache = new SqliteBiomechanicalCache(_tempDatabasePath))
        {
            var stats = cache.GetStatistics();

            // Assert - Statistics should reset but data should persist
            Assert.That(stats.Hits, Is.EqualTo(0));
            Assert.That(stats.Misses, Is.EqualTo(0));
            Assert.That(stats.TotalEntries, Is.EqualTo(1)); // Data persists
        }
    }

    [Test]
    public void CacheKey_WithDifferentPositions_HasDifferentHash()
    {
        // Arrange
        var key1 = CreateCacheKey(1);
        var key2 = CreateCacheKey(2);

        // Act & Assert
        Assert.That(key1.GetHashCode(), Is.Not.EqualTo(key2.GetHashCode()));
    }

    [Test]
    public void CacheKey_WithDifferentHandSize_HasDifferentHash()
    {
        // Arrange
        var key1 = CreateCacheKey(handSize: HandSize.Small);
        var key2 = CreateCacheKey(handSize: HandSize.Large);

        // Act & Assert
        Assert.That(key1.GetHashCode(), Is.Not.EqualTo(key2.GetHashCode()));
    }

    [Test]
    public void CacheKey_WithDifferentCapo_HasDifferentHash()
    {
        // Arrange
        var key1 = CreateCacheKey(capo: null);
        var key2 = CreateCacheKey(capo: Capo.At(2));

        // Act & Assert
        Assert.That(key1.GetHashCode(), Is.Not.EqualTo(key2.GetHashCode()));
    }

    [Test]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var cache = new SqliteBiomechanicalCache(_tempDatabasePath);

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            cache.Dispose();
            cache.Dispose();
        });
    }

    [Test]
    public void AfterDispose_OperationsThrowObjectDisposedException()
    {
        // Arrange
        var cache = new SqliteBiomechanicalCache(_tempDatabasePath);
        cache.Dispose();
        var key = CreateCacheKey();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => cache.TryGet(key, out _));
        Assert.Throws<ObjectDisposedException>(() => cache.Set(key, CreateAnalysis()));
        Assert.Throws<ObjectDisposedException>(() => cache.Clear());
        Assert.Throws<ObjectDisposedException>(() => cache.Invalidate(InvalidationReason.Manual));
        Assert.Throws<ObjectDisposedException>(() => cache.GetStatistics());
    }

    // Helper methods
    private static CacheKey CreateCacheKey(int fret = 3, HandSize handSize = HandSize.Medium, Capo? capo = null)
    {
        var positions = CreatePositions([(1, fret), (2, fret + 1), (3, fret + 2)]);
        return new CacheKey
        {
            Positions = positions.Cast<Position.Played>().ToList(),
            HandSize = handSize,
            Capo = capo
        };
    }

    private static BiomechanicalPlayabilityAnalysis CreateAnalysis(double overallScore = 0.85)
    {
        return new BiomechanicalPlayabilityAnalysis(
            Reachability: 0.9,
            Comfort: 0.8,
            Naturalness: 0.85,
            Efficiency: 0.9,
            Stability: 0.85,
            OverallScore: overallScore,
            Difficulty: BiomechanicalDifficulty.Moderate,
            IsPlayable: true,
            Reason: "Test analysis",
            SolveTime: TimeSpan.FromMilliseconds(100),
            BestPose: null,
            FitnessDetails: null);
    }

    private static ImmutableList<Position> CreatePositions(IEnumerable<(int str, int fret)> stringFretPairs)
    {
        var positions = new List<Position>();
        foreach (var (str, fret) in stringFretPairs)
        {
            var stringObj = Str.FromValue(str);
            var fretObj = Fret.FromValue(fret);
            var location = new PositionLocation(stringObj, fretObj);
            positions.Add(new Position.Played(location, MidiNote.FromValue(60 + fret)));
        }

        return positions.ToImmutableList();
    }
}
