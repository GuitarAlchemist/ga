namespace GaApi.Extensions;

using Configuration;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Extensions;
using GA.Business.Core.Orchestration.Services;
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
    extension(IServiceCollection services)
    {
        /// <summary>
        ///     Add all AI and ML services to the service collection
        /// </summary>
        public IServiceCollection AddAiServices(IConfiguration configuration)
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

            // 3. Register MEAI Embedding Generator (used by SemanticRouter) — respects AI:EmbeddingProvider
            var embeddingProvider = configuration["AI:EmbeddingProvider"] ?? "ollama";
            services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(sp =>
                string.Equals(embeddingProvider, "docker", StringComparison.OrdinalIgnoreCase)
                    ? DockerModelRunnerProvider.CreateEmbeddingGeneratorFromConfig(configuration, sp.GetService<ILogger<DockerModelRunnerChatService>>())
                    : OllamaProvider.CreateEmbeddingGeneratorFromConfig(configuration, sp.GetService<ILogger<OllamaEmbeddingGenerator>>()));

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
        private void AddLlmServices(IConfiguration configuration)
        {
            // Configure the "Ollama" named HttpClient with BaseAddress and timeout here
            // so that OllamaChatService (singleton) does not mutate BaseAddress after construction.
            var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            services.AddHttpClient("Ollama", client =>
            {
                client.BaseAddress = new Uri(ollamaBaseUrl);
            });

            var dockerBaseUrl = configuration["DockerModelRunner:BaseUrl"] ?? "http://localhost:12434/v1";
            services.AddHttpClient("DockerModelRunner", client =>
            {
                client.BaseAddress = new Uri(dockerBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            var chatProvider = configuration["AI:ChatProvider"] ?? "ollama";

            if (string.Equals(chatProvider, "claude", StringComparison.OrdinalIgnoreCase))
                services.AddSingleton<IChatService, ClaudeChatService>();
            else if (string.Equals(chatProvider, "docker", StringComparison.OrdinalIgnoreCase))
                services.AddSingleton<IChatService, DockerModelRunnerChatService>();
            else
                services.AddSingleton<IChatService, OllamaChatService>();

            // Register Adapter for IChatClient (used by Agents)
            services.AddSingleton<IChatClient, OllamaChatClientAdapter>();
        }
        /// <summary>
        ///     Add vector search application services
        /// </summary>
        private void AddVectorSearchApplicationServices(IConfiguration configuration)
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
            // BaseAddress is already configured by the "Ollama" named HttpClient registration above.
            services.AddTransient<GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService, GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("Ollama");
                return new GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService(
                    client,
                    configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text",
                    10,
                    sp.GetService<ILogger<GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService>>());
            });
        }
        /// <summary>
        ///     Add chatbot services (orchestrator, knowledge source, embedding)
        /// </summary>
        private void AddChatbotServices(IConfiguration configuration)
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

            // Register shared agentic orchestration stack (ProductionOrchestrator → SemanticRouter → agents)
            services.AddChatbotOrchestration();

            // GaApi narrator: OllamaGroundedNarrator (reads Ollama:Endpoint from config)
            services.AddScoped<IGroundedNarrator, OllamaGroundedNarrator>();

        }
    }

}
