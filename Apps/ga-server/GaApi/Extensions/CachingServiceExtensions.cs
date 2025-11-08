namespace GaApi.Extensions;

using Services;

/// <summary>
///     Extension methods for registering caching services
/// </summary>
public static class CachingServiceExtensions
{
    /// <summary>
    ///     Registers all caching services including:
    ///     - Core caching service with separate strategies for regular vs semantic data
    ///     - Cache metrics service for monitoring and performance tracking
    ///     - Cache invalidation service for managing cache invalidation strategies
    ///     - Cache warming background service to preload frequently accessed data
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    ///     Caching Strategy:
    ///     - Regular cache: Short TTL (5-15 minutes) for frequently changing data
    ///     - Semantic cache: Longer TTL (30-60 minutes) for embedding-based searches
    ///     - Cache warming: Preloads common chord progressions and scales on startup
    ///     - Metrics: Tracks hit/miss rates, memory usage, and performance
    /// </remarks>
    public static IServiceCollection AddCachingServices(this IServiceCollection services)
    {
        // Add memory caching (legacy - for backward compatibility)
        services.AddMemoryCache();

        // Add cache metrics service for monitoring and performance tracking
        services.AddSingleton<ICacheMetricsService, CacheMetricsService>();

        // Add cache invalidation service for managing cache invalidation strategies
        services.AddSingleton<ICacheInvalidationService, CacheInvalidationService>();

        // Add custom caching service with separate strategies for regular vs semantic data
        services.AddSingleton<ICachingService, CachingService>();

        // Add cache warming background service to preload frequently accessed data
        services.AddHostedService<CacheWarmingService>();

        return services;
    }
}
