namespace GaApi.Controllers;

using Models;
using Services;

/// <summary>
///     Controller for cache metrics and monitoring
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class CacheMetricsController(
    ICacheMetricsService metricsService,
    ICacheInvalidationService invalidationService,
    ILogger<CacheMetricsController> logger) : ControllerBase
{
    /// <summary>
    ///     Get metrics for all cache types
    /// </summary>
    /// <returns>Cache metrics for all cache types</returns>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<string, CacheTypeMetrics>>), StatusCodes.Status200OK)]
    public IActionResult GetAllMetrics()
    {
        try
        {
            var metrics = metricsService.GetAllMetrics();

            logger.LogInformation("Retrieved metrics for {Count} cache types", metrics.Count);

            return Ok(ApiResponse<Dictionary<string, CacheTypeMetrics>>.Ok(metrics));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving cache metrics");
            return StatusCode(500, ApiResponse<Dictionary<string, CacheTypeMetrics>>.Fail(
                "Failed to retrieve cache metrics",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }

    /// <summary>
    ///     Get metrics for a specific cache type
    /// </summary>
    /// <param name="cacheType">The cache type to get metrics for</param>
    /// <returns>Cache metrics for the specified cache type</returns>
    [HttpGet("{cacheType}")]
    [ProducesResponseType(typeof(ApiResponse<CacheTypeMetrics>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CacheTypeMetrics>), StatusCodes.Status404NotFound)]
    public IActionResult GetMetrics(string cacheType)
    {
        try
        {
            var metrics = metricsService.GetMetrics(cacheType);

            if (metrics.TotalRequests == 0)
            {
                return NotFound(ApiResponse<CacheTypeMetrics>.Fail(
                    $"No metrics found for cache type: {cacheType}",
                    null,
                    ErrorCodes.NotFound));
            }

            logger.LogInformation("Retrieved metrics for cache type: {CacheType}", cacheType);

            return Ok(ApiResponse<CacheTypeMetrics>.Ok(metrics));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving metrics for cache type: {CacheType}", cacheType);
            return StatusCode(500, ApiResponse<CacheTypeMetrics>.Fail(
                "Failed to retrieve cache metrics",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }

    /// <summary>
    ///     Get cache statistics summary
    /// </summary>
    /// <returns>Summary of cache statistics across all cache types</returns>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<CacheSummary>), StatusCodes.Status200OK)]
    public IActionResult GetSummary()
    {
        try
        {
            var allMetrics = metricsService.GetAllMetrics();

            var summary = new CacheSummary
            {
                TotalCacheTypes = allMetrics.Count,
                TotalHits = allMetrics.Values.Sum(m => m.TotalHits),
                TotalMisses = allMetrics.Values.Sum(m => m.TotalMisses),
                TotalRequests = allMetrics.Values.Sum(m => m.TotalRequests),
                OverallHitRate = allMetrics.Values.Sum(m => m.TotalRequests) > 0
                    ? (double)allMetrics.Values.Sum(m => m.TotalHits) / allMetrics.Values.Sum(m => m.TotalRequests)
                    : 0,
                CacheTypes = allMetrics.Select(kvp => new CacheTypeSummary
                {
                    CacheType = kvp.Key,
                    HitRate = kvp.Value.HitRate,
                    TotalRequests = kvp.Value.TotalRequests,
                    LastRequestTime = kvp.Value.LastRequestTime
                }).OrderByDescending(c => c.TotalRequests).ToList()
            };

            logger.LogInformation("Retrieved cache summary: {TotalRequests} requests, {HitRate:P2} hit rate",
                summary.TotalRequests, summary.OverallHitRate);

            return Ok(ApiResponse<CacheSummary>.Ok(summary));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving cache summary");
            return StatusCode(500, ApiResponse<CacheSummary>.Fail(
                "Failed to retrieve cache summary",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }

    /// <summary>
    ///     Reset all cache metrics
    /// </summary>
    /// <returns>Success response</returns>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public IActionResult ResetMetrics()
    {
        try
        {
            metricsService.Reset();

            logger.LogWarning("Cache metrics reset by user request");

            return Ok(ApiResponse<string>.Ok("Metrics reset successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error resetting cache metrics");
            return StatusCode(500, ApiResponse<string>.Fail(
                "Failed to reset cache metrics",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }

    /// <summary>
    ///     Invalidate a specific cache entry
    /// </summary>
    /// <param name="key">The cache key to invalidate</param>
    /// <returns>Success response</returns>
    [HttpDelete("invalidate/{key}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public IActionResult InvalidateKey(string key)
    {
        try
        {
            invalidationService.Invalidate(key);
            logger.LogInformation("Invalidated cache key: {Key}", key);
            return Ok(ApiResponse<string>.Ok($"Cache key '{key}' invalidated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidating cache key: {Key}", key);
            return StatusCode(500, ApiResponse<string>.Fail(
                "Failed to invalidate cache key",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }

    /// <summary>
    ///     Invalidate cache entries matching a pattern
    /// </summary>
    /// <param name="pattern">The pattern to match (supports * wildcard)</param>
    /// <returns>Success response</returns>
    [HttpDelete("invalidate/pattern/{pattern}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public IActionResult InvalidatePattern(string pattern)
    {
        try
        {
            invalidationService.InvalidatePattern(pattern);
            logger.LogInformation("Invalidated cache entries matching pattern: {Pattern}", pattern);
            return Ok(ApiResponse<string>.Ok($"Cache entries matching '{pattern}' invalidated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidating cache pattern: {Pattern}", pattern);
            return StatusCode(500, ApiResponse<string>.Fail(
                "Failed to invalidate cache pattern",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }

    /// <summary>
    ///     Invalidate cache entries by tag
    /// </summary>
    /// <param name="tag">The tag to invalidate</param>
    /// <returns>Success response</returns>
    [HttpDelete("invalidate/tag/{tag}")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public IActionResult InvalidateByTag(string tag)
    {
        try
        {
            invalidationService.InvalidateByTag(tag);
            logger.LogInformation("Invalidated cache entries with tag: {Tag}", tag);
            return Ok(ApiResponse<string>.Ok($"Cache entries with tag '{tag}' invalidated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidating cache by tag: {Tag}", tag);
            return StatusCode(500, ApiResponse<string>.Fail(
                "Failed to invalidate cache by tag",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }

    /// <summary>
    ///     Invalidate all cache entries
    /// </summary>
    /// <returns>Success response</returns>
    [HttpDelete("invalidate/all")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    public IActionResult InvalidateAll()
    {
        try
        {
            invalidationService.InvalidateAll();
            logger.LogWarning("Invalidated ALL cache entries");
            return Ok(ApiResponse<string>.Ok("All cache entries invalidated successfully"));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error invalidating all cache entries");
            return StatusCode(500, ApiResponse<string>.Fail(
                "Failed to invalidate all cache entries",
                ex.Message,
                ErrorCodes.InternalError));
        }
    }
}

/// <summary>
///     Summary of cache statistics
/// </summary>
public class CacheSummary
{
    public int TotalCacheTypes { get; set; }
    public long TotalHits { get; set; }
    public long TotalMisses { get; set; }
    public long TotalRequests { get; set; }
    public double OverallHitRate { get; set; }
    public List<CacheTypeSummary> CacheTypes { get; set; } = new();
}

/// <summary>
///     Summary for a specific cache type
/// </summary>
public class CacheTypeSummary
{
    public string CacheType { get; set; } = string.Empty;
    public double HitRate { get; set; }
    public long TotalRequests { get; set; }
    public DateTime LastRequestTime { get; set; }
}
