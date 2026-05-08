namespace GaChatbot.Api.Extensions;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Extensions;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents;
using GA.Business.ML.Extensions;
using GA.Business.ML.Search;
using GA.Domain.Repositories;
using GA.Infrastructure.Documentation;
using GA.Infrastructure.Persistence.Repositories;
using GaChatbot.Api.Services;
using Microsoft.Extensions.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMinimalChatbotApi(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var chatbotMode = (configuration["Chatbot:Mode"] ?? "direct").ToLowerInvariant();
        var chatProvider = configuration.GetValue<string>("AI:ChatProvider") ?? "ollama";
        var embeddingProvider = configuration.GetValue<string>("AI:EmbeddingProvider") ?? "ollama";
        var usesOrchestration = chatbotMode is "full" or "orchestrated";

        services.AddSingleton<ILlmConcurrencyGate, LlmConcurrencyGate>();
        services.TryAddSingleton<ConversationHistoryStore>();
        services.AddSingleton<IChatProviderReadinessProbe, ChatProviderReadinessProbe>();
        services.AddScoped<DirectChatApplicationService>();
        services.AddScoped<RoutedChatApplicationService>();
        services.AddSingleton<LightweightChatRouter>();
        services.AddSingleton<LightweightTheorySanityChecker>();
        // Fully qualified to disambiguate from
        // GA.Business.Core.Orchestration.Abstractions.IChatApplicationService
        // (the host-neutral surface added 2026-05-07 for GaApi controllers).
        // GaChatbot.Api keeps its own richer interface (Trace, readiness,
        // ChatExecutionResult) — codex C-prime guidance is to keep this host
        // frozen until a concrete deploy reason emerges, not to merge contracts.
        services.AddScoped<GaChatbot.Api.Services.IChatApplicationService>(sp =>
            usesOrchestration
                ? sp.GetRequiredService<OrchestratedChatApplicationService>()
                : chatbotMode == "routed"
                    ? sp.GetRequiredService<RoutedChatApplicationService>()
                    : sp.GetRequiredService<DirectChatApplicationService>());

        var ollamaBaseUrl = configuration["Ollama:BaseUrl"]
            ?? configuration["Ollama:Endpoint"]
            ?? "http://localhost:11434";

        services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        if (usesOrchestration)
        {
            services.AddSingleton<IRoutingFeedback, InMemoryRoutingFeedback>();
            services.AddSingleton<ITabCorpusRepository, InMemoryTabCorpusRepository>();
            services.AddSingleton<IProgressionCorpusRepository, InMemoryProgressionCorpusRepository>();
            services.AddSingleton<SchemaDiscoveryService>();
            services.AddSingleton<VoicingIndexingService>();
            services.AddSingleton<IVoicingSearchStrategy, CpuVoicingSearchStrategy>();
            services.AddSingleton<EnhancedVoicingSearchService>();
            services.AddMemoryCache();
            services.AddSingleton<MusicalQueryEncoder>();
            services.AddSingleton<TypedMusicalQueryExtractor>();
            services.AddSingleton<LlmMusicalQueryExtractor>();
            services.AddSingleton<IMusicalQueryExtractor, CompositeMusicalQueryExtractor>();

            services.AddGuitarAlchemistAi(configuration);
            services.AddTextEmbeddings(embeddingProvider, configuration);
            services.AddGuitarAlchemistAI();
            services.AddChatbotOrchestration();
            services.TryAddSingleton<IGroundedNarrator, ChatClientGroundedNarrator>();
            services.AddScoped<IProductionChatOrchestratorClient, ProductionChatOrchestratorClient>();
            services.AddScoped<OrchestratedChatApplicationService>();
            services.AddHostedService<VoicingSearchWarmupService>();
        }
        else
        {
            services.AddGuitarAlchemistChatClient(chatProvider, configuration);
        }

        return services;
    }
}
