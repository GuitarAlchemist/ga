namespace GaApi.Services;

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

/// <summary>
///     Service for managing cache invalidation strategies
/// </summary>
public interface ICacheInvalidationService
{
    /// <summary>
    ///     Invalidate a specific cache entry
    /// </summary>
    void Invalidate(string key);

    /// <summary>
    ///     Invalidate all cache entries matching a pattern
    /// </summary>
    void InvalidatePattern(string pattern);

    /// <summary>
    ///     Invalidate all cache entries with a specific tag
    /// </summary>
    void InvalidateByTag(string tag);

    /// <summary>
    ///     Invalidate all cache entries
    /// </summary>
    void InvalidateAll();

    /// <summary>
    ///     Register a cache entry with tags for later invalidation
    /// </summary>
    void RegisterEntry(string key, params string[] tags);

    /// <summary>
    ///     Register a dependency between cache entries (cascading invalidation)
    /// </summary>
    void RegisterDependency(string key, string dependsOn);
}

/// <summary>
///     Implementation of cache invalidation service
/// </summary>
public class CacheInvalidationService(
    IMemoryCache memoryCache,
    ILogger<CacheInvalidationService> logger) : ICacheInvalidationService
{
    private readonly ConcurrentBag<string> _allKeys = new();

    private readonly ConcurrentDictionary<string, HashSet<string>> _dependencies = new();

    // Track all cache keys for pattern matching and bulk invalidation
    private readonly ConcurrentDictionary<string, HashSet<string>> _keysByTag = new();

    public void Invalidate(string key)
    {
        logger.LogDebug("Invalidating cache key: {Key}", key);
        memoryCache.Remove(key);

        // Remove from tracking
        _allKeys.TryTake(out _);

        // Cascade to dependent keys
        if (_dependencies.TryGetValue(key, out var dependents))
        {
            foreach (var dependent in dependents)
            {
                logger.LogDebug("Cascading invalidation to dependent key: {DependentKey}", dependent);
                Invalidate(dependent);
            }
        }
    }

    public void InvalidatePattern(string pattern)
    {
        logger.LogInformation("Invalidating cache keys matching pattern: {Pattern}", pattern);

        var keysToInvalidate = _allKeys
            .Where(k => IsMatch(k, pattern))
            .ToList();

        foreach (var key in keysToInvalidate)
        {
            Invalidate(key);
        }

        logger.LogInformation("Invalidated {Count} cache entries matching pattern: {Pattern}",
            keysToInvalidate.Count, pattern);
    }

    public void InvalidateByTag(string tag)
    {
        logger.LogInformation("Invalidating cache entries with tag: {Tag}", tag);

        if (_keysByTag.TryGetValue(tag, out var keys))
        {
            foreach (var key in keys.ToList())
            {
                Invalidate(key);
            }

            logger.LogInformation("Invalidated {Count} cache entries with tag: {Tag}",
                keys.Count, tag);
        }
        else
        {
            logger.LogDebug("No cache entries found with tag: {Tag}", tag);
        }
    }

    public void InvalidateAll()
    {
        logger.LogWarning("Invalidating ALL cache entries");

        var keysToInvalidate = _allKeys.ToList();
        foreach (var key in keysToInvalidate)
        {
            memoryCache.Remove(key);
        }

        // Clear all tracking
        _keysByTag.Clear();
        _dependencies.Clear();
        _allKeys.Clear();

        logger.LogWarning("Invalidated {Count} cache entries", keysToInvalidate.Count);
    }

    public void RegisterEntry(string key, params string[] tags)
    {
        _allKeys.Add(key);

        foreach (var tag in tags)
        {
            _keysByTag.AddOrUpdate(
                tag,
                _ => new HashSet<string> { key },
                (_, existing) =>
                {
                    existing.Add(key);
                    return existing;
                });
        }

        logger.LogDebug("Registered cache key {Key} with tags: {Tags}",
            key, string.Join(", ", tags));
    }

    public void RegisterDependency(string key, string dependsOn)
    {
        _dependencies.AddOrUpdate(
            dependsOn,
            _ => new HashSet<string> { key },
            (_, existing) =>
            {
                existing.Add(key);
                return existing;
            });

        logger.LogDebug("Registered dependency: {Key} depends on {DependsOn}",
            key, dependsOn);
    }

    /// <summary>
    ///     Check if a key matches a pattern (supports * wildcard)
    /// </summary>
    private static bool IsMatch(string key, string pattern)
    {
        // Simple wildcard matching (* = any characters)
        if (pattern == "*")
        {
            return true;
        }

        if (!pattern.Contains('*'))
        {
            return key.Equals(pattern, StringComparison.OrdinalIgnoreCase);
        }

        // Convert pattern to regex
        var regexPattern = "^" + Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";

        return Regex.IsMatch(
            key,
            regexPattern,
            RegexOptions.IgnoreCase);
    }
}
