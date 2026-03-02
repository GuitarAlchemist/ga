namespace GaApi.Services;

using System.Collections.Concurrent;

public class CacheMetricsService(ILogger<CacheMetricsService> logger) : ICacheMetricsService
{
    private readonly ConcurrentDictionary<string, CacheTypeMetricsInternal> _metrics = new();

    public void RecordHit(string cacheType, string key)
    {
        var metrics = GetOrCreateMetrics(cacheType);
        Interlocked.Increment(ref metrics.Hits);
        metrics.LastRequestTime = DateTime.UtcNow;

        logger.LogDebug("Cache HIT: {CacheType} - {Key}", cacheType, key);
    }

    public void RecordMiss(string cacheType, string key)
    {
        var metrics = GetOrCreateMetrics(cacheType);
        Interlocked.Increment(ref metrics.Misses);
        metrics.LastRequestTime = DateTime.UtcNow;

        logger.LogDebug("Cache MISS: {CacheType} - {Key}", cacheType, key);
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

        logger.LogDebug("Cache operation: {CacheType}.{Operation} took {DurationMs}ms",
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

    public Dictionary<string, CacheTypeMetrics> GetAllMetrics() =>
        _metrics.ToDictionary(
            kvp => kvp.Key,
            kvp => GetMetrics(kvp.Key));

    public void Reset()
    {
        _metrics.Clear();
        logger.LogInformation("Cache metrics reset");
    }

    private CacheTypeMetricsInternal GetOrCreateMetrics(string cacheType) =>
        _metrics.GetOrAdd(cacheType, _ => new CacheTypeMetricsInternal
        {
            CacheType = cacheType,
            FirstRequestTime = DateTime.UtcNow,
            LastRequestTime = DateTime.UtcNow
        });

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
