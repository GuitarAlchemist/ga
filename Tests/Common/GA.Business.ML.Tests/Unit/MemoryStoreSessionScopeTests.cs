namespace GA.Business.ML.Tests.Unit;

using System.Text.Json;
using GA.Business.ML.Agents.Memory;

/// <summary>
/// Tests for the session-scoping fix that closes the leak documented in
/// PR #151 review and BACKLOG.md Chatbot Track item "memory-session-scope".
/// </summary>
/// <remarks>
/// Uses a temp directory so the user's real <c>~/.ga/memory.json</c> is
/// never touched. Each test instantiates its own MemoryStore against a
/// freshly-deleted path.
/// </remarks>
[TestFixture]
public class MemoryStoreSessionScopeTests
{
    private string _tempDir = string.Empty;
    private string _tempStorePath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-memstore-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _tempStorePath = Path.Combine(_tempDir, "memory.json");
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ─── Core isolation guarantees ─────────────────────────────────────

    [Test]
    public void Write_SessionScopedEntry_NotVisibleToOtherSession()
    {
        // The leak this whole feature exists to close: session A writes a
        // chord reference, session B should NOT see it in any retrieval.
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: "sessionA", key: "k1", type: "response",
            content: "user asked about Cmaj7 over Dm");

        var resultsForB = store.Search(sessionId: "sessionB", query: "Cmaj7");

