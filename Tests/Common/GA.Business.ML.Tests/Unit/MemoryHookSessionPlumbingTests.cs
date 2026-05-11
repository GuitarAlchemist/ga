namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Phase B regression tests — verify that <see cref="ChatHookContext.SessionId"/>
/// actually flows from the orchestrator down into <see cref="MemoryStore.Write"/>'s
/// sessionId argument. Without this, a future orchestrator refactor could silently
/// drop SessionId from one of the seven ChatHookContext construction sites in
/// ProductionOrchestrator and we'd regress to the cross-session-leak posture.
/// </summary>
/// <remarks>
/// Tests use a temp store path so the user's real ~/.ga/memory.json is never
/// touched. Each test builds its own MemoryHook with EnrichOnRetrieve=true so
/// the retrieval branch isn't short-circuited.
/// </remarks>
[TestFixture]
public class MemoryHookSessionPlumbingTests
{
    private string _tempDir = string.Empty;
    private string _tempStorePath = string.Empty;
    private MemoryStore _store = null!;
    private MemoryHook _hook = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-memhook-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempStorePath = Path.Combine(_tempDir, "memory.json");
        _store = new MemoryStore(_tempStorePath);

        var configValues = new Dictionary<string, string?> { ["Memory:EnrichOnRetrieve"] = "true" };
        var config = new ConfigurationBuilder().AddInMemoryCollection(configValues).Build();
        _hook = new MemoryHook(_store, config, NullLogger<MemoryHook>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ─── OnResponseSent — write path uses ctx.SessionId ───────────────

    [Test]
    public async Task OnResponseSent_PersistsWithCtxSessionId()
    {
        // Setup: a context with an explicit SessionId — simulates what the
        // orchestrator should pass after Phase B's plumbing.
        var ctx = new ChatHookContext
        {
            OriginalMessage  = "what's a Cmaj7?",
            CurrentMessage   = "what's a Cmaj7?",
            CorrelationId    = Guid.NewGuid(),
            SessionId        = "sess-from-orchestrator",
            MatchedSkillName = "chord-info",
            Response = new AgentResponse
            {
                AgentId    = "chord-info",
                Result     = "Cmaj7 contains the notes C, E, G, and B — root, major third, perfect fifth, and major seventh respectively. " +
                             "These four pitches together form the major seventh chord, the harmonic backbone of countless jazz progressions.",
                Confidence = 0.95f,
            },
        };

        await _hook.OnResponseSent(ctx);

        // Give the fire-and-forget Task.Run a moment to flush.
        await WaitForWriteAsync(expectedSessionId: "sess-from-orchestrator");

        var sessionEntries = _store.Search(sessionId: "sess-from-orchestrator", query: "Cmaj7");
        Assert.That(sessionEntries, Has.Count.EqualTo(1),
            "ctx.SessionId must reach MemoryStore.Write — otherwise the leak is back.");
        Assert.That(sessionEntries[0].SessionId, Is.EqualTo("sess-from-orchestrator"));
    }

    [Test]
    public async Task OnResponseSent_DifferentSessions_DoNotSeeEachOthersWrites()
    {
        // Two sessions write through the hook; each should only see its own.
        var sessA = MakeCtx("sess-A", "A asks about Cmaj7",
            "Cmaj7 contains C E G B — root, major third, perfect fifth, major seventh. " +
            "This is session A's conversation about the chord and what it sounds like in context.");
        var sessB = MakeCtx("sess-B", "B asks about Dm7",
            "Dm7 contains D F A C — root, minor third, perfect fifth, minor seventh. " +
            "This is session B's conversation about a completely different chord, the ii in C major.");

        await _hook.OnResponseSent(sessA);
        await _hook.OnResponseSent(sessB);

        await WaitForWriteAsync(expectedSessionId: "sess-A");
        await WaitForWriteAsync(expectedSessionId: "sess-B");

        var fromA = _store.Search(sessionId: "sess-A", query: "");
        var fromB = _store.Search(sessionId: "sess-B", query: "");
        var fromC = _store.Search(sessionId: "sess-C", query: ""); // a third, unrelated session

        Assert.That(fromA, Has.Count.EqualTo(1), "Session A should see exactly its one entry.");
        Assert.That(fromB, Has.Count.EqualTo(1), "Session B should see exactly its one entry.");
        Assert.That(fromC, Is.Empty,
            "An unrelated session must not see A's or B's writes — that's the leak Phase A+B closes.");
        Assert.That(fromA[0].SessionId, Is.EqualTo("sess-A"));
        Assert.That(fromB[0].SessionId, Is.EqualTo("sess-B"));
    }

    // ─── OnRequestReceived — retrieval path uses ctx.SessionId ─────────

    [Test]
    public async Task OnRequestReceived_WithSessionId_ScopesRetrievalToThatSession()
    {
        // Pre-populate the store with one entry per session, identical content.
        _store.Write(sessionId: "sess-A", key: "k1", type: "response",
            content: "this is A's previous conversation about voicings of Cmaj7");
        _store.Write(sessionId: "sess-B", key: "k2", type: "response",
            content: "this is B's previous conversation about voicings of Cmaj7");

        var ctxA = new ChatHookContext
        {
            // Query is "Cmaj7" — a substring present in both A's and B's
            // stored content. If isolation works, only A's entry is
            // returned. If it doesn't, B's would surface too.
            OriginalMessage = "Cmaj7",
            CurrentMessage  = "Cmaj7",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = "sess-A",
        };

        var result = await _hook.OnRequestReceived(ctxA);

        // The hook injects matching entries by mutating the message — verify that
        // session A's entry showed up but session B's did NOT leak in.
        Assert.That(result.MutatedMessage, Is.Not.Null,
            "Hook should have found a memory match for session A and injected context.");
        Assert.That(result.MutatedMessage, Does.Contain("A's previous conversation"),
            "Session A's own memory should be in the injection.");
        Assert.That(result.MutatedMessage, Does.Not.Contain("B's previous conversation"),
            "Session B's memory MUST NOT leak into session A's request — that's the regression we're guarding against.");
    }

    [Test]
    public async Task OnRequestReceived_WithoutSessionId_SkipsRetrievalConservatively()
    {
        // Pre-populate so retrieval WOULD find something if the hook didn't skip.
        _store.Write(sessionId: "sess-A", key: "k1", type: "response",
            content: "this content matches the query string");
        _store.Write(sessionId: null, key: "k2", type: "fact",
            content: "this content also matches the query string");

        var ctxNoSession = new ChatHookContext
        {
            OriginalMessage = "tell me about content",
            CurrentMessage  = "tell me about content",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = null,   // simulates "transport hasn't plumbed SessionId yet"
        };

        var result = await _hook.OnRequestReceived(ctxNoSession);

        Assert.That(result.MutatedMessage, Is.Null,
            "With SessionId=null, the hook MUST skip retrieval entirely (safety belt). " +
            "Returning content from any session would replay the cross-session leak.");
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private ChatHookContext MakeCtx(string sessionId, string question, string answer) =>
        new()
        {
            OriginalMessage  = question,
            CurrentMessage   = question,
            CorrelationId    = Guid.NewGuid(),
            SessionId        = sessionId,
            MatchedSkillName = "test",
            Response = new AgentResponse
            {
                AgentId    = "test",
                Result     = answer,
                Confidence = 0.95f,
            },
        };

    /// <summary>
    /// Waits up to 2 seconds for the hook's fire-and-forget write to land.
    /// Polls Search() rather than sleeping a fixed amount so the test is
    /// fast on a hot machine and tolerant on a cold one.
    /// </summary>
    private async Task WaitForWriteAsync(string expectedSessionId, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (_store.Search(sessionId: expectedSessionId, query: "").Count > 0) return;
            await Task.Delay(25);
        }
        Assert.Fail($"Fire-and-forget Write for session '{expectedSessionId}' did not land within {timeoutMs}ms.");
    }
}
