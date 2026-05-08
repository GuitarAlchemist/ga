namespace GA.Business.Core.Orchestration.Extensions;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Clients;
using GA.Business.Core.Orchestration.Intents;
using GA.Business.Core.Orchestration.Services;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Agents.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

/// <summary>
/// DI registration for the chatbot orchestration stack.
/// Call <see cref="AddChatbotOrchestration"/> in both GaChatbot and GaApi.
/// </summary>
public static class ChatbotOrchestrationExtensions
{
    /// <summary>
    /// Registers all chatbot orchestration services.
    /// Skills, hooks, and domain services are discovered automatically via
    /// <see cref="ChatPluginHost.AddChatPluginHost"/> — no manual skill registration needed.
    /// </summary>
    public static IServiceCollection AddChatbotOrchestration(this IServiceCollection services)
    {
        // ── F# closure registry bootstrap ────────────────────────────────────
        // F# module do-bindings are lazy: GA.Business.DSL.Closures.BuiltinClosures.*
        // do NOT register themselves until something inside that module is
        // touched. ga_dsl_eval queries GaClosureRegistry.Global at request time,
        // so without an explicit init() call here the registry is empty in the
        // running app, and Path B SKILL.md skills (transpose / common-tones /
        // diatonic-chords) get "closure not exposed" responses from the LLM.
        // The init is idempotent (RegisterAll is no-op on re-entry) so calling
        // it from every host that wires up the orchestration stack is safe.
        // Diagnosed 2026-05-07 via codex CLI second-opinion review.
        GA.Business.DSL.GaClosureBootstrap.init();

        // HTTP client for Ollama — config resolved lazily at first use
        services.AddHttpClient("ollama", (sp, client) =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var endpointRaw = configuration["Ollama:Endpoint"] ?? "http://localhost:11434";
            var timeoutSeconds = Math.Max(30, configuration.GetValue("Ollama:GenerateTimeoutSeconds", 180));
            if (!Uri.TryCreate(endpointRaw, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"Ollama:Endpoint '{endpointRaw}' is not a valid http/https URI.");
            }

            client.BaseAddress = uri;
            client.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        });

        // Vector store — TryAdd so GaApi's FileBasedVectorIndex takes precedence if already registered
        services.TryAddSingleton<GA.Business.ML.Embeddings.IVectorIndex, GA.Business.ML.Embeddings.InMemoryVectorIndex>();

        // Shared Ollama HTTP client wrapper — single plumbing point for /api/generate calls
        services.AddSingleton<OllamaGenerateClient>();

        // Core stateless services (Singleton — no scoped dependencies)
        services.AddSingleton<DomainMetadataPrompter>();
        services.AddSingleton<GroundedPromptBuilder>();
        services.AddSingleton<ResponseValidator>();
        services.AddSingleton<QueryUnderstandingService>();
        services.AddSingleton<TabPresentationService>();
        services.AddSingleton<IAlgebraPromptClassifier, KeywordAlgebraPromptClassifier>();
        services.AddSingleton<IIxAlgebraService, IxAlgebraService>();
        // NOTE: IGroundedNarrator is intentionally NOT registered here.
        // Each consuming app must register its own narrator implementation:
        //   - GaApi: services.AddScoped<IGroundedNarrator, OllamaGroundedNarrator>();
        //   - GaChatbot: services.AddExtensionsAINarrator();

        // Plugin host — discovers [ChatPlugin] implementations in loaded assemblies
        // and registers their skills, hooks, and domain services automatically.
        // GaPlugin (in GA.Business.Core.Orchestration) registers:
        //   Skills: ScaleInfoSkill, FretSpanSkill, ChordSubstitutionSkill, KeyIdentificationSkill
        //   Hooks:  PromptSanitizationHook, ObservabilityHook
        services.AddChatPluginHost();

        // Conversation history — singleton in-memory store shared across scopes
        services.AddSingleton<ConversationHistoryStore>();

        // Cross-agent delegation coordinator
        services.AddScoped<IAgentCoordinator, AgentCoordinator>();

        // ── Semantic intent routing (replaces ad-hoc string classifiers) ──────
        // Per docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md
        // §"Routing classifiers": embedding-similarity dispatch for the four
        // routing surfaces that previously used keyword regex —
        // KeywordAlgebraPromptClassifier, IsAskingForOptimization, the per-skill
        // CanHandle foreach, and the tab-analysis branch.
        services.AddSingleton<SemanticIntentRouter>();
        services.AddHostedService<IntentEmbeddingWarmupService>();
        services.AddHostedService<ClosureRegistryStartupCheck>();
        services.AddScoped<TabAnalysisOrchestrationService>();