        Assert.That(resultsForB, Is.Empty,
            "Session B must not see entries written by session A — that's the leak the feature flag was protecting against.");
    }

    [Test]
    public void Write_SessionScopedEntry_VisibleToOwnSession()
    {
        // Negative test for the isolation guarantee: it shouldn't hide
        // entries from their OWN session.
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: "sessionA", key: "k1", type: "response",
            content: "user asked about Cmaj7 over Dm");

        var resultsForA = store.Search(sessionId: "sessionA", query: "Cmaj7");

        Assert.That(resultsForA, Has.Count.EqualTo(1));
        Assert.That(resultsForA[0].SessionId, Is.EqualTo("sessionA"));
    }

    [Test]
    public void Write_GlobalEntry_VisibleAcrossAllSessions()
    {
        // Entries written with SessionId=null are the "shared knowledge"
        // partition — visible to every session. MemoryMcpTools uses this.
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: null, key: "global-fact", type: "fact",
            content: "User prefers DADGAD tuning examples.");

        var fromA = store.Search(sessionId: "sessionA", query: "DADGAD");
        var fromB = store.Search(sessionId: "sessionB", query: "DADGAD");
        var fromAnon = store.Search(sessionId: null, query: "DADGAD");

        Assert.That(fromA, Has.Count.EqualTo(1));
        Assert.That(fromB, Has.Count.EqualTo(1));
        Assert.That(fromAnon, Has.Count.EqualTo(1));
    }

    [Test]
    public void Write_SameKeyDifferentSessions_DoesNotCollide()
    {
        // Two sessions can both have a "response_xyz" entry — they sit in
        // separate compositeKey slots and don't overwrite each other.
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: "sessionA", key: "shared-key", type: "response", content: "A's content");
        store.Write(sessionId: "sessionB", key: "shared-key", type: "response", content: "B's content");

        var fromA = store.Read(sessionId: "sessionA", key: "shared-key");
        var fromB = store.Read(sessionId: "sessionB", key: "shared-key");

        Assert.That(fromA!.Content, Is.EqualTo("A's content"));
        Assert.That(fromB!.Content, Is.EqualTo("B's content"));
    }

    // ─── Read fallback semantics ───────────────────────────────────────

    [Test]
    public void Read_FallsBackToGlobalWhenSessionSpecificMissing()
    {
        // If sessionA doesn't have a "user-pref" entry but a global one
        // exists, Read returns the global as a fallback — matches the
        // "shared knowledge augments per-session" intent.
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: null, key: "user-pref", type: "fact", content: "global preference");

        var result = store.Read(sessionId: "sessionA", key: "user-pref");

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Content, Is.EqualTo("global preference"));
        Assert.That(result.SessionId, Is.Null);
    }

    [Test]
    public void Read_PrefersSessionScopedOverGlobal()
    {
        // When both exist, the session-scoped entry wins. Lets a user
        // "override" a global default for their conversation.
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: null, key: "user-pref", type: "fact", content: "global value");
        store.Write(sessionId: "sessionA", key: "user-pref", type: "fact", content: "sessionA override");

        var fromA       = store.Read(sessionId: "sessionA", key: "user-pref");
        var fromB       = store.Read(sessionId: "sessionB", key: "user-pref");
        var fromGlobal  = store.Read(sessionId: null, key: "user-pref");

        Assert.That(fromA!.Content,      Is.EqualTo("sessionA override"));
        Assert.That(fromB!.Content,      Is.EqualTo("global value"));
        Assert.That(fromGlobal!.Content, Is.EqualTo("global value"));
    }

    // ─── Search filtering ──────────────────────────────────────────────

    [Test]
    public void Search_ReturnsGlobalPlusOwnSession_NotOtherSessions()
    {
        // The composite filter: matching content + (matching sessionId OR
        // null sessionId). Three distinct entries, three distinct sessions.
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: null,        key: "g1", type: "fact",     content: "shared dim7 reference");
        store.Write(sessionId: "sessionA",  key: "a1", type: "response", content: "A's dim7 conversation");
        store.Write(sessionId: "sessionB",  key: "b1", type: "response", content: "B's dim7 conversation");

        var fromA = store.Search(sessionId: "sessionA", query: "dim7");

        Assert.That(fromA, Has.Count.EqualTo(2),
            "Session A should see its own + the global, but NOT session B's.");
        Assert.That(fromA.Any(e => e.Key == "b1"), Is.False,
            "Cross-session leak: session B's entry surfaced in session A's search.");
        Assert.That(fromA.Any(e => e.Key == "a1"), Is.True);
        Assert.That(fromA.Any(e => e.Key == "g1"), Is.True);
    }

    [Test]
    public void Stats_FilterBySession_OnlyCountsVisibleEntries()
    {
        var store = new MemoryStore(_tempStorePath);
        store.Write(sessionId: null,       key: "g1", type: "fact",     content: "x");
        store.Write(sessionId: "sessionA", key: "a1", type: "response", content: "x");
        store.Write(sessionId: "sessionA", key: "a2", type: "response", content: "x");
        store.Write(sessionId: "sessionB", key: "b1", type: "response", content: "x");

        var (totalA, _) = store.Stats(sessionId: "sessionA");

        Assert.That(totalA, Is.EqualTo(3), "A sees its 2 entries + the global; not B's.");
        Assert.That(store.TotalEntriesAllSessions(), Is.EqualTo(4),
            "Grand total still reflects all 4 entries — host-level budget concerns operate on this.");
    }

    // ─── Persistence + backward compat ─────────────────────────────────

    [Test]
    public void BackwardCompat_LegacyEntryWithoutSessionId_LoadsAsGlobal()
    {
        // Simulate a memory.json written before SessionId existed:
        // serialise a list of entries where SessionId is intentionally
        // absent from the JSON. New MemoryStore should load them as
        // SessionId=null, treating them as global.
        Directory.CreateDirectory(Path.GetDirectoryName(_tempStorePath)!);
        const string legacyJson = @"[
            { ""Key"": ""legacy1"", ""Type"": ""fact"", ""Content"": ""older entry"", ""Tags"": [], ""Timestamp"": ""2026-05-01T00:00:00+00:00"" },
            { ""Key"": ""legacy2"", ""Type"": ""response"", ""Content"": ""another"", ""Tags"": [""auto""], ""Timestamp"": ""2026-05-02T00:00:00+00:00"" }
        ]";
        File.WriteAllText(_tempStorePath, legacyJson);

        var store = new MemoryStore(_tempStorePath);

        var fromAnySession = store.Search(sessionId: "any-new-session", query: "entry");
        Assert.That(fromAnySession, Has.Count.EqualTo(1),
            "Legacy entry without SessionId should be globally visible.");

        var direct = store.Read(sessionId: null, key: "legacy1");
        Assert.That(direct, Is.Not.Null);
        Assert.That(direct!.SessionId, Is.Null,
            "Legacy entry's SessionId deserialises to null (global).");
    }

    [Test]
    public void Write_Persists_AndIsolationSurvivesReload()
    {
        // Smoke test: write some session-scoped entries, reload the store
        // from disk, confirm session boundaries are preserved.
        var store1 = new MemoryStore(_tempStorePath);
        store1.Write(sessionId: "sessionA", key: "k1", type: "response", content: "A only");
        store1.Write(sessionId: "sessionB", key: "k1", type: "response", content: "B only");
        store1.Write(sessionId: null,       key: "k1", type: "fact",     content: "global");

        var store2 = new MemoryStore(_tempStorePath);

        Assert.That(store2.Read("sessionA", "k1")!.Content, Is.EqualTo("A only"));
        Assert.That(store2.Read("sessionB", "k1")!.Content, Is.EqualTo("B only"));
        Assert.That(store2.Read(null,       "k1")!.Content, Is.EqualTo("global"));
    }

    // ─── Defensive validation ──────────────────────────────────────────

    [Test]
    public void Write_RejectsEmptyKey()
    {
        var store = new MemoryStore(_tempStorePath);
        Assert.That(() => store.Write("sessionA", "", "fact", "x"), Throws.ArgumentException);
        Assert.That(() => store.Write("sessionA", "   ", "fact", "x"), Throws.ArgumentException);
    }

    [Test]
    public void Write_RejectsKeyContainingUnitSeparator()
    {
        // The composite-key encoding uses U+001F as the delimiter. Allowing
        // it in the key would let a malicious / sloppy caller forge a
        // different session's entry.
        var store = new MemoryStore(_tempStorePath);
        Assert.That(() => store.Write("sessionA", "evilkey", "fact", "x"), Throws.ArgumentException);
    }

    [Test]
    public void Write_RejectsEmptySessionId_ButAllowsNull()
    {
        var store = new MemoryStore(_tempStorePath);
        Assert.That(() => store.Write("", "k", "fact", "x"), Throws.ArgumentException,
            "Empty string is not a valid session ID; pass null for global instead.");
        Assert.That(() => store.Write("   ", "k", "fact", "x"), Throws.ArgumentException);
        Assert.That(() => store.Write(null, "k", "fact", "x"), Throws.Nothing,
            "null is the explicit signal for 'global entry'.");
    }

    [Test]
    public void Write_RejectsSessionIdContainingUnitSeparator()
    {
        var store = new MemoryStore(_tempStorePath);
        Assert.That(() => store.Write("evilsession", "k", "fact", "x"), Throws.ArgumentException);
    }
}
