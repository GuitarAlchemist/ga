namespace GaApi.Extensions;

using Services;

/// <summary>
///     Extension methods for registering vector search services
/// </summary>
public static class VectorSearchServiceExtensions
{
    /// <summary>
    ///     Registers all vector search services including:
    ///     - Multiple search strategies (In-Memory, CUDA, MongoDB)
    ///     - Strategy manager for dynamic strategy selection
    ///     - Enhanced vector search service with fallback support
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    /// <remarks>
    ///     Vector Search Strategies:
    ///     - InMemoryVectorSearchStrategy: Fast, limited capacity, good for development
    ///     - CudaVectorSearchStrategy: GPU-accelerated, high performance, requires CUDA
    ///     - MongoDbVectorSearchStrategy: Scalable, persistent, good for production
    ///     The strategy manager automatically selects the best available strategy based on:
    ///     - Hardware availability (GPU presence)
    ///     - Data size (small datasets use in-memory, large use MongoDB)
    ///     - Performance requirements (real-time vs batch processing)
    /// </remarks>
    public static IServiceCollection AddVectorSearchServices(this IServiceCollection services)
    {
        // Register all vector search strategies
        services.AddSingleton<IVectorSearchStrategy, InMemoryVectorSearchStrategy>();
        services.AddSingleton<IVectorSearchStrategy, CudaVectorSearchStrategy>();
        services.AddSingleton<IVectorSearchStrategy, MongoDbVectorSearchStrategy>();

        // Register strategy manager for dynamic strategy selection
        services.AddSingleton<VectorSearchStrategyManager>();

        // Register enhanced vector search service with fallback support
        services.AddSingleton<EnhancedVectorSearchService>();

        return services;
    }
}
