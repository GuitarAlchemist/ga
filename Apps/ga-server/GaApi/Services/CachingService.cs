namespace GaApi.Services;

using System.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

public class CachingService(
    HybridCache cache,
    ILogger<CachingService> logger,
    ICacheMetricsService? metricsService = null) : ICachingService
{
    private readonly HybridCacheEntryOptions _regularOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(15),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private readonly HybridCacheEntryOptions _semanticOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(2)
    };

    private long _regularHits;
    private long _regularMisses;
    private long _semanticHits;
    private long _semanticMisses;

    public async Task<T> GetOrCreateRegularAsync<T>(string key, Func<Task<T>> factory)
    {
        var sw = Stopwatch.StartNew();
        
        var value = await cache.GetOrCreateAsync(
            key, 
            async cancel => 
            {
                Interlocked.Increment(ref _regularMisses);
                metricsService?.RecordMiss("Regular", key);
                logger.LogDebug("Regular cache MISS for key: {Key}", key);
                return await factory();
            },
            options: _regularOptions
        );

        sw.Stop();
        
        // Approximation of hit, since HybridCache abstract it away 
        // If we want exact timing, we'd wrap factory, but GetOrCreate hides the cache check timing
        metricsService?.RecordOperationDuration("Regular", "GetOrCreate", sw.Elapsed);

        return value;
    }

    public async Task<T> GetOrCreateSemanticAsync<T>(string key, Func<Task<T>> factory) =>
        await cache.GetOrCreateAsync(
            key, 
            async cancel => 
            {
                Interlocked.Increment(ref _semanticMisses);
                logger.LogDebug("Semantic cache MISS for key: {Key}", key);
                return await factory();
            },
            options: _semanticOptions
        );

    public void Remove(string key)
    {
        cache.RemoveAsync(key).AsTask().Wait();
        logger.LogDebug("Removed cache entry for key: {Key}", key);
    }

    public void Clear() =>
        // HybridCache doesn't natively support clearing the entire distributed + local cache out of the box in a single method
        logger.LogWarning("Clear() is not fully supported by HybridCache without tracking tags.");

    public CacheStatistics GetStatistics() => new()
    {
        RegularHits = _regularHits,
        RegularMisses = _regularMisses,
        RegularHitRate = (_regularHits + _regularMisses) > 0 ? (double)_regularHits / (_regularHits + _regularMisses) : 0,
        SemanticHits = _semanticHits,
        SemanticMisses = _semanticMisses,
        SemanticHitRate = (_semanticHits + _semanticMisses) > 0 ? (double)_semanticHits / (_semanticHits + _semanticMisses) : 0,
        TotalHits = _regularHits + _semanticHits,
        TotalMisses = _regularMisses + _semanticMisses
    };
}
