namespace GaApi.Services;

using Constants;
using Microsoft.Extensions.Caching.Memory;
using Models;

/// <summary>
///     Business logic service for chord operations
/// </summary>
public interface IChordService
{
    Task<long> GetTotalCountAsync();
    Task<List<Chord>> GetByQualityAsync(string quality, int limit = 100);
    Task<List<Chord>> GetByExtensionAsync(string extension, int limit = 100);
    Task<List<Chord>> GetByStackingTypeAsync(string stackingType, int limit = 100);
    Task<List<Chord>> SearchChordsAsync(string query, int limit = 100);
    Task<Chord?> GetByIdAsync(string id);
    Task<List<Chord>> GetSimilarChordsAsync(string chordId, int limit = 10);
    Task<ChordStatistics> GetStatisticsAsync();
    Task<List<string>> GetAvailableQualitiesAsync();
    Task<List<string>> GetAvailableExtensionsAsync();
    Task<List<string>> GetAvailableStackingTypesAsync();
}

public class ChordService(MongoDbService mongoDb, IMemoryCache cache, ILogger<ChordService> logger)
    : IChordService
{
    private readonly ILogger<ChordService> _logger = logger;

    public async Task<long> GetTotalCountAsync()
    {
        const string cacheKey = "chord_total_count";

        if (cache.TryGetValue(cacheKey, out long cachedCount))
        {
            return cachedCount;
        }

        var count = await mongoDb.GetTotalChordCountAsync();
        cache.Set(cacheKey, count, TimeSpan.FromMinutes(5));

        return count;
    }

    public async Task<List<Chord>> GetByQualityAsync(string quality, int limit = 100)
    {
        var cacheKey = $"chords_quality_{quality}_{limit}";

        if (cache.TryGetValue(cacheKey, out List<Chord>? cachedChords))
        {
            return cachedChords!;
        }

        var chords = await mongoDb.GetChordsByQualityAsync(quality, limit);
        cache.Set(cacheKey, chords, TimeSpan.FromMinutes(10));

        return chords;
    }

    public async Task<List<Chord>> GetByExtensionAsync(string extension, int limit = 100)
    {
        var cacheKey = $"chords_extension_{extension}_{limit}";

        if (cache.TryGetValue(cacheKey, out List<Chord>? cachedChords))
        {
            return cachedChords!;
        }

        var chords = await mongoDb.GetChordsByExtensionAsync(extension, limit);
        cache.Set(cacheKey, chords, TimeSpan.FromMinutes(10));

        return chords;
    }

    public async Task<List<Chord>> GetByStackingTypeAsync(string stackingType, int limit = 100)
    {
        var cacheKey = $"chords_stacking_{stackingType}_{limit}";

        if (cache.TryGetValue(cacheKey, out List<Chord>? cachedChords))
        {
            return cachedChords!;
        }

        var chords = await mongoDb.GetChordsByStackingTypeAsync(stackingType, limit);
        cache.Set(cacheKey, chords, TimeSpan.FromMinutes(10));

        return chords;
    }

    public async Task<List<Chord>> SearchChordsAsync(string query, int limit = 100)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var cacheKey = CacheKeys.ChordSearch(query, limit);

        if (cache.TryGetValue(cacheKey, out List<Chord>? cachedChords))
        {
            return cachedChords!;
        }

        var chords = await mongoDb.SearchChordsAsync(query, limit);
        cache.Set(cacheKey, chords, CacheKeys.Durations.ChordSearch);

        return chords;
    }

    public async Task<Chord?> GetByIdAsync(string id)
    {
        var cacheKey = $"chord_{id}";

        if (cache.TryGetValue(cacheKey, out Chord? cachedChord))
        {
            return cachedChord;
        }

        var chord = await mongoDb.GetChordByIdAsync(id);
        if (chord != null)
        {
            cache.Set(cacheKey, chord, TimeSpan.FromMinutes(30));
        }

        return chord;
    }

    public async Task<List<Chord>> GetSimilarChordsAsync(string chordId, int limit = 10)
    {
        var cacheKey = $"similar_chords_{chordId}_{limit}";

        if (cache.TryGetValue(cacheKey, out List<Chord>? cachedChords))
        {
            return cachedChords!;
        }

        var chords = await mongoDb.GetSimilarChordsAsync(chordId, limit);
        cache.Set(cacheKey, chords, TimeSpan.FromMinutes(15));

        return chords;
    }

    public async Task<ChordStatistics> GetStatisticsAsync()
    {
        const string cacheKey = "chord_statistics";

        if (cache.TryGetValue(cacheKey, out ChordStatistics? cachedStats))
        {
            return cachedStats!;
        }

        var stats = await mongoDb.GetChordStatisticsAsync();
        cache.Set(cacheKey, stats, TimeSpan.FromMinutes(30));

        return stats;
    }

    public async Task<List<string>> GetAvailableQualitiesAsync()
    {
        const string cacheKey = "chord_qualities";

        if (cache.TryGetValue(cacheKey, out List<string>? cachedQualities))
        {
            return cachedQualities!;
        }

        var qualities = await mongoDb.GetDistinctQualitiesAsync();
        cache.Set(cacheKey, qualities, TimeSpan.FromHours(1));

        return qualities;
    }

    public async Task<List<string>> GetAvailableExtensionsAsync()
    {
        const string cacheKey = "chord_extensions";

        if (cache.TryGetValue(cacheKey, out List<string>? cachedExtensions))
        {
            return cachedExtensions!;
        }

        var extensions = await mongoDb.GetDistinctExtensionsAsync();
        cache.Set(cacheKey, extensions, TimeSpan.FromHours(1));

        return extensions;
    }

    public async Task<List<string>> GetAvailableStackingTypesAsync()
    {
        const string cacheKey = "chord_stacking_types";

        if (cache.TryGetValue(cacheKey, out List<string>? cachedTypes))
        {
            return cachedTypes!;
        }

        var types = await mongoDb.GetDistinctStackingTypesAsync();
        cache.Set(cacheKey, types, TimeSpan.FromHours(1));

        return types;
    }
}

/// <summary>
///     Statistics about the chord database
/// </summary>
public class ChordStatistics
{
    public long TotalChords { get; set; }
    public Dictionary<string, int> QualityDistribution { get; set; } = new();
    public Dictionary<string, int> ExtensionDistribution { get; set; } = new();
    public Dictionary<string, int> StackingTypeDistribution { get; set; } = new();
    public Dictionary<int, int> NoteCountDistribution { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
