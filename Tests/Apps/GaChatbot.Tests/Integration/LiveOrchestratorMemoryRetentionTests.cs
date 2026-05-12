namespace GaChatbot.Tests.Integration;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents.Memory;
using Microsoft.Extensions.DependencyInjection;
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
/// <c>OrchestratorSkillIntent.ExecuteAsync</c> was dropping
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
    "Requires a live Ollama embedding endpoint at localhost:11434 for the " +
    "SemanticIntentRouter to actually pick an intent. CI agents don't have " +
    "Ollama wired, so the fixture is excluded from default runs. Trigger " +
    "manually with `dotnet test --filter " +
    "\"FullyQualifiedName~LiveOrchestratorMemoryRetentionTests\"`.")]
public class LiveOrchestratorMemoryRetentionTests
{
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

        _provider = OrchestratorTestHarness.Build(
            memoryPathProvider: () => _tempMemoryPath);
        _memoryStore = _provider.GetRequiredService<MemoryStore>();
        _orchestrator = _provider.GetRequiredService<ProductionOrchestrator>();
    }

    [TearDown]
    public async Task TearDown()
    {
        // ServiceProvider must be disposed asynchronously when any
        // service implements IAsyncDisposable only (e.g.
        // InProcessMcpToolsProvider registered by AddChatPluginHost).
        // Calling Dispose() on the sync surface throws.
        if (_provider is not null)
            await _provider.DisposeAsync();
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
}
