namespace GA.Business.ML.Extensions;

using System;
using Embeddings;
using Embeddings.Services;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using GA.Data.MongoDB.Extensions;
using Rag;

// Alias to disambiguate from our domain's IEmbeddingGenerator
using MEAIEmbeddingGenerator = Microsoft.Extensions.AI.IEmbeddingGenerator<
    GA.Domain.Core.Instruments.Fretboard.Voicings.Search.VoicingDocument,
    Microsoft.Extensions.AI.Embedding<float>>;

using DomainEmbeddingGenerator = GA.Domain.Services.Abstractions.IEmbeddingGenerator;

/// <summary>
/// Extension methods for registering Guitar Alchemist AI services using
/// Microsoft Extensions for AI (MEAI) standard patterns.
/// </summary>
/// <remarks>
/// <para>
/// This follows the .NET 2026 foundation for GenAI as described by Jeremy Likness,
/// providing interchangeable local (Ollama) and cloud (GitHub Models, Foundry) providers.
/// </para>
/// <para>
/// The three stable contracts are:
/// <list type="bullet">
///   <item><description><see cref="IChatClient"/> - LLM chat/reasoning</description></item>
///   <item><description><see cref="MEAIEmbeddingGenerator"/> - Vector embeddings</description></item>
///   <item><description><see cref="IVectorIndex"/> - Vector storage and retrieval</description></item>
/// </list>
/// </para>
/// </remarks>
public static class AiServiceExtensions
{
    /// <summary>
    /// Adds all Guitar Alchemist AI services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGuitarAlchemistAi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ═══════════════════════════════════════════════════════════════════════
        // EMBEDDING INFRASTRUCTURE (Domain-Specific OPTIC-K)
        // ═══════════════════════════════════════════════════════════════════════
        services.AddMusicalEmbeddings();

        // ═══════════════════════════════════════════════════════════════════════
        // VECTOR INDEX
        // ═══════════════════════════════════════════════════════════════════════
        var vectorBackend = configuration.GetValue<string>("VectorStore:Backend") ?? "file";
        services.AddVectorIndex(vectorBackend, configuration);

        // ═══════════════════════════════════════════════════════════════════════
        // CHAT CLIENT (Configurable Provider)
        // ═══════════════════════════════════════════════════════════════════════
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
        services.TryAddSingleton<PhaseSphereService>();

        // Register the main generator
        services.TryAddSingleton<MusicalEmbeddingGenerator>();

        // Register legacy domain interface for backward compatibility
        services.TryAddSingleton<DomainEmbeddingGenerator>(sp => sp.GetRequiredService<MusicalEmbeddingGenerator>());

        // Register MEAI bridge for standard AI ecosystem compatibility
        services.TryAddSingleton<MEAIEmbeddingGenerator, MusicalEmbeddingBridge>();
        services.TryAddSingleton<MusicalEmbeddingBridge>();

