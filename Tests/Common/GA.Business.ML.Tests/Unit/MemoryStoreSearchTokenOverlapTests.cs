namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Memory;

/// <summary>
/// Pins the v0.2 token-overlap behavior of <see cref="MemoryStore.Search"/>.
/// The prior v0.1 predicate required the full lowercased query to be a
/// substring of the entry content/tag/key; v0.2 tokenizes both sides,
/// drops stopwords + short tokens, and matches on token overlap.
/// </summary>
/// <remarks>
/// Backward compatibility: any query that previously matched via substring
/// containment still matches via token overlap (a substring shares all its
/// tokens with the haystack). Tests below verify both the strict
/// improvement (queries that used to fail now pass) AND the regression
/// guards (verbatim substring + type/tag filters + session scope all
/// unchanged).
/// </remarks>
[TestFixture]
public class MemoryStoreSearchTokenOverlapTests
{
    private string _tempDir = string.Empty;
    private MemoryStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-mem-search-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new MemoryStore(Path.Combine(_tempDir, "memory.json"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ─── Recall improvement (the bug this PR fixes) ─────────────────────

    [Test]
    public void NaturalQuery_DifferentPhrasing_NowFindsEntry()
    {
        // This is the exact failure surfaced by the e2e test: the stored
        // entry and the query share concepts but no full substring.
        _store.Write(sessionId: "sess-A", key: "pref_voicings",
            type: "preference",
            content: "I prefer drop-2 voicings for jazz comping",
            tags: ["user-stated"]);

        var results = _store.Search(sessionId: "sess-A",
            query: "what voicings should I use for jazz?");

        Assert.That(results, Has.Count.EqualTo(1),
            "v0.2 token-overlap MUST match — both share 'voicings' and 'jazz'. " +
            "Under v0.1 substring search this returned 0 results.");
    }

    [Test]
    public void WordOrder_DoesNotMatter()
    {
        _store.Write(sessionId: "sess-A", key: "pref",
            type: "preference",
            content: "drop-2 voicings for jazz",
            tags: []);

        // Query reverses the word order — substring search would have missed.
        var results = _store.Search(sessionId: "sess-A", query: "jazz voicings drop-2");

        Assert.That(results, Has.Count.EqualTo(1));
    }

    [Test]
    public void HigherOverlap_RanksAhead_OfSingleTokenMatches()
    {
        // Both entries match on "voicings". The one that also matches on
        // "jazz" must rank ahead.
        _store.Write(sessionId: "sess-A", key: "low",  type: "preference",
            content: "I like open voicings on acoustic", tags: []);
        _store.Write(sessionId: "sess-A", key: "high", type: "preference",
            content: "drop-2 voicings for jazz comping", tags: []);

        var results = _store.Search(sessionId: "sess-A", query: "jazz voicings");

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo("high"),
            "Entry matching both 'jazz' AND 'voicings' (overlap=2) must rank ahead " +
            "of entry matching only 'voicings' (overlap=1).");
    }

    [Test]
    public void Stopwords_AreFiltered_FromQuery()
    {
        _store.Write(sessionId: "sess-A", key: "k",  type: "fact",
            content: "intermediate guitarist", tags: []);

        // Query is mostly stopwords. After filtering, "intermediate" remains
        // and matches.
        var results = _store.Search(sessionId: "sess-A",
            query: "what is the user — an intermediate or what?");

        Assert.That(results, Has.Count.EqualTo(1));
    }

    [Test]
    public void PureStopwordQuery_ReturnsEmpty()
    {
        // Critical posture: a query with no signal tokens must NOT dump
        // the whole session's memory into the retrieval injection. Better
        // to inject nothing than to inject everything.
        _store.Write(sessionId: "sess-A", key: "k", type: "fact",
            content: "anything at all", tags: []);

        var results = _store.Search(sessionId: "sess-A", query: "what is the");

        Assert.That(results, Is.Empty,
            "A query that's pure stopwords must NOT return all entries. The whole " +
            "point of the stopword filter is to refuse no-signal queries — falling " +
            "through to 'match everything' would defeat the rate-limiting on " +
            "retrieval-injection prompts.");
    }

    [Test]
    public void ShortTokens_AreFiltered()
    {
        // Single-character tokens (e.g., "a", "I", initials) are dropped
        // by the length filter — they have no information content.
        _store.Write(sessionId: "sess-A", key: "k", type: "fact",
            content: "C is a chord", tags: []);

        // "a" alone is a stopword AND single-char; "c" is single-char.
        // Nothing should match.
        var results = _store.Search(sessionId: "sess-A", query: "a C");

        Assert.That(results, Is.Empty);
    }

    [Test]
    public void TokensInTags_AreSearchable()
    {
        _store.Write(sessionId: "sess-A", key: "k", type: "preference",
            content: "some content",
            tags: ["jazz", "audience:intermediate"]);

        var results = _store.Search(sessionId: "sess-A", query: "jazz");

        Assert.That(results, Has.Count.EqualTo(1),
            "Tokens in tags must be searchable — tags are intentional metadata.");
    }

    [Test]
    public void TokensInKey_AreSearchable()
    {
        _store.Write(sessionId: "sess-A", key: "preference_voicings_deadbeef",
            type: "preference", content: "x", tags: []);

        var results = _store.Search(sessionId: "sess-A", query: "voicings");

        Assert.That(results, Has.Count.EqualTo(1),
            "Tokens in the key must be searchable — keys carry semantic info " +
            "in the post-RememberThis era (e.g., 'preference_voicings_xxx').");
    }

    // ─── Backward compatibility ─────────────────────────────────────────

    [Test]
    public void VerbatimSubstringQuery_StillMatches()
    {
        _store.Write(sessionId: "sess-A", key: "k", type: "fact",
            content: "I prefer drop-2 voicings", tags: []);

        var results = _store.Search(sessionId: "sess-A", query: "drop-2 voicings");

        Assert.That(results, Has.Count.EqualTo(1),
            "v0.1 substring queries must continue to match — the new predicate " +
            "is strictly additive.");
    }

    [Test]
    public void TypeFilter_StillComposes()
    {
        _store.Write(sessionId: "sess-A", key: "p", type: "preference",
            content: "voicings jazz", tags: []);
        _store.Write(sessionId: "sess-A", key: "f", type: "fact",
            content: "voicings jazz", tags: []);

        var prefs = _store.Search(sessionId: "sess-A", query: "voicings", type: "preference");
        var facts = _store.Search(sessionId: "sess-A", query: "voicings", type: "fact");

        Assert.That(prefs, Has.Count.EqualTo(1));
        Assert.That(prefs[0].Type, Is.EqualTo("preference"));
        Assert.That(facts, Has.Count.EqualTo(1));
        Assert.That(facts[0].Type, Is.EqualTo("fact"));
    }

    [Test]
    public void TagFilter_StillComposes()
    {
        _store.Write(sessionId: "sess-A", key: "k1", type: "preference",
            content: "voicings jazz", tags: ["audience:beginner"]);
        _store.Write(sessionId: "sess-A", key: "k2", type: "preference",
            content: "voicings jazz", tags: ["audience:advanced"]);

        var results = _store.Search(sessionId: "sess-A",
            query: "voicings", tags: ["audience:advanced"]);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Key, Is.EqualTo("k2"));
    }

