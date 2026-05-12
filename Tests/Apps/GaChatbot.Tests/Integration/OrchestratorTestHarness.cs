namespace GaChatbot.Tests.Integration;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Extensions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents;
using GA.Business.ML.Extensions;
using GA.Business.ML.Search;
using GA.Infrastructure.Documentation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

/// <summary>
/// Shared test harness that wires the production-shaped DI graph for
/// <c>ProductionOrchestrator</c> so integration tests can run the full
/// orchestrator dispatch without each test re-discovering the ~15
/// host-side services that <c>AddChatbotOrchestration</c> intentionally
/// leaves to the host.
/// </summary>
/// <remarks>
/// <para>
/// <b>Mirrors:</b> <c>GaChatbot.Api.Extensions.ServiceCollectionExtensions.AddMinimalChatbotApi</c>
/// in the <c>usesOrchestration</c> branch — the canonical reference for
/// what a chatbot host needs to register. When that production wiring
/// changes, the harness must follow.
/// </para>
/// <para>
/// <b>Created to unblock:</b> <see cref="LiveOrchestratorMemoryRetentionTests"/>
/// (3 tests that pin the RememberThisSkill → MemoryWriteHook → MemoryStore
/// retention loop end-to-end). Without this harness those tests were
/// <see cref="NUnit.Framework.ExplicitAttribute">[Explicit]</see>-skipped
/// and the e2e contract that would have caught the PR #185 production bug
/// was uncovered.
/// </para>
/// <para>
/// <b>External dependencies:</b> still requires a live Ollama embedding
/// endpoint at <c>localhost:11434</c> for the
/// <see cref="SemanticIntentRouter"/> dispatch to actually pick an intent.
/// Tests using the harness should keep <c>[Explicit]</c> for that reason.
/// </para>
/// </remarks>
public static class OrchestratorTestHarness
{
    /// <summary>
    /// Default Ollama endpoint. Override per-test via the
    /// <paramref name="builder"/> action if needed.
    /// </summary>
    public const string DefaultOllamaEndpoint = "http://localhost:11434";

    /// <summary>
    /// Wires the full production-shaped DI graph plus a test-isolated
    /// <see cref="GA.Business.ML.Agents.Memory.MemoryStore"/> at the path
    /// returned by <paramref name="memoryPathProvider"/>.
    /// </summary>
    /// <param name="memoryPathProvider">Returns the path that the
    /// in-process MemoryStore should persist to. Tests typically pass a
    /// per-test temp path so concurrent tests don't share storage.</param>
    /// <param name="ollamaEndpoint">Optional Ollama base URL. Defaults to
    /// <see cref="DefaultOllamaEndpoint"/>.</param>
    /// <param name="builder">Optional hook for tests to register
    /// additional services or override harness defaults BEFORE
    /// BuildServiceProvider.</param>
    /// <returns>A fully-wired <see cref="ServiceProvider"/>. Callers own
    /// disposal.</returns>
    public static ServiceProvider Build(
        Func<string>                memoryPathProvider,
        string?                     ollamaEndpoint = null,
        Action<IServiceCollection>? builder        = null)
    {
        ArgumentNullException.ThrowIfNull(memoryPathProvider);
        var endpoint = ollamaEndpoint ?? DefaultOllamaEndpoint;

        var configValues = new Dictionary<string, string?>
        {
            ["Memory:EnrichOnRetrieve"]    = "false",
            ["Memory:AllowLlmGlobalWrite"] = "false",
            ["Ollama:Endpoint"]            = endpoint,
            ["Ollama:BaseUrl"]             = endpoint,
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));

        // ── Order matters: AddGuitarAlchemistAI registers TryAdd defaults,
        //    AddChatbotOrchestration registers TryAdd defaults including the
        //    plugin host. Host-specific services that the orchestration
        //    extension intentionally leaves for the host (per its inline
        //    comments at GA.Business.Core.Orchestration/Extensions/
        //    ChatbotOrchestrationExtensions.cs:72-76) are registered AFTER
        //    so they win against any defaults.

