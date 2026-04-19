namespace GA.Business.ML.Extensions;

using Rag;
using Rag.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Embeddings.Services;
using Embeddings;
using GA.Data.MongoDB.Extensions;

// Alias to disambiguate from our domain's IEmbeddingGenerator
using MEAIEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<
    ChordVoicingRagDocument,
    Microsoft.Extensions.AI.Embedding<float>>;

/// <summary>
/// Extension methods for registering Guitar Alchemist AI services using
/// Microsoft Extensions for AI (MEAI) standard patterns.
/// </summary>
public static class AiServiceExtensions
{
    /// <summary>
    /// Adds all Guitar Alchemist AI services to the service collection.
    /// </summary>
    public static IServiceCollection AddGuitarAlchemistAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // EMBEDDING INFRASTRUCTURE (Domain-Specific OPTIC-K)
        services.AddMusicalEmbeddings();

        // VECTOR INDEX
        var vectorBackend = configuration.GetValue<string>("VectorStore:Backend") ?? "file";
        services.AddVectorIndex(vectorBackend, configuration);

        // CHAT CLIENT (Configurable Provider)
        var chatProvider = configuration.GetValue<string>("AI:ChatProvider") ?? "ollama";
        services.AddGuitarAlchemistChatClient(chatProvider, configuration);

