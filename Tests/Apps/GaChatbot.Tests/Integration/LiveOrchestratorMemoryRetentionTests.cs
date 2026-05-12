namespace GaChatbot.Tests.Integration;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Extensions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Extensions;
using GA.Business.ML.Search;
using GA.Infrastructure.Documentation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

/// <summary>
/// End-to-end test that proves the
/// <c>RememberThisSkill</c> → <c>OrchestratorSkillIntent</c> →
/// <c>ProductionOrchestrator</c> → <c>MemoryWriteHook</c> →
/// <see cref="MemoryStore"/> retention loop actually retains content when
/// run through the LIVE orchestrator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this test exists (PR #185 prevention):</b> the chatbot's
/// retain-on-remember story has unit tests at every layer (parser,
/// skill, hook, store), and component tests that bypass the orchestrator
/// by calling <c>MemoryWriteHook.OnResponseSent</c> with a hand-built
/// <c>ChatHookContext</c>. ALL of them passed for the entirety of the
/// 2026-05-11 chatbot-improvement session, and the chatbot's
/// remember-flow was silently broken in production the whole time —
/// <see cref="OrchestratorSkillIntent.ExecuteAsync"/> was dropping
/// <c>AgentResponse.Data</c> during the adapter map, so
/// <c>MemoryWriteHook</c>'s
/// <c>ctx.Response?.Data is MemoryWriteRequest</c> guard never matched.
/// </para>
/// <para>
/// The component-level test that should have caught this didn't,
/// because it constructed the ChatHookContext directly and skipped the
/// dispatch. A live-orchestrator e2e is the only test that exercises
/// the full payload path. This test would have failed prior to PR #185.
/// </para>
/// <para>
/// <b>[Explicit] flag:</b> the live SemanticIntentRouter requires an
/// Ollama embedding endpoint to compute intent similarity. CI agents
/// don't have Ollama, so the test is excluded from default runs.
/// Trigger manually:
/// <code>dotnet test --filter "FullyQualifiedName~LiveOrchestratorMemoryRetentionTests"</code>
/// </para>
/// <para>
/// <b>Coverage scope:</b> this test only exercises the WRITE half of the
/// memory loop. The READ half (MemoryHook's enrich-on-retrieve path)
/// has separate integration tests in
/// <c>EnrichOnRetrieveLoopTests</c> at the ML test project.
/// </para>
/// </remarks>
[TestFixture]
[Explicit(
    "Requires (a) live Ollama embedding endpoint at localhost:11434 AND " +
    "(b) the shared OrchestratorTestHarness — pending follow-up PR. The " +
    "current SetUp wires the minimum surface (IChatClient, " +
    "IEmbeddingGenerator, IGroundedNarrator, SchemaDiscoveryService, " +
    "EnhancedVoicingSearchService + IVoicingSearchStrategy + " +
    "VoicingIndexingService) but the production DI graph requires more " +
    "services (IMusicalQueryExtractor, CompositeMusicalQueryExtractor, " +
    "and their transitive deps) that no test framework wires by default. " +
    "Run produces a DI-resolution exception listing the next missing " +
    "type — that's the checklist for the harness PR. Until that lands, " +
    "OrchestratorSkillIntentDataPropagationTests at " +
    "Tests/Common/GA.Business.Core.Tests/Orchestration/ pins the " +
    "PR #185 regression at unit level.")]
public class LiveOrchestratorMemoryRetentionTests
{
    private const string EmbedEndpoint = "http://localhost:11434";

    private string _tempDir = string.Empty;
    private string _tempMemoryPath = string.Empty;
    private ServiceProvider _provider = null!;
    private MemoryStore _memoryStore = null!;
    private ProductionOrchestrator _orchestrator = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-mem-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempMemoryPath = Path.Combine(_tempDir, "memory.json");

