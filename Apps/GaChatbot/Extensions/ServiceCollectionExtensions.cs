namespace GaChatbot.Extensions;

using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Extensions;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Musical.Enrichment;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;
using GA.Domain.Core.Instruments;
using GA.Domain.Services.Abstractions;
using GA.Domain.Services.Fretboard.Analysis;
using GA.Domain.Core.Design.Schema;
using GaChatbot.Abstractions;
using GaChatbot.Services;

/// <summary>
/// Extension methods for registering GaChatbot services.
/// Consolidates all service registrations for maintainability.
/// </summary>
public static class GaChatbotServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers all GaChatbot services including orchestration, anti-hallucination guardrails,
        /// and domain services.
        /// </summary>
        public IServiceCollection AddGaChatbotServices()
        {
            // ---- HTTP Client Factory (for Ollama calls — avoids static HttpClient anti-pattern) ----
            services.AddHttpClient("ollama", (sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var endpointRaw = config["Ollama:Endpoint"] ?? "http://localhost:11434";

                // Validate the endpoint is a well-formed absolute URI with http/https scheme
                if (!Uri.TryCreate(endpointRaw, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new InvalidOperationException(
                        $"Ollama:Endpoint '{endpointRaw}' is not a valid http/https URI. " +
                        "Ensure appsettings.json or the OLLAMA_HOST environment variable is set correctly.");
                }

                client.BaseAddress = uri;
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            // ---- Core AI/ML Services ----
            services.AddGuitarAlchemistAI();
            services.AddGuitarAlchemistAgents();
            services.AddSingleton<IVectorIndex, GaChatbot.Services.InMemoryVectorIndex>();

            // ---- Phase Sphere & Modal Services ----
            services.AddSingleton<PhaseSphereService>();
            services.AddSingleton<AutoTaggingService>();
            services.AddSingleton<ModalFlavorService>();

            // ---- Spectral Retrieval ----
            services.AddScoped<ISpectralRetrievalService, SpectralRetrievalService>();
            services.AddScoped<SpectralRetrievalService>();

            // ---- Anti-Hallucination Guardrails (Phase 5.2.5) ----
            services.AddSingleton<SchemaDiscoveryService>();
            services.AddSingleton<DomainMetadataPrompter>();
            services.AddSingleton<QueryUnderstandingService>();
            services.AddSingleton<GroundedPromptBuilder>();
            services.AddSingleton<ResponseValidator>();

            // ---- LLM Provider (Microsoft.Extensions.AI - 2026 Pattern) ----
            // Configure via GA_AI_PROVIDER environment variable:
            // - "ollama" (default): Local Ollama instance
            // - "github": GitHub Models (free tier, requires GITHUB_TOKEN)
            // - "openai": OpenAI API (requires OPENAI_API_KEY)
            var aiProvider = Environment.GetEnvironmentVariable("GA_AI_PROVIDER")?.ToLowerInvariant() ?? "ollama";

            switch (aiProvider)
            {
                case "github":
                    services.AddGitHubModelsChatClient(
                        modelId: Environment.GetEnvironmentVariable("GA_AI_MODEL") ?? "gpt-4o-mini");
                    break;
                case "openai":
                    var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                        ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable required for OpenAI provider");
                    services.AddOpenAIChatClient(
                        modelId: Environment.GetEnvironmentVariable("GA_AI_MODEL") ?? "gpt-4o-mini",
                        apiKey: openAiKey);
                    break;
                default: // "ollama"
                    services.AddOllamaAIChatClient(
                        modelId: Environment.GetEnvironmentVariable("GA_AI_MODEL") ?? "llama3.2",
                        endpoint: Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://localhost:11434");
                    break;
            }

            services.AddExtensionsAINarrator();

            // ---- Domain Services ----
            services.AddSingleton<Tuning>(Tuning.Default);
            services.AddSingleton<FretboardPositionMapper>();
            services.AddSingleton<IMlNaturalnessRanker, GA.Business.ML.Naturalness.MlNaturalnessRanker>();
            services.AddSingleton<PhysicalCostService>();
            services.AddSingleton<IEmbeddingGenerator, MusicalEmbeddingGenerator>();

            // ---- Retrieval & Suggestion Services ----
            services.AddSingleton<StyleProfileService>();
            services.AddSingleton<NextChordSuggestionService>();
            services.AddSingleton<ModulationAnalyzer>();

            // ---- Tab Services ----
            services.AddSingleton<AdvancedTabSolver>();
            services.AddSingleton<AlternativeFingeringService>();
            services.AddSingleton<TabPresentationService>();

            // ---- Orchestrators ----
            services.AddSingleton<SpectralRagOrchestrator>();
            services.AddSingleton<TabAwareOrchestrator>();
            services.AddSingleton<ProductionOrchestrator>();

            return services;
        }
    }
}
