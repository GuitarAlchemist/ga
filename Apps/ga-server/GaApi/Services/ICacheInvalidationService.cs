namespace GaApi.Services;

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