namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Memory;

/// <summary>
/// Pins the v0.3 BM25 ranking behavior of <see cref="MemoryStore.Search"/>.
/// Token-overlap (v0.2) gave the same score to every entry containing a
/// query term once, breaking ties by timestamp. BM25 adds (a) IDF weighting
/// so rare terms dominate common terms, and (b) length normalization so a
/// long generic note doesn't outscore a short focused entry merely by
/// having more slots for hits to land in.
/// </summary>
/// <remarks>
/// These tests are deliberately scenario-based: they construct minimal
/// corpora that exercise one BM25 property at a time, then assert the
/// resulting rank ordering. Floating-point-equality checks on the score
/// itself are avoided — only the relative order matters for ranking, and
/// pinning numeric values would couple the test to k1/b constants that
/// future tuning may legitimately change.
/// </remarks>
[TestFixture]
public class MemoryStoreSearchBm25Tests
{
    private string _tempDir = string.Empty;
    private MemoryStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-mem-bm25-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new MemoryStore(Path.Combine(_tempDir, "memory.json"));
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ─── IDF — rare terms dominate common terms ────────────────────────

    [Test]
    public void RareTerm_RanksAheadOf_CommonTerm()
    {
        // "voicings" appears in every entry (low IDF). "kontakt" appears
        // in only one (high IDF). A query that matches BOTH should boost
        // the entry containing the rare term over the entry containing
        // only the common one — even though both share the common term
        // and both share one matching term with the query.
        _store.Write("sess-A", "common-only", "preference",
            content: "voicings for ballads", tags: []);
        _store.Write("sess-A", "common-and-rare", "preference",
            content: "voicings for kontakt sample libraries", tags: []);
        _store.Write("sess-A", "filler-1", "preference",
            content: "voicings for jazz comping", tags: []);
        _store.Write("sess-A", "filler-2", "preference",
            content: "voicings for blues", tags: []);

        var results = _store.Search("sess-A", "voicings kontakt");

        Assert.That(results, Has.Count.GreaterThanOrEqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo("common-and-rare"),
            "BM25 must boost entries that match the rare term ('kontakt' = " +
            "DF 1 of 4) over entries that only match the common term " +
            "('voicings' = DF 4 of 4). Token-overlap gave both score = 1.");
    }

    [Test]
    public void EqualOverlap_RareTerm_RanksAhead_OfCommonTerm()
    {
        // Discriminator test that isolates IDF from overlap count.
        // Both target entries match EXACTLY ONE query term (overlap = 1 each)
        // but one matches a rare term and the other matches a common term.
        // Under v0.2 token-overlap, both score = 1 → timestamp DESC breaks
        // the tie. Under v0.3 BM25, IDF dominates: the rare-term match
        // ranks ahead.
        //
        // Seed the corpus so "voicings" appears in 4 docs (low IDF) and
        // "kontakt" appears in only 1 doc (high IDF).
        _store.Write("sess-A", "filler-1", "preference",
            content: "voicings for ballads", tags: []);
        _store.Write("sess-A", "filler-2", "preference",
            content: "voicings for jazz", tags: []);
        _store.Write("sess-A", "filler-3", "preference",
            content: "voicings for blues", tags: []);
        Thread.Sleep(20);
        // Now write the rare-term matcher BEFORE the common-term matcher
        // so that under v0.2 timestamp DESC would favor the common matcher.
        _store.Write("sess-A", "matches-rare", "preference",
            content: "kontakt sample libraries are amazing", tags: []);
        Thread.Sleep(20);
        _store.Write("sess-A", "matches-common", "preference",
            content: "voicings for funk", tags: []);

        // Query has both terms. Each target entry overlaps EXACTLY ONE
        // query term, so v0.2 would call it a tie and use timestamp.
        var results = _store.Search("sess-A", "voicings kontakt");

        // Both matchers in the results, plus the 3 filler entries that
        // match "voicings" with the same low IDF — collateral but expected.
        var rareIndex   = results.ToList().FindIndex(e => e.Key == "matches-rare");
        var commonIndex = results.ToList().FindIndex(e => e.Key == "matches-common");

        Assert.That(rareIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(commonIndex, Is.GreaterThanOrEqualTo(0));
        Assert.That(rareIndex, Is.LessThan(commonIndex),
            "Entry matching the rare term (kontakt, df=1) must rank ahead of " +
            "entry matching the common term (voicings, df=4). Same overlap " +
            "count (1 each). Under v0.2 token-overlap, the common-term match " +
            "would win on timestamp DESC. Under v0.3 BM25, IDF makes the " +
            "rare-term match win on score.");
    }

    // ─── Length normalization — short entries outscore long entries
    //     when both have the same TF for the query term ──────────────────

    [Test]
    public void ShortEntry_RanksAheadOf_LongEntry_OnSingleHit()
    {
        // Both entries contain "voicings" exactly once. The short one is
        // 5 tokens; the long one is buried inside a 50-token note. BM25
        // length normalization (b=0.75) penalizes the long doc so the
        // short focused preference wins.
        _store.Write("sess-A", "short", "preference",
            content: "drop-2 voicings for jazz comping", tags: []);
        _store.Write("sess-A", "long", "note",
            content: "yesterday I was practicing scales over the C major and " +
                     "A minor pentatonic shapes on the fretboard while also " +
                     "experimenting with hammer-ons pull-offs and slides up " +
                     "and down the neck looking for fresh voicings ideas " +
                     "and the metronome was set to one twenty bpm so",
            tags: []);

        var results = _store.Search("sess-A", "voicings");

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo("short"),
            "Short focused entry must outrank long generic entry on a single-" +
            "hit query — BM25 length normalization (b=0.75) is exactly the " +
            "lever that does this work.");
    }