    [Test]
    public void SessionScope_StillEnforced()
    {
        // Two entries with the same content under different sessions.
        // Each session must only see its own entry plus global.
        _store.Write(sessionId: "sess-A", key: "k1", type: "preference",
            content: "voicings jazz", tags: []);
        _store.Write(sessionId: "sess-B", key: "k2", type: "preference",
            content: "voicings jazz", tags: []);
        _store.Write(sessionId: null,    key: "g",  type: "fact",
            content: "voicings everyone", tags: []);

        var sessA = _store.Search(sessionId: "sess-A", query: "voicings");
        var sessB = _store.Search(sessionId: "sess-B", query: "voicings");

        Assert.That(sessA.Select(e => e.Key), Is.EquivalentTo(new[] { "k1", "g" }),
            "Session A sees its own + global, never session B's.");
        Assert.That(sessB.Select(e => e.Key), Is.EquivalentTo(new[] { "k2", "g" }),
            "Session B sees its own + global, never session A's.");
    }

    [Test]
    public void EmptyQuery_ReturnsEmpty()
    {
        _store.Write(sessionId: "sess-A", key: "k", type: "fact", content: "x", tags: []);

        Assert.That(_store.Search(sessionId: "sess-A", query: ""),    Is.Empty);
        Assert.That(_store.Search(sessionId: "sess-A", query: "   "), Is.Empty);
    }
}
