namespace GaApi.Extensions;

using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Abstractions;
using GA.Business.ML.Configuration;
using GA.Business.ML.Text.Ollama;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Services;

/// <summary>
/// Extension methods for registering AI and ML services
/// </summary>
public static class AiServiceExtensions
{
    /// <summary>
    /// Add all AI and ML services to the service collection
    /// </summary>
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register embedding services
        services.AddEmbeddingServices(configuration);

        // Register LLM services
        services.AddLlmServices(configuration);

        // Register vector search services
        services.AddVectorSearchServices(configuration);

        // Register chatbot services
        services.AddChatbotServices(configuration);

        return services;
    }

    /// <summary>
    /// Add embedding generation services
    /// </summary>
    private static IServiceCollection AddEmbeddingServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register local embedding service (ONNX-based)
        services.AddSingleton<LocalEmbeddingService>();

        // Register the Ollama embedding service (primary)
        var ollamaEndpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
        var embeddingModel = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        var maxConcurrentRequests = configuration.GetValue<int>("Ollama:MaxConcurrentRequests", 20);

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
        // Register batch embedding service for high-performance concurrent embedding generation
        services.AddSingleton<GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService>(sp =>
        {
            var httpClient = new HttpClient { BaseAddress = new Uri(ollamaEndpoint) };
            var logger = sp.GetRequiredService<ILogger<GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService>>();
            return new GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService(
                httpClient,
                embeddingModel,
                maxConcurrentRequests,
                logger);
        });

        // Register single embedding service (wraps batch service for backward compatibility)
        services.AddSingleton<ITextEmbeddingService>(sp =>
        {
            var batchService = sp.GetRequiredService<GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService>();
            return new BatchEmbeddingServiceAdapter(batchService);
        });
#pragma warning restore SKEXP0001

        return services;
    }

    /// <summary>
    /// Adapter to make IBatchEmbeddingService compatible with ITextEmbeddingService
    /// </summary>
    private class BatchEmbeddingServiceAdapter(GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService batchService) : ITextEmbeddingService
    {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var results = await batchService.GenerateBatchEmbeddingsAsync(new[] { text });
            return [.. results[0]];
        }
#pragma warning restore SKEXP0001
    }

    /// <summary>
    /// Add LLM (Large Language Model) services
    /// </summary>
    private static IServiceCollection AddLlmServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Ollama chat service
        services.AddSingleton<IOllamaChatService, OllamaChatService>();

        return services;
    }

    /// <summary>
    /// Add vector search services
    /// </summary>
    private static IServiceCollection AddVectorSearchServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register MongoDB service for vector storage
        services.AddSingleton<MongoDbService>();

        // Register vector search service
        services.AddSingleton<VectorSearchService>();

        // Register enhanced vector search with multiple strategies
        services.AddSingleton<EnhancedVectorSearchService>();

        return services;
    }

    /// <summary>
    /// Add chatbot services (orchestrator, knowledge source, embedding)
    /// </summary>
    private static IServiceCollection AddChatbotServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Ollama embedding service for generating embeddings from user queries
        services.AddSingleton<Services.OllamaEmbeddingService>();

        // Register semantic knowledge source (bridges voicing search to chatbot)
        services.AddSingleton<SemanticKnowledgeSource>();
        services.AddSingleton<ISemanticKnowledgeSource>(sp =>
            sp.GetRequiredService<SemanticKnowledgeSource>());

        // Register chatbot session orchestrator (manages conversation flow)
        services.AddScoped<ChatbotSessionOrchestrator>();

        return services;
    }
}

