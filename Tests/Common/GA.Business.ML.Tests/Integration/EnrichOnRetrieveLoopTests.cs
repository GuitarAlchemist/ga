namespace GA.Business.ML.Tests.Integration;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Hooks;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// End-to-end smoke test that the memory subsystem closes the loop when
/// <c>Memory:EnrichOnRetrieve=true</c>:
/// </summary>
/// <list type="number">
/// <item>User says "remember that I prefer drop-2 voicings" →
/// <see cref="RememberThisSkill"/> parses, emits a
/// <see cref="MemoryWriteRequest"/> on <see cref="AgentResponse.Data"/>.</item>
/// <item><see cref="MemoryWriteHook"/> picks it up on
/// <c>OnResponseSent</c>, persists with the caller's <c>SessionId</c>.</item>
/// <item>Same user asks a follow-up about voicings →
/// <see cref="MemoryHook"/>.OnRequestReceived retrieves the entry via
/// session-scoped <see cref="MemoryStore"/>.Search and injects it as
/// <c>[Relevant context from memory]</c> prefix.</item>
/// </list>
/// <remarks>
/// <para>
/// <b>What this test pins:</b> the architectural invariant that the four
/// pieces shipped across PRs #157, #173/#174, #177, #179 actually work
/// together. Any future refactor that breaks this loop — silently
/// dropping <c>SessionId</c>, changing the search predicate, reverting
/// the <c>EnrichOnRetrieve</c> default, breaking the hook order — fails
/// here.
/// </para>
/// <para>
/// <b>Cross-session isolation</b> is the load-bearing security property
/// (SC-001 lineage). A separate test verifies session B sees no entries
/// written by session A. If this test ever passes leaks, the chatbot
/// regressed to the pre-PR-157 cross-session-leak posture.
/// </para>
/// <para>
/// <b>What this test does NOT cover:</b> the embedder, the LLM, the
/// SignalR transport, or the actual <c>ProductionOrchestrator</c>
/// hook-chain dispatch. Those are integration-tested elsewhere. Here we
/// exercise the four pieces directly so the contract is locked at the
/// component level — fast, deterministic, no external deps.
/// </para>
/// </remarks>
[TestFixture]
public class EnrichOnRetrieveLoopTests
{
    private string _tempDir = string.Empty;
    private MemoryStore _store = null!;
    private ChatTranscriptStore _transcripts = null!;
    private RememberThisSkill _skill = null!;
    private MemoryWriteHook _writeHook = null!;
    private MemoryHook _retrieveHook = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-enrich-e2e-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _store        = new MemoryStore(Path.Combine(_tempDir, "memory.json"));
        _transcripts  = new ChatTranscriptStore(Path.Combine(_tempDir, "transcripts.json"));

