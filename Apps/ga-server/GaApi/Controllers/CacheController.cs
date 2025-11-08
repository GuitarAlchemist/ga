namespace GaApi.Controllers;

using Microsoft.AspNetCore.RateLimiting;
using Services;

[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("admin")]
public class CacheController(ICachingService cache, ILogger<CacheController> logger) : ControllerBase
{
    /// <summary>
    ///     Get cache statistics showing hit rates for regular vs semantic caches
    /// </summary>
    [HttpGet("stats")]
    public ActionResult<CacheStatistics> GetStatistics()
    {
        try
        {
            var stats = cache.GetStatistics();
            return Ok(stats);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting cache statistics");
            return StatusCode(500, "Error retrieving cache statistics");
        }
    }

    /// <summary>
    ///     Clear all caches (admin operation)
    /// </summary>
    [HttpPost("clear")]
    public ActionResult ClearCache()
    {
        try
        {
            cache.Clear();
            logger.LogInformation("Cache cleared by admin request");
            return Ok(new { message = "All caches cleared successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error clearing cache");
            return StatusCode(500, "Error clearing cache");
        }
    }

    /// <summary>
    ///     Remove a specific cache entry
    /// </summary>
    [HttpDelete("{key}")]
    public ActionResult RemoveCacheEntry(string key)
    {
        try
        {
            cache.Remove(key);
            logger.LogInformation("Cache entry removed: {Key}", key);
            return Ok(new { message = $"Cache entry '{key}' removed successfully" });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing cache entry: {Key}", key);
            return StatusCode(500, "Error removing cache entry");
        }
    }
}