        var configValues = new Dictionary<string, string?>
        {
            // Don't exercise retrieval — this test only proves writes.
            ["Memory:EnrichOnRetrieve"]    = "false",
            ["Memory:AllowLlmGlobalWrite"] = "false",
            ["Ollama:Endpoint"]            = EmbedEndpoint,
        };
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configValues)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddGuitarAlchemistAI();
        services.AddChatbotOrchestration();

        // ── Host-provided wiring not done by AddChatbotOrchestration ──────
        // The orchestration extension intentionally leaves a few interfaces
        // unregistered so each host (GaApi, GaChatbot, GaChatbotCli) can
        // wire its own implementation. The test mirrors GaChatbotCli's
        // setup since that's the closest to a minimal-dep harness.

        // DomainMetadataPrompter dependency.
        services.TryAddSingleton<SchemaDiscoveryService>();

        // SemanticRouter agent fallback — uses IChatClient. Tests don't
        // hit that path (RememberThis lands via the embedding intent
        // router) but DI requires the binding to construct SemanticRouter.
        services.TryAddSingleton<IChatClient>(_ =>
            new OllamaChatClient(new Uri(EmbedEndpoint), "llama3.2"));

        // Embedder for SemanticIntentRouter — this is the load-bearing
        // path: the router embeds the prompt and every intent's
        // description+examples to pick the best match. Without this the
        // test can't route to RememberThisSkill.
        services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(_ =>
            new OllamaEmbeddingGenerator(new Uri(EmbedEndpoint), "nomic-embed-text"));

        // Voicing-search stack — VoicingAgent (transitively required by
        // SemanticRouter, which constructs eagerly with ALL agents) needs
        // these even though this test never exercises voicing. CPU strategy
        // is parameterless and doesn't need a GPU or OPTK index file.
        services.TryAddSingleton<VoicingIndexingService>();
        services.TryAddSingleton<IVoicingSearchStrategy>(new CpuVoicingSearchStrategy());
        services.TryAddSingleton<EnhancedVoicingSearchService>();

        // Test-scoped MemoryStore at a temp path so the test doesn't pollute
        // the developer's real ~/.ga/memory.json. The plugin host registers
        // a default; we replace it.
        services.RemoveAll<MemoryStore>();
        services.AddSingleton(_ => new MemoryStore(_tempMemoryPath));

        // Stub IGroundedNarrator — orchestration intentionally doesn't
        // register one, hosts must provide it. RememberThisSkill is
        // deterministic; the narrator is on the fallback path which this
        // test doesn't take.
        services.AddSingleton<IGroundedNarrator>(new StubGroundedNarrator());

        _provider = services.BuildServiceProvider();
        _memoryStore = _provider.GetRequiredService<MemoryStore>();
        _orchestrator = _provider.GetRequiredService<ProductionOrchestrator>();
    }

    [TearDown]
    public void TearDown()
    {
        _provider?.Dispose();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { /* best-effort */ }
        }
    }

    [Test]
    public async Task RememberPreference_PersistsToMemoryStore_UnderSessionScope()
    {
        const string SessionId = "test-session-001";
        const string Message =
            "remember that I prefer drop-2 voicings for jazz comping";

        // Baseline: store is empty before the orchestrator runs.
        Assert.That(_memoryStore.Search(SessionId, "drop-2 voicings"), Is.Empty,
            "Test isolation broken — memory store should be empty at SetUp.");

        var response = await _orchestrator.AnswerAsync(
            new ChatRequest(Message, SessionId: SessionId));

        // Sanity: the orchestrator returned a response and routed to
        // RememberThisSkill (its routing id is "skill.rememberthis").
        Assert.That(response, Is.Not.Null);
        Assert.That(response.NaturalLanguageAnswer, Is.Not.Empty,
            "Orchestrator returned an empty answer — the skill didn't fire " +
            "and the fallback path produced nothing.");
        Assert.That(response.Routing?.AgentId, Is.EqualTo("skill.rememberthis").IgnoreCase
            .Or.EqualTo("skill.remember-this").IgnoreCase,
            $"Expected routing to land on RememberThis. Routed to: " +
            $"{response.Routing?.AgentId ?? "(null)"} at confidence " +
            $"{response.Routing?.Confidence:F2}. If this fails, the embedder " +
            $"scored a different intent higher than RememberThis — examine " +
            $"the SemanticIntentRouter trace.");

        // The critical assertion — PR #185 regression pin at the
        // FULL-PIPELINE level.
        //
        // RememberThisSkill emits AgentResponse.Data = MemoryWriteRequest.
        // OrchestratorSkillIntent.ExecuteAsync must forward that into
        // IntentResult.Data (PR #185 fix in IIntent.cs). The orchestrator
        // must populate skillRespForHooks.Data = intentResult.Data (PR
        // #185 fix in ProductionOrchestrator.cs). MemoryWriteHook's
        // OnResponseSent must then pattern-match the Data and persist.
        //
        // If ANY of those three forwards drops, the entry never lands
        // in the store and this assertion fails.
        var entries = _memoryStore.Search(SessionId, "drop-2 voicings jazz");

        Assert.That(entries, Is.Not.Empty,
            "MemoryStore.Search must find the preference. If empty, the " +
            "RememberThisSkill → MemoryWriteHook payload path is broken. " +
            "Check (in order): " +
            "(1) RememberThisSkill.ExecuteAsync emits AgentResponse.Data, " +
            "(2) OrchestratorSkillIntent.ExecuteAsync forwards response.Data " +
            "into IntentResult.Data (PR #185 fix), " +
            "(3) ProductionOrchestrator's skillRespForHooks sets " +
            "Data = intentResult.Data (PR #185 fix), " +
            "(4) MemoryWriteHook.OnResponseSent pattern-matches the request " +
            "and writes to MemoryStore.");

        var entry = entries[0];
        Assert.That(entry.SessionId, Is.EqualTo(SessionId),
            "Entry must be scoped to the caller's SessionId — the SC-001 " +
            "defense lives on this invariant.");
        Assert.That(entry.Type, Is.EqualTo("preference"),
            "RememberThisParser should classify a 'prefer drop-2' message " +
            "as a preference (the 'prefer' verb is the cue).");
        Assert.That(entry.Content, Does.Contain("drop-2").IgnoreCase,
            "Content must preserve the user's stated preference verbatim.");
    }

    [Test]
    public async Task RememberFocus_ClassifiesAsFocus_NotPreference()
    {
        const string SessionId = "test-session-focus";
        const string Message = "note: I'm working on fingerstyle technique this month";

        await _orchestrator.AnswerAsync(
            new ChatRequest(Message, SessionId: SessionId));

        var entries = _memoryStore.Search(SessionId, "fingerstyle technique");

        Assert.That(entries, Is.Not.Empty,
            "Focus-type write must land in the store. Same payload path as " +
            "preference; if this fails see the PR #185 checklist in the " +
            "preference test.");
        Assert.That(entries[0].Type, Is.EqualTo("focus"),
            "RememberThisParser should classify 'working on' / 'this month' " +
            "phrasing as a focus statement, not a preference.");
    }

    [Test]
    public async Task NonRememberMessage_DoesNotWriteToMemoryStore()
    {
        const string SessionId = "test-session-noremember";
        // A theory question that should route to a different intent —
        // MUST NOT accidentally write to memory because something
        // upstream forgot to gate on remember-phrasing.
        const string Message = "what notes are in C major?";

        await _orchestrator.AnswerAsync(
            new ChatRequest(Message, SessionId: SessionId));

        // The whole session should still be empty — no other skill emits
        // a MemoryWriteRequest, and the MemoryWriteHook refuses non-allowed
        // types. This is the negative-space coverage for the e2e.
        var (count, _) = _memoryStore.Stats(SessionId);
        Assert.That(count, Is.EqualTo(0),
            "Non-remember queries must NOT write to MemoryStore. If a " +
            "scale/chord query is landing entries here, check that " +
            "MemoryWriteHook.AllowedTypes still excludes everything except " +
            "{fact, preference, focus} AND that no other skill is " +
            "accidentally emitting a MemoryWriteRequest on AgentResponse.Data.");
    }

    private sealed class StubGroundedNarrator : IGroundedNarrator
    {
        public Task<string> NarrateAsync(string query, IReadOnlyList<CandidateVoicing> candidates) =>
            Task.FromResult($"[stub narrator] {candidates.Count} candidates for: {query}");
    }
}
