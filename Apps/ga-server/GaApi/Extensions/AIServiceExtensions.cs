namespace GaApi.Extensions;

using MongoDbService = GA.Data.MongoDB.Services.MongoDbService;
using IEmbeddingService = GA.Business.Core.AI.Services.Embeddings.IEmbeddingService;
using Services;
using Services.DocumentProcessing;
using Services.AutonomousCuration;

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

        // Register document processing services
        services.AddDocumentProcessingServices();

        // Register autonomous curation services
        services.AddAutonomousCurationServices();

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
        services.AddSingleton<IEmbeddingService>(sp =>
        {
            var batchService = sp.GetRequiredService<GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService>();
            return new BatchEmbeddingServiceAdapter(batchService);
        });
#pragma warning restore SKEXP0001

        return services;
    }

    /// <summary>
    /// Adapter to make IBatchEmbeddingService compatible with IEmbeddingService
    /// </summary>
    private class BatchEmbeddingServiceAdapter : IEmbeddingService
    {
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only
        private readonly GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService _batchService;

        public BatchEmbeddingServiceAdapter(GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService batchService)
        {
            _batchService = batchService;
        }

        public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            var results = await _batchService.GenerateBatchEmbeddingsAsync(new[] { text });
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
    /// Add document processing services
    /// </summary>
    private static IServiceCollection AddDocumentProcessingServices(this IServiceCollection services)
    {
        // Register YouTube transcript extractor
        services.AddSingleton<YouTubeTranscriptExtractor>();

        // Register document ingestion pipeline
        services.AddSingleton<DocumentIngestionPipeline>();

        // Register guitar technique processor
        services.AddSingleton<GuitarTechniqueProcessor>();

        // Register music theory knowledge processor
        services.AddSingleton<MusicTheoryKnowledgeProcessor>();

        // Register style learning processor
        services.AddSingleton<StyleLearningProcessor>();

        return services;
    }

    /// <summary>
    /// Add autonomous curation services
    /// </summary>
    private static IServiceCollection AddAutonomousCurationServices(this IServiceCollection services)
    {
        // Register knowledge gap analyzer
        services.AddSingleton<KnowledgeGapAnalyzer>();

        // Register YouTube search service
        services.AddSingleton<YouTubeSearchService>();

        // Register video quality evaluator
        services.AddSingleton<VideoQualityEvaluator>();

        // Register autonomous curation orchestrator
        services.AddSingleton<AutonomousCurationOrchestrator>();

        return services;
    }
}