        services.AddMemoryCache();

        // Repositories that the orchestrator's tab/progression analysis
        // services transitively depend on. In-memory variants are fine
        // for tests — production uses real corpus stores.
        services.AddSingleton<GA.Business.ML.Agents.IRoutingFeedback,
            GA.Business.ML.Agents.InMemoryRoutingFeedback>();
        services.AddSingleton<GA.Domain.Repositories.ITabCorpusRepository,
            GA.Infrastructure.Persistence.Repositories.InMemoryTabCorpusRepository>();
        services.AddSingleton<GA.Domain.Repositories.IProgressionCorpusRepository,
            GA.Infrastructure.Persistence.Repositories.InMemoryProgressionCorpusRepository>();

        // DomainMetadataPrompter dependency. Not in AddGuitarAlchemistAI
        // by design — each host registers it explicitly.
        services.AddSingleton<SchemaDiscoveryService>();

        // Voicing-search stack. VoicingAgent (transitively required by
        // SemanticRouter, which constructs eagerly with ALL agents) needs
        // these even for tests that never exercise voicing.
        services.AddSingleton<VoicingIndexingService>();
        services.AddSingleton<IVoicingSearchStrategy, CpuVoicingSearchStrategy>();
        services.AddSingleton<EnhancedVoicingSearchService>();

        // Musical-query extractors. CompositeMusicalQueryExtractor sits
        // on top of Typed + Llm; both must be registered for the
        // composite to resolve.
        services.AddSingleton<MusicalQueryEncoder>();
        services.AddSingleton<TypedMusicalQueryExtractor>();
        services.AddSingleton<LlmMusicalQueryExtractor>();
        services.AddSingleton<IMusicalQueryExtractor, CompositeMusicalQueryExtractor>();

        // Core ML services (agents, embedding generator, semantic router).
        services.AddGuitarAlchemistAI();

        // Orchestration stack — IIntent registry, hook chain, dispatch.
        services.AddChatbotOrchestration();

        // ── Host-provided abstractions ────────────────────────────────────
        // SemanticRouter fallback path requires an IChatClient; tests
        // typically don't exercise it but the binding must resolve.
        services.TryAddSingleton<IChatClient>(_ =>
            new OllamaChatClient(new Uri(endpoint), "llama3.2"));

        // SemanticIntentRouter embedder — the LOAD-BEARING path. The
        // router embeds the prompt and every intent's
        // description+examples to pick the best match. Without this
        // tests can't actually route.
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            new OllamaEmbeddingGenerator(new Uri(endpoint), "nomic-embed-text"));

        // Stub narrator. RememberThisSkill / deterministic skills don't
        // hit the narrator path; tests that do should supply a real
        // narrator via the builder action.
        services.TryAddSingleton<IGroundedNarrator>(new StubGroundedNarrator());

        // ── Test-isolated MemoryStore ────────────────────────────────────
        // Plugin host registered a default MemoryStore at ~/.ga/memory.json
        // via AddChatbotOrchestration; replace with a per-test temp path
        // so tests don't pollute the developer's real store.
        services.RemoveAll<GA.Business.ML.Agents.Memory.MemoryStore>();
        services.AddSingleton(sp =>
            new GA.Business.ML.Agents.Memory.MemoryStore(
                memoryPathProvider(),
                sp.GetService<ILogger<GA.Business.ML.Agents.Memory.MemoryStore>>()));

        // Per-test customization happens AFTER all harness defaults so
        // tests can override anything they need.
        builder?.Invoke(services);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Stub <see cref="IGroundedNarrator"/> — returns a deterministic
    /// placeholder string. Tests that exercise the narrator path must
    /// supply their own via the harness builder action.
    /// </summary>
    private sealed class StubGroundedNarrator : IGroundedNarrator
    {
        public Task<string> NarrateAsync(string query, IReadOnlyList<CandidateVoicing> candidates) =>
            Task.FromResult($"[stub narrator] {candidates.Count} candidates for: {query}");
    }
}
