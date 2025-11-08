namespace GA.Business.Web;

using Microsoft.Extensions.DependencyInjection;
using Services;

/// <summary>
///     Extension methods for registering web integration services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add web integration services to the service collection
    /// </summary>
    public static IServiceCollection AddWebIntegrationServices(this IServiceCollection services)
    {
        // Register HTTP client
        services.AddHttpClient();

        // Register memory cache
        services.AddMemoryCache(options =>
        {
            options.SizeLimit = 100; // Limit cache size
        });

        // Register web integration services
        services.AddSingleton<WebContentCache>();
        services.AddSingleton<WebScrapingService>();
        services.AddSingleton<FeedReaderService>();
        services.AddSingleton<WebSearchService>();

        return services;
    }
}
