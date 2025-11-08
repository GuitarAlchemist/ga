namespace GaApi.Extensions;

using Configuration;
using Services;

/// <summary>
///     Extension methods for registering health check services
/// </summary>
public static class HealthCheckServiceExtensions
{
    /// <summary>
    ///     Registers all health check services including:
    ///     - Core health check service (IHealthCheckService)
    ///     - Monadic health check service for functional programming patterns
    ///     - Configuration validation service
    ///     - Performance metrics service
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddHealthCheckServices(this IServiceCollection services)
    {
        // Core health check service
        services.AddScoped<IHealthCheckService, HealthCheckService>();

        // Monadic health check service for functional programming patterns
        services.AddScoped<MonadicHealthCheckService>();

        // Configuration validation
        services.AddSingleton<ConfigurationValidator>();
        services.AddHostedService<ConfigurationValidationService>();

        // Performance metrics service to track when microservices split might be needed
        services.AddSingleton<PerformanceMetricsService>();

        return services;
    }
}