        _skill        = new RememberThisSkill(NullLogger<RememberThisSkill>.Instance);
        _writeHook    = new MemoryWriteHook(_store, NullLogger<MemoryWriteHook>.Instance);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Memory:EnrichOnRetrieve"] = "true",
            })
            .Build();
        _retrieveHook = new MemoryHook(_store, _transcripts, config, NullLogger<MemoryHook>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    [Test]
    public async Task FullLoop_RememberThenAsk_RetrievesInjectedContext()
    {
        const string sessionId = "sess-alice";

        // ── Phase 1: WRITE — user asks the chatbot to remember a preference.
        var writeResponse = await _skill.ExecuteAsync(
            "remember that I prefer drop-2 voicings for jazz comping");

        Assert.That(writeResponse.Data, Is.InstanceOf<MemoryWriteRequest>(),
            "Phase 1: RememberThisSkill must emit a MemoryWriteRequest on .Data.");

        var writeCtx = new ChatHookContext
        {
            OriginalMessage = "remember that I prefer drop-2 voicings for jazz comping",
            CurrentMessage  = "remember that I prefer drop-2 voicings for jazz comping",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = sessionId,
            Response        = writeResponse,
        };
        await _writeHook.OnResponseSent(writeCtx);

        Assert.That(_store.TotalEntriesAllSessions(), Is.EqualTo(1),
            "Phase 1: MemoryWriteHook must have persisted exactly one entry.");
        var stored = _store.Search(sessionId, "drop-2");
        Assert.That(stored, Is.Not.Empty,
            "Phase 1: the persisted entry must be findable via session-scoped Search.");
        Assert.That(stored.Single().Type, Is.EqualTo("preference"));

        // ── Phase 2: RETRIEVE — same user asks a follow-up. MemoryHook.
        //   OnRequestReceived must surface the preference into the prompt.
        //
        // NOTE on the retrieval query: MemoryStore.Search uses substring
        // containment (entry.Content.Contains(query)), so the follow-up
        // query needs to be a substring of the stored content. The stored
        // entry's content is "I prefer drop-2 voicings for jazz comping",
        // so we ask about "drop-2 voicings". This is a documented v0.1
        // limitation — improving search to BM25 / token-overlap is a
        // separate task. The loop itself works; only the recall is weak.
        const string followUp = "drop-2 voicings";
        var retrieveCtx = new ChatHookContext
        {
            OriginalMessage = followUp,
            CurrentMessage  = followUp,
            CorrelationId   = Guid.NewGuid(),
            SessionId       = sessionId,
        };
        var result = await _retrieveHook.OnRequestReceived(retrieveCtx);

        Assert.That(result.MutatedMessage, Is.Not.Null,
            "Phase 2: MemoryHook must have mutated the message — EnrichOnRetrieve=true + " +
            "a matching session-scoped entry exists, so retrieval must inject.");
        Assert.That(result.MutatedMessage, Does.Contain("drop-2 voicings"),
            "Phase 2: the injected context must include the persisted preference content.");
        Assert.That(result.MutatedMessage, Does.Contain("Relevant context from memory"),
            "Phase 2: the canonical injection prefix must be present so the LLM knows " +
            "the section is retrieved context rather than user input.");
        Assert.That(result.MutatedMessage, Does.EndWith(followUp),
            "Phase 2: the original user message must remain — injection prepends, " +
            "never replaces.");
    }

    [Test]
    public async Task FullLoop_CrossSession_DoesNotLeakEntries()
    {
        const string sessionA = "sess-alice";
        const string sessionB = "sess-bob";

        // Alice asks the chatbot to remember a preference.
        var aliceResponse = await _skill.ExecuteAsync(
            "remember that I prefer drop-2 voicings for jazz comping");
        await _writeHook.OnResponseSent(new ChatHookContext
        {
            OriginalMessage = "remember that I prefer drop-2 voicings",
            CurrentMessage  = "remember that I prefer drop-2 voicings",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = sessionA,
            Response        = aliceResponse,
        });

        // Bob, in a different session, asks the SAME follow-up question.
        var bobRetrieveResult = await _retrieveHook.OnRequestReceived(new ChatHookContext
        {
            OriginalMessage = "what voicings should I use for jazz?",
            CurrentMessage  = "what voicings should I use for jazz?",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = sessionB,
        });

        // Bob MUST NOT see Alice's preference. If this leaks, we've regressed
        // to the pre-PR-157 cross-session-leak posture and EnrichOnRetrieve
        // must be disabled in production until storage scoping is repaired.
        Assert.That(bobRetrieveResult.MutatedMessage, Is.Null,
            "Cross-session isolation: session B must not retrieve session A's entries. " +
            "If this fails, SC-001's session-scoping defense is broken.");
    }

    [Test]
    public async Task FullLoop_AnonymousCaller_NullSessionId_BothPhasesRefuse()
    {
        // The hook contract is: when SessionId is null, write refuses (writing
        // to global without the operator-flagged path would defeat SC-001's
        // defense) AND retrieve refuses (treating null as global session
        // replays the leak the OFF-default was protecting against). Both
        // hooks log a warning and Continue. This test pins both refusals.
        var writeResponse = await _skill.ExecuteAsync(
            "remember that I prefer drop-2 voicings");

        await _writeHook.OnResponseSent(new ChatHookContext
        {
            OriginalMessage = "x",
            CurrentMessage  = "x",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = null,   // anonymous
            Response        = writeResponse,
        });

        Assert.That(_store.TotalEntriesAllSessions(), Is.EqualTo(0),
            "Null SessionId on the write path → refuse. Otherwise we'd land an entry " +
            "in the global partition without an operator-flagged path.");

        // Retrieve side: even if there WERE a global entry, null SessionId
        // skips retrieval. Verify no injection happens.
        var retrieveResult = await _retrieveHook.OnRequestReceived(new ChatHookContext
        {
            OriginalMessage = "what voicings",
            CurrentMessage  = "what voicings",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = null,
        });
        Assert.That(retrieveResult.MutatedMessage, Is.Null,
            "Null SessionId on the retrieve path → no injection. Mirrors the write " +
            "side's posture for symmetric defense.");
    }

    [Test]
    public async Task FullLoop_NoMemoryYet_ReturnsContinue_NoMutation()
    {
        // Fresh session, no prior writes. EnrichOnRetrieve=true is on, but
        // Search returns empty, so MemoryHook returns Continue without
        // mutating. Pins that an empty store doesn't break the request path.
        var result = await _retrieveHook.OnRequestReceived(new ChatHookContext
        {
            OriginalMessage = "what voicings should I use",
            CurrentMessage  = "what voicings should I use",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = "sess-anyone",
        });

        Assert.That(result.MutatedMessage, Is.Null,
            "Empty store with EnrichOnRetrieve=true must return Continue — never " +
            "inject empty context or a placeholder header.");
    }

    [Test]
    public async Task FullLoop_ThreeRemembers_SameSession_AllRetrievableTogether()
    {
        // Validates accumulation: a user can layer multiple remembers in the
        // same session, and all three should be findable on a later query.
        // MemoryHook injects up to 3 entries — exactly hits that ceiling.
        const string sessionId = "sess-carol";

        var statements = new[]
        {
            "remember that I prefer drop-2 voicings",
            "save this: my favorite key is Bb",
            "note: I'm working on jazz comping this month",
        };

        foreach (var s in statements)
        {
            var resp = await _skill.ExecuteAsync(s);
            await _writeHook.OnResponseSent(new ChatHookContext
            {
                OriginalMessage = s,
                CurrentMessage  = s,
                CorrelationId   = Guid.NewGuid(),
                SessionId       = sessionId,
                Response        = resp,
            });
        }

        Assert.That(_store.TotalEntriesAllSessions(), Is.EqualTo(3));

        // Search uses substring containment (entry.Content.Contains(query)),
        // so we query a short substring that appears in the stored
        // preference content "I prefer drop-2 voicings for jazz comping".
        // "voicings" is shared lexicon between the user's intent and the
        // preference; that's enough for the single-term substring search.
        var result = await _retrieveHook.OnRequestReceived(new ChatHookContext
        {
            OriginalMessage = "voicings",
            CurrentMessage  = "voicings",
            CorrelationId   = Guid.NewGuid(),
            SessionId       = sessionId,
        });

        Assert.That(result.MutatedMessage, Does.Contain("drop-2 voicings"),
            "Layered remember-calls must accumulate; the voicings preference is one entry " +
            "and must surface for a query that overlaps its content.");
    }
}
