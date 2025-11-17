namespace GA.Data.SemanticKernel.Embeddings.Extensions;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGaSemanticKernel(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<EmbeddingConfig>(
            configuration.GetSection("Embeddings"));

        // Register Semantic Kernel builder
        services.AddScoped(_ =>
        {
            var config = configuration
                .GetSection("Embeddings")
                .Get<EmbeddingConfig>();

            // Create HTTP client for Ollama
            var httpClient = new HttpClient
            {
                BaseAddress = new Uri(config?.Endpoint ?? "http://localhost:11434")
            };

            // Create the embedding generation service
            var embeddingService = new OllamaTextEmbeddingGeneration(
                httpClient,
                config?.ModelName ?? "nomic-embed-text");

            // Build and configure the kernel
            var builder = Kernel.CreateBuilder();
            builder.Services.AddKeyedSingleton<ITextEmbeddingGenerationService>("embeddings", embeddingService);

            return builder.Build();
        });

        // Register our embedding service wrapper
        services.AddScoped<IEmbeddingService, OllamaEmbeddingService>();

        return services;
    }
}