        return services;
    }

    /// <summary>
    /// Adds a vector index implementation based on the specified backend.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="backend">The backend type: "file", "qdrant", or "memory".</param>
    /// <param name="configuration">The configuration instance.</param>
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
                    var dimension = configuration.GetValue<ulong>("VectorStore:Qdrant:Dimension", 216);
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

        return services;
    }

    /// <summary>
    /// Adds an <see cref="IChatClient"/> based on the specified provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="provider">The provider: "ollama", "github", or "openai".</param>
    /// <param name="configuration">The configuration instance.</param>
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

            case "github":
                // GitHub Models uses Azure AI Inference endpoint with GITHUB_TOKEN
                if (!Providers.GitHubModelsProvider.IsAvailable())
                {
                    throw new InvalidOperationException(
                        "GitHub Models requires the GITHUB_TOKEN environment variable. " +
                        "Create a GitHub Personal Access Token and set it as GITHUB_TOKEN.");
                }
                services.TryAddSingleton<IChatClient>(_ =>
                    Providers.GitHubModelsProvider.CreateChatClientFromConfig(configuration));
                break;

            case "openai":
                // OpenAI integration deferred - SDK version mismatch with MEAI.OpenAI package
                throw new NotImplementedException(
                    "OpenAI provider is deferred until Microsoft.Extensions.AI.OpenAI package " +
                    "version stabilizes. Use 'ollama' provider for local development.");

            default:
                throw new ArgumentException($"Unknown chat provider: {provider}", nameof(provider));
        }

        return services;
    }

    /// <summary>
    /// Adds a text embedding generator based on the specified provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="provider">The provider: "ollama", "github", or "openai".</param>
    /// <param name="configuration">The configuration instance.</param>
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

            case "github":
                if (!Providers.GitHubModelsProvider.IsAvailable())
                {
                    throw new InvalidOperationException(
                        "GitHub Models requires the GITHUB_TOKEN environment variable.");
                }
                services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
                    Providers.GitHubModelsProvider.CreateEmbeddingGeneratorFromConfig(configuration));
                break;

            case "openai":
                // OpenAI integration deferred - SDK version mismatch with MEAI.OpenAI package
                throw new NotImplementedException(
                    "OpenAI embedding provider is deferred until Microsoft.Extensions.AI.OpenAI package " +
                    "version stabilizes. Use 'ollama' provider for local development.");

            default:
                throw new ArgumentException($"Unknown embedding provider: {provider}", nameof(provider));
        }

        return services;
    }

    /// <summary>
    /// Adds the hybrid embedding service that supports both musical and text embeddings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="textProvider">The text embedding provider: "ollama", "github", or "openai".</param>
    /// <param name="configuration">The configuration instance.</param>
    public static IServiceCollection AddHybridEmbeddings(
        this IServiceCollection services,
        string textProvider,
        IConfiguration configuration)
    {
        // Ensure musical embeddings are registered
        services.AddMusicalEmbeddings();

        // Add text embeddings
        services.AddTextEmbeddings(textProvider, configuration);

        // Register hybrid service
        services.TryAddSingleton<Providers.HybridEmbeddingService>();

        return services;
    }

    /// <summary>
    /// Adds all Guitar Alchemist specialized agents to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGuitarAlchemistAgents(this IServiceCollection services)
    {
        // Register individual agents
        services.TryAddSingleton<Agents.TabAgent>();
        services.TryAddSingleton<Agents.TheoryAgent>();
        services.TryAddSingleton<Agents.TechniqueAgent>();
        services.TryAddSingleton<Agents.ComposerAgent>();
        services.TryAddSingleton<Agents.CriticAgent>();

        // Register as collection for the router
        services.TryAddSingleton<IEnumerable<Agents.GuitarAlchemistAgentBase>>(sp => new Agents.GuitarAlchemistAgentBase[]
        {
            sp.GetRequiredService<Agents.TabAgent>(),
            sp.GetRequiredService<Agents.TheoryAgent>(),
            sp.GetRequiredService<Agents.TechniqueAgent>(),
            sp.GetRequiredService<Agents.ComposerAgent>(),
            sp.GetRequiredService<Agents.CriticAgent>()
        });

        // Register the semantic router
        services.TryAddSingleton<Agents.SemanticRouter>();

        return services;
    }

    /// <summary>
    /// Adds partitioned RAG services for multi-backend knowledge retrieval.
    /// </summary>
    public static IServiceCollection AddPartitionedRag(this IServiceCollection services)
    {
        // Specialized MongoDB RAG collections
        services.AddRagServices();

        // Orchestration and evaluation services
        services.TryAddSingleton<IPartitionedRagService, PartitionedRagService>();
        services.TryAddSingleton<PartitionedRagService>();
        services.TryAddSingleton<RagEvaluationService>();

        return services;
    }

    /// <summary>
    /// Adds the complete Guitar Alchemist AI stack including embeddings, agents, and routing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGuitarAlchemistFullStack(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Core AI services
        services.AddGuitarAlchemistAi(configuration);

        // Text embeddings for semantic routing
        var embeddingProvider = configuration.GetValue<string>("AI:EmbeddingProvider") ?? "ollama";
        services.AddTextEmbeddings(embeddingProvider, configuration);

        // All agents
        services.AddGuitarAlchemistAgents();

        // Partitioned RAG
        services.AddPartitionedRag();

        return services;
    }
}
