namespace GaApi.Extensions;

using Configuration;
using GA.Business.ML.Extensions;
using GA.Business.ML.Providers;
using GA.Domain.Repositories;
using GA.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.AI;
using Services;

/// <summary>
///     Extension methods for registering AI and ML services
/// </summary>
public static class AiServiceExtensions
{
    /// <summary>
    ///     Add all AI and ML services to the service collection
    /// </summary>
    public static IServiceCollection AddAiServices(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Register Core Guitar Alchemist AI Services (Embeddings, Agents, ML)
        services.AddGuitarAlchemistAI();

        // 2. Register Platform-Specific Embedding Services (Ollama, Onnx)
        // Note: Generic ITextEmbeddingService is already registered by AddGuitarAlchemistAI
        // We add specific API implementations here if needed, or configure settings.
        services.AddEmbeddingServices(options =>
        {
            options.OllamaHost = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            options.ModelName = configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        });

        // 3. Register MEAI Embedding Generator (used by SemanticRouter)
        services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
            OllamaProvider.CreateEmbeddingGeneratorFromConfig(configuration, sp.GetService<ILogger<OllamaEmbeddingGenerator>>()));

        // 4. Register Repository
        services.AddSingleton<ITabCorpusRepository, InMemoryTabCorpusRepository>();
        services.AddSingleton<IProgressionCorpusRepository, InMemoryProgressionCorpusRepository>();

        // 5. Register LLM Services (Chat)
        services.AddLlmServices(configuration);

        // 6. Register Vector Search & Retrieval
        services.AddVectorSearchApplicationServices(configuration);

        // 7. Register Chatbot Application Services
        services.AddChatbotServices(configuration);

        return services;
    }

    /// <summary>
    ///     Add LLM (Large Language Model) services
    /// </summary>
    private static IServiceCollection AddLlmServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Ollama chat service
        services.AddSingleton<IOllamaChatService, OllamaChatService>();

        // Register Adapter for IChatClient (used by Agents)
        services.AddSingleton<Microsoft.Extensions.AI.IChatClient, OllamaChatClientAdapter>();

        return services;
    }

    /// <summary>
    ///     Add vector search application services
    /// </summary>
    private static IServiceCollection AddVectorSearchApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Register local embedding service (used by vector search)
        services.AddSingleton<LocalEmbeddingService>();

        // Register standard vector search service
        services.AddSingleton<VectorSearchService>();

        // Register all vector search strategies and manager (from VectorSearchServiceExtensions)
        services.AddVectorSearchServices();

        // Register voicing-specific search services (from VoicingSearchServiceExtensions)
        services.AddVoicingSearchServices(configuration);

        // Register batch embedding service (used by VoicingIndexInitializationService)
        services.AddTransient<GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService, GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService>(sp =>
            new GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService(
                sp.GetRequiredService<HttpClient>(),
                configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text",
                10,
                sp.GetService<ILogger<GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService>>()));

        return services;
    }

    /// <summary>
    ///     Add chatbot services (orchestrator, knowledge source, embedding)
    /// </summary>
    private static IServiceCollection AddChatbotServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<ChatbotOptions>(
            configuration.GetSection(ChatbotOptions.SectionName));
        services.Configure<GuitarAgentOptions>(
            configuration.GetSection(GuitarAgentOptions.SectionName));

        // Register Ollama embedding service (used by knowledge source)
        services.AddSingleton<OllamaEmbeddingService>();

        // Register semantic knowledge source (bridges voicing search to chatbot)
        services.AddSingleton<SemanticKnowledgeSource>();
        services.AddSingleton<ISemanticKnowledgeSource>(sp =>
            sp.GetRequiredService<SemanticKnowledgeSource>());

        // Register chatbot session orchestrator (manages conversation flow)
        services.AddScoped<ChatbotSessionOrchestrator>();

        return services;
    }
}
