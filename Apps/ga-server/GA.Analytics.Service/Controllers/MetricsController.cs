namespace GA.Analytics.Service.Controllers;
using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.RateLimiting;
using GA.Analytics.Service.Services;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("admin")]
public class MetricsController(PerformanceMetricsService metrics, ILogger<MetricsController> logger)
    : ControllerBase
{
    /// <summary>
    ///     Get performance statistics comparing regular vs semantic operations
    ///     Helps identify when microservices split might be needed
    /// </summary>
    [HttpGet("performance")]
    public ActionResult<PerformanceStatistics> GetPerformanceStatistics()
    {
        try
        {
            var stats = metrics.GetStatistics();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting performance statistics");
            return StatusCode(500, "Error retrieving performance statistics");
        }
    }

    /// <summary>
    ///     Get comprehensive system metrics including cache and performance
    /// </summary>
    [HttpGet("system")]
    public ActionResult GetSystemMetrics(
        [FromServices] ICachingService cache)
    {
        try
        {
            var performanceStats = metrics.GetStatistics();
            var cacheStats = cache.GetStatistics();

            var systemMetrics = new
            {
                timestamp = DateTime.UtcNow,
                performance = performanceStats,
                cache = cacheStats,
                memory = new
                {
                    totalMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
                    gen0Collections = GC.CollectionCount(0),
                    gen1Collections = GC.CollectionCount(1),
                    gen2Collections = GC.CollectionCount(2)
                },
                recommendations = new[]
                {
                    performanceStats.SplitRecommendation,
                    GetCacheRecommendation(cacheStats),
                    GetMemoryRecommendation()
                }
            };

            return Ok(systemMetrics);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting system metrics");
            return StatusCode(500, "Error retrieving system metrics");
        }
    }

    private string GetCacheRecommendation(CacheStatistics stats)
    {
        if (stats.TotalHitRate > 0.8)
        {
            return "CACHE: Excellent hit rate (>80%), current strategy is working well";
        }

        if (stats.TotalHitRate > 0.5)
        {
            return "CACHE: Good hit rate (>50%), consider increasing cache size or TTL";
        }

        if (stats.TotalHitRate > 0.2)
        {
            return "CACHE: Low hit rate (<50%), review caching strategy";
        }

        return "CACHE: Very low hit rate (<20%), caching may not be effective for current workload";
    }

    private string GetMemoryRecommendation()
    {
        var totalMemoryMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0;
        var gen2Collections = GC.CollectionCount(2);

        if (totalMemoryMb > 1000)
        {
            return "MEMORY: High memory usage (>1GB), consider reducing cache size or implementing pagination";
        }

        if (gen2Collections > 100)
        {
            return "MEMORY: Frequent Gen2 collections, may indicate memory pressure";
        }

        return "MEMORY: Memory usage is healthy";
    }
}