        return services;
    }

    /// <summary>
    /// Adds the OPTIC-K musical embedding generator and its MEAI bridge.
    /// </summary>
    public static IServiceCollection AddMusicalEmbeddings(this IServiceCollection services)
    {
        // Register all embedding sub-services
        services.TryAddSingleton<IdentityVectorService>();
        services.TryAddSingleton<TheoryVectorService>();
        services.TryAddSingleton<MorphologyVectorService>();
        services.TryAddSingleton<ContextVectorService>();
        services.TryAddSingleton<SymbolicVectorService>();
        services.TryAddSingleton<ModalVectorService>();
        services.TryAddSingleton<RootVectorService>();
        services.TryAddSingleton<PhaseSphereService>();

        // Register the main generator
        services.TryAddSingleton<MusicalEmbeddingGenerator>();

        // Register MEAI bridge for standard AI ecosystem compatibility
        services.TryAddSingleton<MEAIEmbeddingGenerator, MusicalEmbeddingBridge>();
        services.TryAddSingleton<MusicalEmbeddingBridge>();

        return services;
    }

    /// <summary>
    /// Adds a vector index implementation based on the specified backend.
    /// </summary>
    public static IServiceCollection AddVectorIndex(
        this IServiceCollection services,
        string backend,
        IConfiguration configuration)
    {
        switch (backend.ToLowerInvariant())
        {
            case "qdrant":
                services.TryAddSingleton<IVectorIndex>(_ =>
                {
                    var host = configuration.GetValue<string>("VectorStore:Qdrant:Host") ?? "localhost";
                    var port = configuration.GetValue<int>("VectorStore:Qdrant:Port", 6334);
                    var dimension = configuration.GetValue<ulong>("VectorStore:Qdrant:Dimension", 228);
                    return new QdrantVectorIndex(host, port, dimension);
                });
                break;

            case "memory":
                services.TryAddSingleton<IVectorIndex, InMemoryVectorIndex>();
                break;

            case "file":
            default:
                services.TryAddSingleton<IVectorIndex>(_ =>
                {
                    var path = configuration.GetValue<string>("VectorStore:File:Path")
                               ?? "voicing_index.json";
                    return new FileBasedVectorIndex(path);
                });
                break;
        }

        // Register the MEAI VectorStore adapter
        services.TryAddSingleton<Microsoft.Extensions.VectorData.VectorStore, GA.Business.ML.Embeddings.VectorStoreAdapter>();

        return services;
    }

    /// <summary>
    /// Adds an <see cref="IChatClient"/> based on the specified provider.
    /// </summary>
    public static IServiceCollection AddGuitarAlchemistChatClient(
        this IServiceCollection services,
        string provider,
        IConfiguration configuration)
    {
        switch (provider.ToLowerInvariant())
        {
            case "ollama":
                services.TryAddSingleton<IChatClient>(_ =>
                    Providers.OllamaProvider.CreateChatClientFromConfig(configuration));
                break;

            case "docker":
                services.TryAddSingleton<IChatClient>(_ =>
                    Providers.DockerModelRunnerProvider.CreateChatClientFromConfig(configuration));
                break;

            case "github":
                if (!Providers.GitHubModelsProvider.IsAvailable())
                {
                    throw new InvalidOperationException(
                        "GitHub Models requires the GITHUB_TOKEN environment variable.");
                }
                services.TryAddSingleton<IChatClient>(_ =>
                    Providers.GitHubModelsProvider.CreateChatClientFromConfig(configuration));
                break;

            default:
                throw new ArgumentException($"Unknown chat provider: {provider}", nameof(provider));
        }

        return services;
    }

    /// <summary>
    /// Adds a text embedding generator based on the specified provider.
    /// </summary>
    public static IServiceCollection AddTextEmbeddings(
        this IServiceCollection services,
        string provider,
        IConfiguration configuration)
    {
        switch (provider.ToLowerInvariant())
        {
            case "ollama":
                services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
                    Providers.OllamaProvider.CreateEmbeddingGeneratorFromConfig(configuration));
                break;

            case "docker":
                services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
                    Providers.DockerModelRunnerProvider.CreateEmbeddingGeneratorFromConfig(configuration));
                break;

            case "github":
                if (!Providers.GitHubModelsProvider.IsAvailable())
                {
                    throw new InvalidOperationException(
                        "GitHub Models requires the GITHUB_TOKEN environment variable.");
                }
                services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
                    Providers.GitHubModelsProvider.CreateEmbeddingGeneratorFromConfig(configuration));
                break;

            default:
                throw new ArgumentException($"Unknown embedding provider: {provider}", nameof(provider));
        }

        return services;
    }

    /// <summary>
    /// Adds the hybrid embedding service that supports both musical and text embeddings.
    /// </summary>
    public static IServiceCollection AddHybridEmbeddings(
        this IServiceCollection services,
        string textProvider,
        IConfiguration configuration)
    {
        services.AddMusicalEmbeddings();
        services.AddTextEmbeddings(textProvider, configuration);
        services.TryAddSingleton<Providers.HybridEmbeddingService>();
        return services;
    }

    /// <summary>
    /// Adds all Guitar Alchemist specialized agents to the service collection.
    /// </summary>
    public static IServiceCollection AddGuitarAlchemistAgents(this IServiceCollection services)
    {
        services.TryAddSingleton<Agents.TabAgent>();
        services.TryAddSingleton<Agents.TheoryAgent>();
        services.TryAddSingleton<Agents.TechniqueAgent>();
        services.TryAddSingleton<Agents.ComposerAgent>();
        services.TryAddSingleton<Agents.CriticAgent>();
        services.TryAddSingleton<Agents.VoicingAgent>();

        services.TryAddSingleton<IEnumerable<Agents.GuitarAlchemistAgentBase>>(sp => new Agents.GuitarAlchemistAgentBase[]
        {
            sp.GetRequiredService<Agents.TabAgent>(),
            sp.GetRequiredService<Agents.TheoryAgent>(),
            sp.GetRequiredService<Agents.TechniqueAgent>(),
            sp.GetRequiredService<Agents.ComposerAgent>(),
            sp.GetRequiredService<Agents.CriticAgent>(),
            sp.GetRequiredService<Agents.VoicingAgent>()
        });

        services.TryAddSingleton<Agents.SemanticRouter>();
        services.TryAddSingleton<Agents.IRoutingFeedback, Agents.MongoRoutingFeedback>();

        return services;
    }

    /// <summary>
    /// Adds partitioned RAG services for multi-backend knowledge retrieval.
    /// </summary>
    public static IServiceCollection AddPartitionedRag(this IServiceCollection services)
    {
        services.AddRagServices();
        services.TryAddSingleton<IPartitionedRagService, PartitionedRagService>();
        services.TryAddSingleton<PartitionedRagService>();
        services.TryAddSingleton<RagEvaluationService>();

        return services;
    }

    /// <summary>
    /// Adds the complete Guitar Alchemist AI stack including embeddings, agents, and routing.
    /// </summary>
    public static IServiceCollection AddGuitarAlchemistFullStack(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddGuitarAlchemistAi(configuration);
        var embeddingProvider = configuration.GetValue<string>("AI:EmbeddingProvider") ?? "ollama";
        services.AddTextEmbeddings(embeddingProvider, configuration);
        services.AddGuitarAlchemistAgents();
        services.AddPartitionedRag();

        return services;
    }
}
