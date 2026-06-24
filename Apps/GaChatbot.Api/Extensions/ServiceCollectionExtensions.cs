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
using Microsoft.Extensions.Logging;

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

        // Fully qualified to disambiguate from the orchestration gate
        // (GA.Business.Core.Orchestration.{Abstractions.ILlmConcurrencyGate,
        // Services.LlmConcurrencyGate}) added by the IChatIntake seam. The host
        // controllers inject GaChatbot.Api's own gate; keep them on it.
        services.AddSingleton<GaChatbot.Api.Services.ILlmConcurrencyGate, GaChatbot.Api.Services.LlmConcurrencyGate>();
        services.TryAddSingleton<ConversationHistoryStore>();
        services.TryAddSingleton<RoutingContextEnricher>();
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

            // Voicing search strategy. The chatbot encodes queries with MusicalQueryEncoder
            // (124-dim OPTK compact vectors), so it must score against the matching OPTK mmap
            // index — exactly what GaApi and GaMcpServer already do successfully.
            // CpuVoicingSearchStrategy instead scores those 124-dim musical vectors against
            // per-voicing 768-dim text embeddings; the dimension mismatch makes its
            // CosineSimilarity return 0.0 for EVERY voicing, collapsing the ranking to corpus
            // insertion order and returning arbitrary wrong chords at score 0.000. Prefer the
            // dimension-clean OPTK index; fall back to CPU only when it is genuinely absent.
            services.AddSingleton<IVoicingSearchStrategy>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<EnhancedVoicingSearchService>>();
                var indexPath = ResolveOpticIndexPath(configuration);
                if (indexPath is not null)
                {
                    try
                    {
                        var strategy = new OptickSearchStrategy(indexPath);
                        logger.LogInformation("Chatbot voicing search using OPTK index at {Path}", indexPath);
                        return strategy;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex,
                            "OPTK index at {Path} failed to open; falling back to CPU voicing search", indexPath);
                    }
                }
                else
                {
                    logger.LogWarning(
                        "OPTK voicing index not found (set VoicingSearch:OpticIndexPath or " +
                        "GA_OPTICK_INDEX_PATH, or place it under state/voicings/optick.index); " +
                        "falling back to CPU voicing search — relevance scores will be degraded.");
                }

                return new CpuVoicingSearchStrategy();
            });
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

    /// <summary>
    ///     Resolves the OPTK voicing index path the same way GaMcpServer does: an explicit
    ///     <c>VoicingSearch:OpticIndexPath</c> config value or <c>GA_OPTICK_INDEX_PATH</c>
    ///     environment override first, then a walk up from the entry directory toward the
    ///     repo root looking for <c>state/voicings/optick.index</c>. Returns <see langword="null"/>
    ///     when the index is absent so the caller can degrade to CPU search instead of
    ///     throwing at host startup.
    /// </summary>
    private static string? ResolveOpticIndexPath(IConfiguration configuration)
    {
        var configured = configuration["VoicingSearch:OpticIndexPath"]
                         ?? Environment.GetEnvironmentVariable("GA_OPTICK_INDEX_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return System.IO.File.Exists(configured) ? configured : null;
        }

        // Walk up toward the repo root. Guard the walk: DirectoryInfo.Parent/FullName can
        // throw on pathological mounts (PathTooLong / Security), and this runs at host
        // startup — degrade to CPU (return null) rather than aborting boot, per the contract.
        try
        {
            for (var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
                 dir is not null;
                 dir = dir.Parent)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, "state", "voicings", "optick.index");
                if (System.IO.File.Exists(candidate))
                {
                    return candidate;
                }
            }
        }
        catch (Exception)
        {
            // fall through to null → CPU fallback
        }

        return null;
    }
}