    // ─── Term frequency — repeated occurrences within an entry boost it ───

    [Test]
    public void RepeatedTermInEntry_RanksAhead_OfSingleOccurrence()
    {
        // Both entries are similar length. One mentions "jazz" once; the
        // other mentions "jazz" three times. TF saturation (k1=1.5) gives
        // the multi-mention entry a higher score, but with diminishing
        // returns — a 3x mention doesn't get a 3x score.
        //
        // **Test discriminator (PR #189 review fix):** write `thrice`
        // FIRST and `once` LAST so the timestamp DESC tie-break would
        // favor `once`. Under v0.2 token-overlap, both entries had
        // overlap = 1 and the secondary sort (timestamp DESC) would put
        // `once` on top. Under v0.3 BM25, TF=3 beats TF=1 regardless of
        // timestamp. If this test ever ranks `once` first, BM25 has
        // regressed to overlap-counting behavior.
        _store.Write("sess-A", "thrice", "preference",
            content: "jazz voicings jazz comping jazz standards practice",
            tags: []);
        // Force a measurable timestamp gap so the secondary sort would
        // actually flip under token-overlap (Write uses DateTimeOffset.UtcNow,
        // which on modern Windows has ~15ms resolution).
        Thread.Sleep(20);
        _store.Write("sess-A", "once", "preference",
            content: "I like jazz harmony in general", tags: []);

        var results = _store.Search("sess-A", "jazz");

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo("thrice"),
            "Entry with TF=3 must outrank entry with TF=1 even when `once` " +
            "was written later (newer timestamp). BM25's TF contribution must " +
            "dominate the timestamp tie-break — otherwise we'd be back to v0.2 " +
            "token-overlap counting.");
    }

    // ─── Backward compat — strictly-additive matching contract ────────

    [Test]
    public void NaturalQuery_StillMatches_LikeTokenOverlap()
    {
        // The v0.2 → v0.3 upgrade is supposed to be strictly additive:
        // the set of entries returned doesn't change, only the ordering.
        // This test reproduces the exact recall-test prompt from the v0.2
        // tests to confirm BM25 doesn't regress matching.
        _store.Write("sess-A", "pref_voicings", "preference",
            content: "I prefer drop-2 voicings for jazz comping",
            tags: ["user-stated"]);

        var results = _store.Search("sess-A",
            "what voicings should I use for jazz?");

        Assert.That(results, Has.Count.EqualTo(1),
            "BM25 returns entries with score > 0, which requires at least one " +
            "overlapping query term — same matching contract as token-overlap.");
    }

    [Test]
    public void HigherOverlap_StillRanksAhead_OfSingleHit()
    {
        // The v0.2 ranking invariant must hold under BM25: an entry that
        // matches 2 query terms (each TF=1) outranks an entry that matches
        // only 1 query term. BM25 makes this stricter because each matching
        // term contributes a positive IDF-weighted summand.
        _store.Write("sess-A", "single",  "preference",
            content: "I like open voicings on acoustic", tags: []);
        _store.Write("sess-A", "double",  "preference",
            content: "drop-2 voicings for jazz comping", tags: []);

        var results = _store.Search("sess-A", "jazz voicings");

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Key, Is.EqualTo("double"),
            "v0.2 ranking invariant: more distinct hits → higher rank. BM25 " +
            "preserves this because each matching term contributes a positive " +
            "IDF-weighted summand.");
    }

    // ─── BM25 constants pinned — change is deliberate, not accidental ──

    [Test]
    public void Bm25Constants_Match_OriginalPaperDefaults()
    {
        // k1 = 1.5 and b = 0.75 are the canonical Okapi BM25 defaults and
        // also Elasticsearch / Lucene defaults. Changing either is a
        // tuning decision that should re-run the chatbot retrieval recall
        // evaluation before merging — pin the values so a typo doesn't
        // silently shift the entire memory-retrieval surface.
        Assert.That(MemoryStore.Bm25K1, Is.EqualTo(1.5),
            "k1 must be 1.5 (Okapi BM25 default). Changes require a recall " +
            "regression run.");
        Assert.That(MemoryStore.Bm25B, Is.EqualTo(0.75),
            "b must be 0.75 (Okapi BM25 default). Changes require a recall " +
            "regression run.");
    }
}
