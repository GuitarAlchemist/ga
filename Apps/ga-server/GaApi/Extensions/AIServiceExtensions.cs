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

            // 3. Register the MEAI embedding generator (AI:EmbeddingProvider) and the
            //    purpose-aware IEmbeddingGeneratorFactory through the same shared registration
            //    as GaChatbot.Api, so AI:Embedding:Routing:Model (e.g. bge-large) reaches the
            //    semantic intent router together with AI:Routing:MinConfidence.
            var embeddingProvider = configuration["AI:EmbeddingProvider"] ?? "ollama";
            services.AddTextEmbeddings(embeddingProvider, configuration);

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
            // so that consumers (the Ollama IChatClient, ChatProviderAvailability) never mutate BaseAddress.
            var ollamaBaseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            services.AddHttpClient("Ollama", client =>
            {
                client.BaseAddress = new Uri(ollamaBaseUrl);
                // Cloudflare Access service-token headers when the endpoint is gated
                // (remote/CI); no-op for a header-less localhost Ollama.
                OllamaProvider.TryAddAccessHeaders(client, configuration);
            });

            var dockerBaseUrl = configuration["DockerModelRunner:BaseUrl"] ?? "http://localhost:12434/v1";
            services.AddHttpClient("DockerModelRunner", client =>
            {
                client.BaseAddress = new Uri(dockerBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            // Configure the "MistralTts" named HttpClient for Voxtral TTS
            var ttsBaseUrl = configuration["VoxtralTts:BaseUrl"] ?? "https://api.mistral.ai";
            services.AddHttpClient("MistralTts", client =>
            {
                client.BaseAddress = new Uri(ttsBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddSingleton<IVoxtralTtsService, VoxtralTtsService>();

            // Kokoro-82M TTS — local Apache 2.0 provider, ~85x smaller than Voxtral
            services.AddHttpClient<IKokoroTtsService, KokoroTtsService>();

            // A global provider gate would also block provider-free deterministic skills.
            if (configuration.GetValue<bool>("Chatbot:Readiness:ProviderCheck"))
                services.AddSingleton<IChatReadinessProbe, GaApiChatReadinessProbe>();
            services.AddSingleton<IFallbackChatHandler, GaApiFallbackChatHandler>();
            // One snapshot of the provider settings, shared by the IChatClient and the
            // availability check so status always describes what requests use.
            var selection = ChatProviderSelection.FromConfiguration(configuration);
            services.AddSingleton(selection);
            services.AddSingleton<IChatProviderAvailability, ChatProviderAvailability>();

            // Register the IChatClient every chat path uses (agents, skills, the
            // direct-chat fallback). Provider-aware so AI:ChatProvider=claude
            // routes the chatbot's main loop through
            // Anthropic (model from Anthropic:Model — defaults to Haiku 4.5 in
            // appsettings.Development.json). Anthropic costs ~$0.10–0.20 per
            // soak run vs Ollama's hosting requirements; for the public demo
            // and Phase 3 measurement Haiku 4.5 wins on tool-use reliability.
            // Falls back to Ollama for offline / cost-sensitive setups.
            //
            // EAGER (claude): build the client now, not via a Singleton lambda. The
            // lambda variant deferred CreateChatClient until first request,
            // which meant a missing ANTHROPIC_API_KEY booted cleanly,
            // /api/health passed, and the failure only surfaced as an opaque
            // 500 on the first user prompt — invisible to operators. See PR
            // #151 review (correctness corr-1, reliability rel-001). Eager
            // construction surfaces the error at startup.
            //
            // AI:CascadeProvider wraps the primary in the same CascadingChatClient as
            // GaChatbot.Api (shared GA.Business.ML registration), so e.g. mistral takes
            // over when the primary times out or is unreachable.
            if (selection.IsClaude)
            {
                var anthropicClient = GA.Providers.Anthropic.AnthropicProvider.CreateChatClient(
                    configuration,
                    configuration["Anthropic:Model"]);
                services.AddSingleton<IChatClient>(sp => WithCascadeFallback(anthropicClient, selection, configuration, sp));
            }
            else
            {
                // Local providers (Docker Model Runner, Ollama): the same function-invocation
                // pipeline as the Anthropic client, so tool-calling skills behave the same on
                // every provider. Requests stream under the hood so the transport timeout
                // bounds only the wait for the first bytes, as with the former services.
                services.AddSingleton<IChatClient>(sp =>
                    WithCascadeFallback(
                            new StreamingAggregationChatClient(CreateLocalChatClient(selection, ollamaBaseUrl, sp)),
                            selection,
                            configuration,
                            sp)
                        .AsBuilder()
                        .UseFunctionInvocation()
                        .Build(sp));
            }
        }

        private static IChatClient WithCascadeFallback(
            IChatClient primary,
            ChatProviderSelection selection,
            IConfiguration configuration,
            IServiceProvider sp) =>
            GA.Business.ML.Extensions.AiServiceExtensions.WithCascadeFallback(
                primary,
                selection.CascadeProvider,
                configuration,
                sp.GetService<ILogger<CascadingChatClient>>());

        private static IChatClient CreateLocalChatClient(ChatProviderSelection selection, string ollamaBaseUrl, IServiceProvider sp)
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("GaApi.ChatClient");
            if (selection.IsDocker)
            {
                // 60 s: the timeout of the former DockerModelRunnerChatService client.
                return DockerModelRunnerProvider.CreateChatClient(
                    selection.DockerBaseUrl, selection.DockerModel, logger, TimeSpan.FromSeconds(60));
            }

            // The named "Ollama" client carries the base address and the Cloudflare Access headers.
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama");
            httpClient.Timeout = selection.OllamaTimeout;
            return OllamaProvider.CreateChatClient(ollamaBaseUrl, selection.OllamaModel, logger, httpClient);
        }
        /// <summary>
        ///     Add vector search application services
        /// </summary>
        private void AddVectorSearchApplicationServices(IConfiguration configuration)
        {
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
            services.Configure<VoxtralTtsOptions>(
                configuration.GetSection(VoxtralTtsOptions.SectionName));
            services.Configure<KokoroTtsOptions>(
                configuration.GetSection(KokoroTtsOptions.SectionName));

            // Register Ollama embedding service (used by knowledge source)
            services.AddSingleton<OllamaEmbeddingService>();

            // OPTIC-K bridge: NL → structured intent → 112-dim compact vector.
            // Lives alongside the Ollama service so either path can be wired without disturbing
            // the other. SemanticKnowledgeSource picks the OPTK path when both are available.
            //
            // Extractor is two-tiered: deterministic typed parser first (~1 ms, $0, handles
            // structured chord+tag queries), LLM fallback only when typed finds nothing.
            services.AddSingleton<GA.Business.ML.Search.MusicalQueryEncoder>();
            services.AddSingleton<GA.Business.ML.Search.TypedMusicalQueryExtractor>();
            services.AddSingleton<GA.Business.ML.Search.LlmMusicalQueryExtractor>();
            services.AddSingleton<GA.Business.ML.Search.IMusicalQueryExtractor,
                                  GA.Business.ML.Search.CompositeMusicalQueryExtractor>();

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

            // Algedonic signal service (evaluates belief transitions → pain/pleasure signals)
            services.AddSingleton<AlgedonicSignalService>();

            // Discord bridge (stub today; swap for real bot connection when available)
            services.AddSingleton<IDiscordBridgeService, DiscordBridgeService>();

            // YouTube-to-tab pipeline services
            services.AddSingleton<VideoFrameExtractor>();
            services.AddSingleton<HandPosePipeline>();
            services.AddSingleton<PositionToTabGenerator>();

            // HandPoseClient (GA.Business.AI) — used by HandPosePipeline
            var handPoseBaseUrl = configuration["HandPoseService:BaseUrl"] ?? "http://localhost:8080";
            services.AddHttpClient("HandPose", client =>
            {
                client.BaseAddress = new Uri(handPoseBaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddSingleton<GA.Business.AI.HandPose.HandPoseClient>(sp =>
            {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var client = factory.CreateClient("HandPose");
                var hpLogger = sp.GetRequiredService<ILogger<GA.Business.AI.HandPose.HandPoseClient>>();
                return new GA.Business.AI.HandPose.HandPoseClient(client, hpLogger);
            });

        }
    }

}
