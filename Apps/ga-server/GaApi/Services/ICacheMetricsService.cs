namespace GaApi.Services;

/// <summary>
///     Centralized cache metrics tracking service
/// </summary>
public interface ICacheMetricsService
{
    /// <summary>
    ///     Record a cache hit
    /// </summary>
    void RecordHit(string cacheType, string key);

    /// <summary>
    ///     Record a cache miss
    /// </summary>
    void RecordMiss(string cacheType, string key);

    /// <summary>
    ///     Record cache operation duration
    /// </summary>
    void RecordOperationDuration(string cacheType, string operation, TimeSpan duration);

    /// <summary>
    ///     Get metrics for a specific cache type
    /// </summary>
    CacheTypeMetrics GetMetrics(string cacheType);

    /// <summary>
    ///     Get metrics for all cache types
    /// </summary>
    Dictionary<string, CacheTypeMetrics> GetAllMetrics();

    /// <summary>
    ///     Reset all metrics
    /// </summary>
    void Reset();
}