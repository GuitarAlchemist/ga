namespace GaApi.Extensions;

using Services;

/// <summary>
///     Extension methods for registering vector search services
/// </summary>
public static class VectorSearchServiceExtensions
{
    /// <summary>
    ///     Registers the chord vector search strategies (In-Memory, CUDA, MongoDB).
    /// </summary>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    ///     A single <see cref="IVectorSearchStrategy" /> resolves to the last registration
    ///     (<see cref="MongoDbVectorSearchStrategy" />); the health check services consume it.
    /// </remarks>
    public static IServiceCollection AddVectorSearchServices(this IServiceCollection services)
    {
        // Register all vector search strategies
        services.AddSingleton<IVectorSearchStrategy, InMemoryVectorSearchStrategy>();
        services.AddSingleton<IVectorSearchStrategy, CudaVectorSearchStrategy>();
        services.AddSingleton<IVectorSearchStrategy, MongoDbVectorSearchStrategy>();

        return services;
    }
}
