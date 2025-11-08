namespace GaApi.Services;

using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
///     Caching service with different strategies for regular vs semantic data
/// </summary>
public interface ICachingService
{
    /// <summary>
    ///     Get or create a cached value for regular data (longer TTL, more permissive)
    /// </summary>
    Task<T> GetOrCreateRegularAsync<T>(string key, Func<Task<T>> factory);

    /// <summary>
    ///     Get or create a cached value for semantic data (shorter TTL, more selective)
    /// </summary>
    Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory);

    /// <summary>
    ///     Remove a cached value
    /// </summary>
    void Remove(string key);

    /// <summary>
    ///     Clear all cached values
    /// </summary>
    void Clear();

    /// <summary>
    ///     Get cache statistics
    /// </summary>
    CacheStatistics GetStatistics();
}

public class CachingService(
    ILogger<CachingService> logger,
    ICacheMetricsService? metricsService = null) : ICachingService
{
    private readonly IMemoryCache _regularCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 1000, // Can cache more regular data
        CompactionPercentage = 0.25,
        ExpirationScanFrequency = TimeSpan.FromMinutes(2)
    });

    // Cache options for regular data (longer TTL, more permissive)
    private readonly MemoryCacheEntryOptions _regularOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
        SlidingExpiration = TimeSpan.FromMinutes(5),
        Size = 1,
        Priority = CacheItemPriority.Normal
    };

    private readonly IMemoryCache _semanticCache = new MemoryCache(new MemoryCacheOptions
    {
        SizeLimit = 100, // Limit semantic cache size (results are larger)
        CompactionPercentage = 0.5,
        ExpirationScanFrequency = TimeSpan.FromMinutes(1)
    });

    // Cache options for semantic data (shorter TTL, more selective)
    private readonly MemoryCacheEntryOptions _semanticOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        SlidingExpiration = TimeSpan.FromMinutes(2),
        Size = 1,
        Priority = CacheItemPriority.Low // Semantic results are expensive to compute but can be evicted
    };

    // Cache statistics (kept for backward compatibility)
    private long _regularHits;
    private long _regularMisses;
    private long _semanticHits;
    private long _semanticMisses;

    // Create separate memory caches with different size limits
    // Can cache more regular data
    // Limit semantic cache size (results are larger)

    public async Task<T> GetOrCreateRegularAsync<T>(string key, Func<Task<T>> factory)
    {
        var sw = Stopwatch.StartNew();

        if (_regularCache.TryGetValue(key, out T? cachedValue))
        {
            sw.Stop();
            Interlocked.Increment(ref _regularHits);
            metricsService?.RecordHit("Regular", key);
            metricsService?.RecordOperationDuration("Regular", "Get", sw.Elapsed);
            logger.LogDebug("Regular cache HIT for key: {Key}", key);
            return cachedValue!;
        }

        Interlocked.Increment(ref _regularMisses);
        metricsService?.RecordMiss("Regular", key);
        logger.LogDebug("Regular cache MISS for key: {Key}", key);

        var value = await factory();
        _regularCache.Set(key, value, _regularOptions);

        sw.Stop();
        metricsService?.RecordOperationDuration("Regular", "GetOrCreate", sw.Elapsed);

        return value;
    }

    public async Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_semanticCache.TryGetValue(key, out T? cachedValue))
        {
            Interlocked.Increment(ref _semanticHits);
            logger.LogDebug("Semantic cache HIT for key: {Key}", key);
            return cachedValue!;
        }

        Interlocked.Increment(ref _semanticMisses);
        logger.LogDebug("Semantic cache MISS for key: {Key}", key);

        var value = await factory();
        _semanticCache.Set(key, value, _semanticOptions);

        return value;
    }

    public void Remove(string key)
    {
        _regularCache.Remove(key);
        _semanticCache.Remove(key);
        logger.LogDebug("Removed cache entry for key: {Key}", key);
    }

    public void Clear()
    {
        // MemoryCache doesn't have a Clear method, so we need to dispose and recreate
        logger.LogInformation("Clearing all caches");

        if (_regularCache is MemoryCache regularMemCache)
        {
            regularMemCache.Compact(1.0);
        }

        if (_semanticCache is MemoryCache semanticMemCache)
        {
            semanticMemCache.Compact(1.0);
        }
    }

    public CacheStatistics GetStatistics()
    {
        var regularTotal = _regularHits + _regularMisses;
        var semanticTotal = _semanticHits + _semanticMisses;

        return new CacheStatistics
        {
            RegularHits = _regularHits,
            RegularMisses = _regularMisses,
            RegularHitRate = regularTotal > 0 ? (double)_regularHits / regularTotal : 0,
            SemanticHits = _semanticHits,
            SemanticMisses = _semanticMisses,
            SemanticHitRate = semanticTotal > 0 ? (double)_semanticHits / semanticTotal : 0,
            TotalHits = _regularHits + _semanticHits,
            TotalMisses = _regularMisses + _semanticMisses
        };
    }
}

public class CacheStatistics
{
    public long RegularHits { get; set; }
    public long RegularMisses { get; set; }
    public double RegularHitRate { get; set; }
    public long SemanticHits { get; set; }
    public long SemanticMisses { get; set; }
    public double SemanticHitRate { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }

    public double TotalHitRate => TotalHits + TotalMisses > 0
        ? (double)TotalHits / (TotalHits + TotalMisses)
        : 0;
}
