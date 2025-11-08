namespace GA.Business.Web.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

/// <summary>
///     Caching service for external web content to reduce API calls and improve performance
/// </summary>
public class WebContentCache(IMemoryCache cache, ILogger<WebContentCache> logger)
{
    private readonly MemoryCacheEntryOptions _defaultOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1),
        SlidingExpiration = TimeSpan.FromMinutes(15),
        Size = 1
    };

    // Default cache options: 1 hour expiration, sliding window

    /// <summary>
    ///     Get cached content or execute factory function if not cached
    /// </summary>
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null)
    {
        if (cache.TryGetValue(key, out T? cachedValue) && cachedValue != null)
        {
            logger.LogDebug("Cache hit for key: {Key}", key);
            return cachedValue;
        }

        logger.LogDebug("Cache miss for key: {Key}, fetching from source", key);

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = absoluteExpiration ?? TimeSpan.FromHours(1),
            SlidingExpiration = slidingExpiration ?? TimeSpan.FromMinutes(15),
            Size = 1
        };

        var value = await factory();
        cache.Set(key, value, options);

        return value;
    }

    /// <summary>
    ///     Invalidate cached content by key
    /// </summary>
    public void Invalidate(string key)
    {
        cache.Remove(key);
        logger.LogInformation("Invalidated cache for key: {Key}", key);
    }

    /// <summary>
    ///     Invalidate all cached content matching a pattern
    /// </summary>
    public void InvalidatePattern(string pattern)
    {
        // Note: IMemoryCache doesn't support pattern-based invalidation natively
        // This is a placeholder for future implementation with a more sophisticated cache
        logger.LogWarning("Pattern-based cache invalidation not yet implemented for pattern: {Pattern}", pattern);
    }
}