        services.AddSingleton<AlgebraIntent>();
        services.AddSingleton<IIntent>(sp => sp.GetRequiredService<AlgebraIntent>());

        services.AddScoped<TabOptimizeIntent>();
        services.AddScoped<IIntent>(sp => sp.GetRequiredService<TabOptimizeIntent>());

        services.AddScoped<TabAnalyzeIntent>();
        services.AddScoped<IIntent>(sp => sp.GetRequiredService<TabAnalyzeIntent>());

        // Voicing as a first-class IIntent so the embedding router can
        // dispatch semantic variants ("chord shapes for Am7") alongside the
        // explicit regex guard's keyword path ("Drop 2 voicings of Cmaj7").
        // Codex CLI 2026-05-07 follow-up to the dispatch-order fix
        // (a9220957) — roadmap P1 #6 follow-up. Lifetime is Scoped to match
        // VoicingAgent's transitive dependencies (IChatClient is Scoped).
        services.AddScoped<VoicingIntent>();
        services.AddScoped<IIntent>(sp => sp.GetRequiredService<VoicingIntent>());

        // Orchestrators — Scoped because they transitively depend on
        // SemanticRouter (Scoped) and SpectralRetrievalService (Scoped).
        services.AddScoped<SpectralRagOrchestrator>();
        services.AddScoped<TabAwareOrchestrator>();
        services.AddScoped<ProductionOrchestrator>();

        // Register as the standard interface so callers can depend on IHarmonicChatOrchestrator
        services.AddScoped<IHarmonicChatOrchestrator>(sp =>
            sp.GetRequiredService<ProductionOrchestrator>());

        // Host-neutral chat application service — the single canonical
        // chat entry point that GaApi controllers / hubs (and any future
        // host) take a dependency on. Codex CLI second-opinion
        // recommendation 2026-05-07 (roadmap P0 #2). Distinct from
        // GaChatbot.Api's heavier IChatApplicationService which adds
        // readiness probing, fallback, and AgenticTrace assembly.

        // Inner: the bare HarmonicChatApplicationService is registered as a
        // concrete type only (not the IChatApplicationService binding) so the
        // decorator stack below is the canonical IChatApplicationService.
        services.AddScoped<HarmonicChatApplicationService>();

        // Per-request trace capture. Scoped lifetime — every chat request
        // gets its own AgenticTrace; concurrent requests don't interleave.
        // The decorator stack writes steps; hosts read the captured trace
        // at the wire boundary (GaApi ChatJsonResponse.Trace, SignalR
        // routing payload, AG-UI custom events). Roadmap P1 #7 commit 1.
        services.AddScoped<IAgenticTraceCapture, AgenticTraceCapture>();

        // Default permissive readiness probe; hosts can override to gain
        // real gating. TryAdd so a host-supplied IChatReadinessProbe wins.
        // Roadmap P1 #7 commit 2.
        services.TryAddSingleton<IChatReadinessProbe, PermissiveChatReadinessProbe>();

        // Default no-op fallback handler; hosts that want real fallback
        // (e.g. direct chat to Ollama) override the binding. Behavior is
        // additionally gated by FallbackOptions.Enabled, default false —
        // codex CLI 2026-05-08 explicit call: "Fallback should be config-
        // gated off by default … until tests prove deterministic-skill
        // failures cannot be papered over." Roadmap P1 #7 commit 3.
        services.TryAddSingleton<IFallbackChatHandler, NoOpFallbackChatHandler>();
        services.AddOptions<FallbackOptions>()
            .BindConfiguration(FallbackOptions.SectionName);

        // Decorator stack (innermost → outermost):
        //   Harmonic  →  Traceable  →  Fallback  →  ReadinessGated
        //
        // - Trace innermost so its coverage extends to every outer layer.
        // - Fallback inside ReadinessGated so a readiness-blocked request
        //   short-circuits without invoking fallback (no point).
        // - Fallback outside Traceable so the trace step records the
        //   orchestrator's actual result before the fallback decision.
        // Codex CLI 2026-05-08 design review (roadmap P1 #7).
        services.AddScoped<IChatApplicationService>(sp =>
        {
            var capture   = sp.GetRequiredService<IAgenticTraceCapture>();
            var harmonic  = sp.GetRequiredService<HarmonicChatApplicationService>();
            var traceable = new TraceableChatApplicationService(harmonic, capture);
            var fallback  = new FallbackChatApplicationService(
                traceable,
                sp.GetRequiredService<IFallbackChatHandler>(),
                sp.GetRequiredService<IOptions<FallbackOptions>>(),
                capture);
            var probe     = sp.GetRequiredService<IChatReadinessProbe>();
            return new ReadinessGatedChatApplicationService(fallback, probe, capture);
        });

        return services;
    }
}
