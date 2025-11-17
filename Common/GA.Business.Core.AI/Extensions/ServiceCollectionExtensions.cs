namespace GA.Business.Core.AI.Extensions;

using GA.Business.Core.AI.Services.Embeddings;
using GA.Business.Core.AI.Services.SemanticSearch;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring AI services in the dependency injection container
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds AI services to the service collection
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddGuitarAlchemistAI(this IServiceCollection services)
    {
        // Register embedding services
        services.AddTransient<EmbeddingServiceFactory>();
        services.AddTransient<OnnxEmbeddingService>();
        services.AddTransient<OllamaEmbeddingService>();
        services.AddTransient<OpenAiEmbeddingService>();
        services.AddTransient<AzureOpenAiEmbeddingService>();
        services.AddTransient<HuggingFaceEmbeddingService>();
        services.AddTransient<BatchOllamaEmbeddingService>();
        
        // Register semantic search services
        services.AddTransient<SemanticSearchService>();
        services.AddTransient<SemanticFretboardService>();
        
        // Register GPU-accelerated services if available
        // TODO: Re-enable when ILGPU API issues are resolved
        // services.AddTransient<GPUAcceleratedEmbeddingService>();
        
        return services;
    }
    
    /// <summary>
    /// Adds embedding services with configuration
    /// </summary>
    /// <param name="services">The service collection</param>
    /// <param name="configureOptions">Configuration action</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddEmbeddingServices(
        this IServiceCollection services, 
        Action<EmbeddingServiceSettings>? configureOptions = null)
    {
        if (configureOptions != null)
        {
            services.Configure(configureOptions);
        }
        
        services.AddTransient<EmbeddingServiceFactory>();
        services.AddTransient<IEmbeddingService>(provider => 
            provider.GetRequiredService<EmbeddingServiceFactory>().CreateService());
            
        return services;
    }
}
