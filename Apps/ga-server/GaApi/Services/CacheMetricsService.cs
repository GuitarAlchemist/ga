namespace GaApi.Services;

using System.Collections.Concurrent;

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

/// <summary>
///     Metrics for a specific cache type
/// </summary>
public class CacheTypeMetrics
{
    public string CacheType { get; set; } = string.Empty;
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalRequests => TotalHits + TotalMisses;
    public double HitRate => TotalRequests > 0 ? (double)TotalHits / TotalRequests : 0;
    public double MissRate => TotalRequests > 0 ? (double)TotalMisses / TotalRequests : 0;
    public Dictionary<string, OperationMetrics> Operations { get; set; } = new();
    public DateTime FirstRequestTime { get; set; }
    public DateTime LastRequestTime { get; set; }
}

/// <summary>
///     Metrics for a specific cache operation
/// </summary>
public class OperationMetrics
{
    public string Operation { get; set; } = string.Empty;
    public long Count { get; set; }
    public double AverageDurationMs { get; set; }
    public double MinDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public double TotalDurationMs { get; set; }
}

public class CacheMetricsService : ICacheMetricsService
{
    private readonly ILogger<CacheMetricsService> _logger;
    private readonly ConcurrentDictionary<string, CacheTypeMetricsInternal> _metrics = new();

    public CacheMetricsService(ILogger<CacheMetricsService> logger)
    {
        _logger = logger;
    }

    public void RecordHit(string cacheType, string key)
    {
        var metrics = GetOrCreateMetrics(cacheType);
        Interlocked.Increment(ref metrics.Hits);
        metrics.LastRequestTime = DateTime.UtcNow;

        _logger.LogDebug("Cache HIT: {CacheType} - {Key}", cacheType, key);
    }

    public void RecordMiss(string cacheType, string key)
    {
        var metrics = GetOrCreateMetrics(cacheType);
        Interlocked.Increment(ref metrics.Misses);
        metrics.LastRequestTime = DateTime.UtcNow;

        _logger.LogDebug("Cache MISS: {CacheType} - {Key}", cacheType, key);
    }

    public void RecordOperationDuration(string cacheType, string operation, TimeSpan duration)
    {
        var metrics = GetOrCreateMetrics(cacheType);
        var durationMs = duration.TotalMilliseconds;

        metrics.Operations.AddOrUpdate(
            operation,
            _ => new OperationMetricsInternal
            {
                Operation = operation,
                Count = 1,
                TotalDurationMs = durationMs,
                MinDurationMs = durationMs,
                MaxDurationMs = durationMs
            },
            (_, existing) =>
            {
                Interlocked.Increment(ref existing.Count);
                Interlocked.Exchange(ref existing.TotalDurationMs, existing.TotalDurationMs + durationMs);

                // Update min/max (not thread-safe but acceptable for metrics)
                if (durationMs < existing.MinDurationMs)
                {
                    existing.MinDurationMs = durationMs;
                }

                if (durationMs > existing.MaxDurationMs)
                {
                    existing.MaxDurationMs = durationMs;
                }

                return existing;
            });

        _logger.LogDebug("Cache operation: {CacheType}.{Operation} took {DurationMs}ms",
            cacheType, operation, durationMs);
    }

    public CacheTypeMetrics GetMetrics(string cacheType)
    {
        if (!_metrics.TryGetValue(cacheType, out var internalMetrics))
        {
            return new CacheTypeMetrics { CacheType = cacheType };
        }

        return new CacheTypeMetrics
        {
            CacheType = cacheType,
            TotalHits = internalMetrics.Hits,
            TotalMisses = internalMetrics.Misses,
            FirstRequestTime = internalMetrics.FirstRequestTime,
            LastRequestTime = internalMetrics.LastRequestTime,
            Operations = internalMetrics.Operations.ToDictionary(
                kvp => kvp.Key,
                kvp => new OperationMetrics
                {
                    Operation = kvp.Value.Operation,
                    Count = kvp.Value.Count,
                    TotalDurationMs = kvp.Value.TotalDurationMs,
                    MinDurationMs = kvp.Value.MinDurationMs,
                    MaxDurationMs = kvp.Value.MaxDurationMs,
                    AverageDurationMs = kvp.Value.Count > 0
                        ? kvp.Value.TotalDurationMs / kvp.Value.Count
                        : 0
                })
        };
    }

    public Dictionary<string, CacheTypeMetrics> GetAllMetrics()
    {
        return _metrics.ToDictionary(
            kvp => kvp.Key,
            kvp => GetMetrics(kvp.Key));
    }

    public void Reset()
    {
        _metrics.Clear();
        _logger.LogInformation("Cache metrics reset");
    }

    private CacheTypeMetricsInternal GetOrCreateMetrics(string cacheType)
    {
        return _metrics.GetOrAdd(cacheType, _ => new CacheTypeMetricsInternal
        {
            CacheType = cacheType,
            FirstRequestTime = DateTime.UtcNow,
            LastRequestTime = DateTime.UtcNow
        });
    }

    private class CacheTypeMetricsInternal
    {
        public long Hits;
        public long Misses;
        public string CacheType { get; set; } = string.Empty;
        public DateTime FirstRequestTime { get; set; }
        public DateTime LastRequestTime { get; set; }
        public ConcurrentDictionary<string, OperationMetricsInternal> Operations { get; } = new();
    }

    private class OperationMetricsInternal
    {
        public long Count;
        public double MaxDurationMs;
        public double MinDurationMs;
        public double TotalDurationMs;
        public string Operation { get; set; } = string.Empty;
    }
}
