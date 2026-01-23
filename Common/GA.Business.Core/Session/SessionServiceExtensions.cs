namespace GA.Business.Core.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// Extension methods for registering session context services
/// </summary>
public static class SessionServiceExtensions
{
    /// <summary>
    /// Registers session context provider as a singleton (shared across all requests)
    /// </summary>
    /// <remarks>
    /// Use for desktop applications or single-user scenarios.
    /// The context will be shared across the entire application lifetime.
    /// </remarks>
    public static IServiceCollection AddSessionContextSingleton(this IServiceCollection services)
    {
        services.TryAddSingleton<ISessionContextProvider, InMemorySessionContextProvider>();
        return services;
    }
    
    /// <summary>
    /// Registers session context provider as scoped (one per request/scope)
    /// </summary>
    /// <remarks>
    /// Use for web applications where each user/request should have isolated context.
    /// The context will be created per HTTP request or per scope.
    /// </remarks>
    public static IServiceCollection AddSessionContextScoped(this IServiceCollection services)
    {
        services.TryAddScoped<ISessionContextProvider, InMemorySessionContextProvider>();
        return services;
    }
    
    /// <summary>
    /// Registers session context provider as transient (new instance each time)
    /// </summary>
    /// <remarks>
    /// Use for stateless services or when context is passed explicitly.
    /// Not recommended for most scenarios.
    /// </remarks>
    public static IServiceCollection AddSessionContextTransient(this IServiceCollection services)
    {
        services.TryAddTransient<ISessionContextProvider, InMemorySessionContextProvider>();
        return services;
    }
}
