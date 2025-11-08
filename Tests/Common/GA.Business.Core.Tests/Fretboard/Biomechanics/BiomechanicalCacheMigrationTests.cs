namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using System.IO;
using System.Threading;
using Core.Fretboard.Biomechanics;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Notes.Primitives;

[TestFixture]
public class BiomechanicalCacheMigrationTests
{
    [SetUp]
    public void Setup()
    {
        _tempDatabasePath = Path.Combine(Path.GetTempPath(), $"test_migration_{Guid.NewGuid()}.db");
    }

    [TearDown]
    public void TearDown()
    {
        // Give time for any connections to close
        Thread.Sleep(100);

        // Force garbage collection to ensure connections are closed
        GC.Collect();
        GC.WaitForPendingFinalizers();

        if (File.Exists(_tempDatabasePath))
        {
            try
            {
                File.Delete(_tempDatabasePath);
            }
            catch (IOException)
            {
                // File might still be locked, ignore
            }
        }
    }

    private string _tempDatabasePath = null!;

    [Test]
    public void Migrate_EmptySource_ReturnsZero()
    {
        var source = new MemoryBiomechanicalCache();
        using var destination = new SqliteBiomechanicalCache(_tempDatabasePath);

        var count = BiomechanicalCacheMigration.Migrate(source, destination);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void Migrate_MemoryToSqlite_MigratesAllEntries()
    {
        var source = new MemoryBiomechanicalCache();
        using var destination = new SqliteBiomechanicalCache(_tempDatabasePath);

        // Add entries to source
        var key1 = CreateCacheKey();
        var key2 = CreateCacheKey(5);
        var key3 = CreateCacheKey(7);
        var analysis1 = CreateAnalysis(0.8);
        var analysis2 = CreateAnalysis();
        var analysis3 = CreateAnalysis(0.9);

        source.Set(key1, analysis1);
        source.Set(key2, analysis2);
        source.Set(key3, analysis3);

        // Migrate
        var count = BiomechanicalCacheMigration.Migrate(source, destination);

        Assert.That(count, Is.EqualTo(3));
        Assert.That(destination.TryGet(key1, out var retrieved1), Is.True);
        Assert.That(retrieved1!.OverallScore, Is.EqualTo(0.8));
        Assert.That(destination.TryGet(key2, out var retrieved2), Is.True);
        Assert.That(retrieved2!.OverallScore, Is.EqualTo(0.85));
        Assert.That(destination.TryGet(key3, out var retrieved3), Is.True);
        Assert.That(retrieved3!.OverallScore, Is.EqualTo(0.9));
    }

    [Test]
    public void Migrate_SqliteToMemory_MigratesAllEntries()
    {
        using var source = new SqliteBiomechanicalCache(_tempDatabasePath);
        var destination = new MemoryBiomechanicalCache();

        // Add entries to source
        var key1 = CreateCacheKey();
        var key2 = CreateCacheKey(5);
        var analysis1 = CreateAnalysis(0.75);
        var analysis2 = CreateAnalysis(0.95);

        source.Set(key1, analysis1);
        source.Set(key2, analysis2);

        // Migrate
        var count = BiomechanicalCacheMigration.Migrate(source, destination);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(destination.TryGet(key1, out var retrieved1), Is.True);
        Assert.That(retrieved1!.OverallScore, Is.EqualTo(0.75));
        Assert.That(destination.TryGet(key2, out var retrieved2), Is.True);
        Assert.That(retrieved2!.OverallScore, Is.EqualTo(0.95));
    }

    [Test]
    public void MigrateToSqlite_CreatesNewCacheAndMigrates()
    {
        var memoryCache = new MemoryBiomechanicalCache();

        // Add entries
        var key = CreateCacheKey(10);
        var analysis = CreateAnalysis(0.88);
        memoryCache.Set(key, analysis);

        // Migrate to SQLite
        using var sqliteCache = BiomechanicalCacheMigration.MigrateToSqlite(
            memoryCache,
            _tempDatabasePath,
            maxSize: 100);

        Assert.That(sqliteCache.TryGet(key, out var retrieved), Is.True);
        Assert.That(retrieved!.OverallScore, Is.EqualTo(0.88));
    }

    [Test]
    public void MigrateToMemory_CreatesNewCacheAndMigrates()
    {
        using var sqliteCache = new SqliteBiomechanicalCache(_tempDatabasePath);

        // Add entries
        var key = CreateCacheKey(12);
        var analysis = CreateAnalysis(0.92);
        sqliteCache.Set(key, analysis);

        // Migrate to memory
        var memoryCache = BiomechanicalCacheMigration.MigrateToMemory(
            sqliteCache,
            maxSize: 100);

        Assert.That(memoryCache.TryGet(key, out var retrieved), Is.True);
        Assert.That(retrieved!.OverallScore, Is.EqualTo(0.92));
    }

    [Test]
    public void Migrate_PreservesHandSizeInKey()
    {
        var source = new MemoryBiomechanicalCache();
        using var destination = new SqliteBiomechanicalCache(_tempDatabasePath);

        var keySmall = CreateCacheKey(5, HandSize.Small);
        var keyLarge = CreateCacheKey(5, HandSize.Large);
        var analysis1 = CreateAnalysis(0.7);
        var analysis2 = CreateAnalysis(0.9);

        source.Set(keySmall, analysis1);
        source.Set(keyLarge, analysis2);

        BiomechanicalCacheMigration.Migrate(source, destination);

        Assert.That(destination.TryGet(keySmall, out var retrieved1), Is.True);
        Assert.That(retrieved1!.OverallScore, Is.EqualTo(0.7));
        Assert.That(destination.TryGet(keyLarge, out var retrieved2), Is.True);
        Assert.That(retrieved2!.OverallScore, Is.EqualTo(0.9));
    }

    [Test]
    public void Migrate_PreservesCapoInKey()
    {
        var source = new MemoryBiomechanicalCache();
        using var destination = new SqliteBiomechanicalCache(_tempDatabasePath);

        var keyNoCapo = CreateCacheKey(fret: 5, capo: null);
        var keyWithCapo = CreateCacheKey(5, capo: Capo.At(2));
        var analysis1 = CreateAnalysis(0.8);
        var analysis2 = CreateAnalysis();

        source.Set(keyNoCapo, analysis1);
        source.Set(keyWithCapo, analysis2);

        BiomechanicalCacheMigration.Migrate(source, destination);

        Assert.That(destination.TryGet(keyNoCapo, out var retrieved1), Is.True);
        Assert.That(retrieved1!.OverallScore, Is.EqualTo(0.8));
        Assert.That(destination.TryGet(keyWithCapo, out var retrieved2), Is.True);
        Assert.That(retrieved2!.OverallScore, Is.EqualTo(0.85));
    }

    [Test]
    public void GetAllEntries_MemoryCache_ReturnsAllEntries()
    {
        var cache = new MemoryBiomechanicalCache();

        var key1 = CreateCacheKey();
        var key2 = CreateCacheKey(5);
        var analysis1 = CreateAnalysis(0.8);
        var analysis2 = CreateAnalysis(0.9);

        cache.Set(key1, analysis1);
        cache.Set(key2, analysis2);

        var entries = cache.GetAllEntries().ToList();

        Assert.That(entries.Count, Is.EqualTo(2));
        Assert.That(entries.Any(e => e.Analysis.OverallScore == 0.8), Is.True);
        Assert.That(entries.Any(e => e.Analysis.OverallScore == 0.9), Is.True);
    }

    [Test]
    public void GetAllEntries_SqliteCache_ReturnsAllEntries()
    {
        using var cache = new SqliteBiomechanicalCache(_tempDatabasePath);

        var key1 = CreateCacheKey();
        var key2 = CreateCacheKey(5);
        var key3 = CreateCacheKey(7);
        var analysis1 = CreateAnalysis(0.7);
        var analysis2 = CreateAnalysis(0.8);
        var analysis3 = CreateAnalysis(0.9);

        cache.Set(key1, analysis1);
        cache.Set(key2, analysis2);
        cache.Set(key3, analysis3);

        var entries = cache.GetAllEntries().ToList();

        Assert.That(entries.Count, Is.EqualTo(3));
        Assert.That(entries.Any(e => e.Analysis.OverallScore == 0.7), Is.True);
        Assert.That(entries.Any(e => e.Analysis.OverallScore == 0.8), Is.True);
        Assert.That(entries.Any(e => e.Analysis.OverallScore == 0.9), Is.True);
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
