namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using Core.Fretboard.Biomechanics;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Notes.Primitives;

[TestFixture]
public class BiomechanicalCacheTests
{
    [SetUp]
    public void Setup()
    {
        _cache = new MemoryBiomechanicalCache(maxSize: 10);
        _analyzer = new BiomechanicalAnalyzer(cache: _cache);
    }

    private MemoryBiomechanicalCache _cache = null!;
    private BiomechanicalAnalyzer _analyzer = null!;

    [Test]
    public void Cache_FirstAccess_ShouldBeCacheMiss()
    {
        // Arrange
        var positions = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);

        // Act
        var analysis = _analyzer.AnalyzeChordPlayability(positions);
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.Misses, Is.EqualTo(1));
        Assert.That(stats.Hits, Is.EqualTo(0));
        Assert.That(stats.TotalEntries, Is.EqualTo(1));
    }

    [Test]
    public void Cache_SecondAccess_ShouldBeCacheHit()
    {
        // Arrange
        var positions = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);

        // Act
        var analysis1 = _analyzer.AnalyzeChordPlayability(positions);
        var analysis2 = _analyzer.AnalyzeChordPlayability(positions);
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.Hits, Is.EqualTo(1));
        Assert.That(stats.Misses, Is.EqualTo(1));
        Assert.That(stats.HitRate, Is.EqualTo(0.5));
        Assert.That(analysis1, Is.SameAs(analysis2)); // Same reference
    }

    [Test]
    public void Cache_DifferentPositions_ShouldBeCacheMiss()
    {
        // Arrange
        var positions1 = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);
        var positions2 = CreatePositions([(5, 0), (4, 2), (3, 2), (2, 1), (1, 0)]);

        // Act
        var analysis1 = _analyzer.AnalyzeChordPlayability(positions1);
        var analysis2 = _analyzer.AnalyzeChordPlayability(positions2);
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.Misses, Is.EqualTo(2));
        Assert.That(stats.Hits, Is.EqualTo(0));
        Assert.That(stats.TotalEntries, Is.EqualTo(2));
    }

    [Test]
    public void Cache_DifferentHandSize_ShouldBeCacheMiss()
    {
        // Arrange
        var positions = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);
        var smallHandAnalyzer = BiomechanicalAnalyzer.CreateForHandSize(HandSize.Small, cache: _cache);
        var largeHandAnalyzer = BiomechanicalAnalyzer.CreateForHandSize(HandSize.Large, cache: _cache);

        // Act
        var analysis1 = smallHandAnalyzer.AnalyzeChordPlayability(positions);
        var analysis2 = largeHandAnalyzer.AnalyzeChordPlayability(positions);
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.Misses, Is.EqualTo(2));
        Assert.That(stats.Hits, Is.EqualTo(0));
        Assert.That(stats.TotalEntries, Is.EqualTo(2));
    }

    [Test]
    public void Cache_DifferentCapo_ShouldBeCacheMiss()
    {
        // Arrange - Use positions above capo (fret 2+)
        var positions = CreatePositions([(5, 5), (4, 4), (3, 3), (2, 3), (1, 2)]);
        var noCapoAnalyzer = new BiomechanicalAnalyzer(cache: _cache);
        var capoAnalyzer = BiomechanicalAnalyzer.CreateWithCapo(2, cache: _cache);

        // Act
        var analysis1 = noCapoAnalyzer.AnalyzeChordPlayability(positions);
        var analysis2 = capoAnalyzer.AnalyzeChordPlayability(positions);
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.Misses, Is.EqualTo(2));
        Assert.That(stats.Hits, Is.EqualTo(0));
        Assert.That(stats.TotalEntries, Is.EqualTo(2));
    }

    [Test]
    public void Cache_Clear_ShouldRemoveAllEntries()
    {
        // Arrange
        var positions = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);
        _analyzer.AnalyzeChordPlayability(positions);

        // Act
        _cache.Clear();
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.TotalEntries, Is.EqualTo(0));
    }

    [Test]
    public void Cache_Invalidate_ShouldRemoveAllEntries()
    {
        // Arrange
        var positions = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);
        _analyzer.AnalyzeChordPlayability(positions);

        // Act
        _cache.Invalidate(InvalidationReason.UserPreferenceChanged);
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.TotalEntries, Is.EqualTo(0));
    }

    [Test]
    public void Cache_AfterInvalidation_ShouldBeCacheMiss()
    {
        // Arrange
        var positions = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);
        _analyzer.AnalyzeChordPlayability(positions);
        _cache.Invalidate(InvalidationReason.UserPreferenceChanged);

        // Act
        var analysis = _analyzer.AnalyzeChordPlayability(positions);
        var stats = _cache.GetStatistics();

        // Assert - After invalidation, cache is cleared but stats continue counting
        Assert.That(stats.Misses, Is.EqualTo(2)); // First miss + miss after invalidation
        Assert.That(stats.Hits, Is.EqualTo(0));
        Assert.That(stats.TotalEntries, Is.EqualTo(1)); // Only the new entry
    }

    [Test]
    public void Cache_LRUEviction_ShouldEvictOldestEntry()
    {
        // Arrange - Cache size is 10
        var positions = new List<ImmutableList<Position>>();
        for (var i = 0; i < 11; i++)
        {
            positions.Add(CreatePositions([(5, i), (4, i + 1), (3, i + 2)]));
        }

        // Act - Add 11 entries (should evict the first one)
        foreach (var pos in positions)
        {
            _analyzer.AnalyzeChordPlayability(pos);
        }

        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.TotalEntries, Is.EqualTo(10)); // Max size
        Assert.That(stats.Misses, Is.EqualTo(11)); // All were misses
    }

    [Test]
    public void Cache_LRUEviction_ShouldKeepRecentlyAccessed()
    {
        // Arrange - Cache size is 10
        var firstPosition = CreatePositions([(5, 0), (4, 1), (3, 2)]);
        _analyzer.AnalyzeChordPlayability(firstPosition);

        // Add 9 more entries
        for (var i = 1; i < 10; i++)
        {
            var pos = CreatePositions([(5, i), (4, i + 1), (3, i + 2)]);
            _analyzer.AnalyzeChordPlayability(pos);
        }

        // Access the first position again (should update its last accessed time)
        _analyzer.AnalyzeChordPlayability(firstPosition);

        // Act - Add one more entry (should evict the second entry, not the first)
        var newPosition = CreatePositions([(5, 10), (4, 11), (3, 12)]);
        _analyzer.AnalyzeChordPlayability(newPosition);

        // Access the first position again - should be a cache hit
        _analyzer.AnalyzeChordPlayability(firstPosition);
        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.TotalEntries, Is.EqualTo(10));
        Assert.That(stats.Hits, Is.GreaterThan(0)); // First position should still be cached
    }

    [Test]
    public void Cache_PerformanceImprovement_ShouldBeFaster()
    {
        // Arrange
        var positions = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);

        // Act - First call (cache miss)
        var sw1 = Stopwatch.StartNew();
        var analysis1 = _analyzer.AnalyzeChordPlayability(positions);
        sw1.Stop();

        // Second call (cache hit)
        var sw2 = Stopwatch.StartNew();
        var analysis2 = _analyzer.AnalyzeChordPlayability(positions);
        sw2.Stop();

        // Assert
        Assert.That(sw2.ElapsedMilliseconds, Is.LessThan(sw1.ElapsedMilliseconds));
        Assert.That(sw2.ElapsedMilliseconds, Is.LessThan(10)); // Cache hit should be < 10ms
    }

    [Test]
    public void Cache_MultipleChords_ShouldCacheAll()
    {
        // Arrange - Common chord progression: C, Am, F, G
        var cMajor = CreatePositions([(5, 3), (4, 2), (3, 0), (2, 1), (1, 0)]);
        var aMinor = CreatePositions([(5, 0), (4, 2), (3, 2), (2, 1), (1, 0)]);
        var fMajor = CreatePositions([(6, 1), (5, 3), (4, 3), (3, 2), (2, 1), (1, 1)]);
        var gMajor = CreatePositions([(6, 3), (5, 2), (4, 0), (3, 0), (2, 0), (1, 3)]);

        // Act - Analyze each chord twice
        _analyzer.AnalyzeChordPlayability(cMajor);
        _analyzer.AnalyzeChordPlayability(aMinor);
        _analyzer.AnalyzeChordPlayability(fMajor);
        _analyzer.AnalyzeChordPlayability(gMajor);

        _analyzer.AnalyzeChordPlayability(cMajor);
        _analyzer.AnalyzeChordPlayability(aMinor);
        _analyzer.AnalyzeChordPlayability(fMajor);
        _analyzer.AnalyzeChordPlayability(gMajor);

        var stats = _cache.GetStatistics();

        // Assert
        Assert.That(stats.TotalEntries, Is.EqualTo(4));
        Assert.That(stats.Hits, Is.EqualTo(4));
        Assert.That(stats.Misses, Is.EqualTo(4));
        Assert.That(stats.HitRate, Is.EqualTo(0.5));
    }

    [Test]
    public void CacheKey_SamePositions_ShouldBeEqual()
    {
        // Arrange
        var positions1 = CreatePositions([(5, 3), (4, 2), (3, 0)]).Cast<Position.Played>().ToList();
        var positions2 = CreatePositions([(5, 3), (4, 2), (3, 0)]).Cast<Position.Played>().ToList();

        var key1 = new CacheKey { Positions = positions1, HandSize = HandSize.Medium, Capo = null };
        var key2 = new CacheKey { Positions = positions2, HandSize = HandSize.Medium, Capo = null };

        // Assert
        Assert.That(key1, Is.EqualTo(key2));
        Assert.That(key1.GetHashCode(), Is.EqualTo(key2.GetHashCode()));
    }

    [Test]
    public void CacheKey_DifferentPositions_ShouldNotBeEqual()
    {
        // Arrange
        var positions1 = CreatePositions([(5, 3), (4, 2), (3, 0)]).Cast<Position.Played>().ToList();
        var positions2 = CreatePositions([(5, 3), (4, 2), (3, 1)]).Cast<Position.Played>().ToList();

        var key1 = new CacheKey { Positions = positions1, HandSize = HandSize.Medium, Capo = null };
        var key2 = new CacheKey { Positions = positions2, HandSize = HandSize.Medium, Capo = null };

        // Assert
        Assert.That(key1, Is.Not.EqualTo(key2));
    }

    private static ImmutableList<Position> CreatePositions(IEnumerable<(int str, int fret)> stringFretPairs)
    {
        var positions = new List<Position>();

        foreach (var (str, fret) in stringFretPairs)
        {
            var stringObj = Str.FromValue(str);
            var fretObj = Fret.FromValue(fret);
            var location = new PositionLocation(stringObj, fretObj);
            // Use dummy MIDI note - not used in biomechanical analysis
            positions.Add(new Position.Played(location, MidiNote.FromValue(60 + fret)));
        }

        return positions.ToImmutableList();
    }
}
