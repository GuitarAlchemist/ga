namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Memory;
using Microsoft.Extensions.AI;

/// <summary>
/// Pins the v0.4 hybrid BM25 + cosine ranking behavior of
/// <see cref="MemoryStore.SearchHybridAsync"/>. Three layers of coverage:
/// matching contract, lazy-backfill behavior, and graceful fallback when
/// no embedder is configured.
/// </summary>
/// <remarks>
/// These tests use a deterministic stub embedder (vectors derived from
/// hashed token sets) so the rank-ordering invariants are reproducible
/// without an Ollama dependency. Tests that need a real embedder belong
/// in the live e2e suite, not here.
/// </remarks>
[TestFixture]
public class MemoryStoreSearchHybridTests
{
    private string _tempDir = string.Empty;
    private MemoryStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ga-mem-hybrid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new MemoryStore(
            Path.Combine(_tempDir, "memory.json"),
            logger: null,
            embedder: new StubEmbedder());
    }

    [TearDown]
    public void TearDown()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { /* best-effort */ }
    }

    // ─── Matching contract — same shape as BM25 ────────────────────────

    [Test]
    public async Task HybridSearch_ReturnsEntries_WithAnyOverlapping_TokensOrConcept()
    {
        _store.Write("sess-A", "pref", "preference",
            content: "I prefer drop-2 voicings for jazz comping",
            tags: ["user-stated"]);

        var results = await _store.SearchHybridAsync("sess-A", "voicings jazz");

        Assert.That(results, Has.Count.EqualTo(1),
            "Hybrid must match anything that BM25 would match — strictly " +
            "additive on the matching contract.");
    }

    [Test]
    public async Task HybridSearch_RespectsSessionScope()
    {
        _store.Write("sess-A", "k1", "preference", content: "voicings jazz", tags: []);
        _store.Write("sess-B", "k2", "preference", content: "voicings jazz", tags: []);

        var resultsA = await _store.SearchHybridAsync("sess-A", "voicings");

        Assert.That(resultsA.Select(e => e.Key), Is.EquivalentTo(new[] { "k1" }),
            "Hybrid must enforce the same session scope as Search — the " +
            "SC-001 defense lives on this invariant.");
    }

    // ─── Lazy backfill — entries gain embeddings on first search ──────

    [Test]
    public async Task HybridSearch_PopulatesEmbedding_OnFirstQuery()
    {
        _store.Write("sess-A", "k1", "preference",
            content: "drop-2 voicings", tags: []);

        // Before search: no embedding on the entry.
        var before = _store.Read("sess-A", "k1")!;
        Assert.That(before.Embedding, Is.Null,
            "Write must NOT trigger embedding — that's the lazy-backfill " +
            "design (Write stays fast, embedding cost amortized on Search).");

        await _store.SearchHybridAsync("sess-A", "jazz");

        // After search: embedding is cached.
        var after = _store.Read("sess-A", "k1")!;
        Assert.That(after.Embedding, Is.Not.Null,
            "SearchHybridAsync must lazy-backfill embeddings for in-scope " +
            "entries that don't have them yet.");
        Assert.That(after.Embedding!.Length, Is.GreaterThan(0));
    }

    [Test]
    public async Task HybridSearch_DoesNotReEmbed_OnSubsequentSearches()
    {
        var embedder = new StubEmbedder();
        var store = new MemoryStore(
            Path.Combine(_tempDir, "memory2.json"),
            logger: null,
            embedder: embedder);

        store.Write("sess-A", "k1", "preference", content: "drop-2 voicings", tags: []);

        await store.SearchHybridAsync("sess-A", "jazz");
        var callsAfterFirst = embedder.CallCount;

        await store.SearchHybridAsync("sess-A", "blues");
        var callsAfterSecond = embedder.CallCount;

        // Second search embeds the QUERY ("blues") but NOT the entry —
        // the entry's cached embedding is reused. So delta is exactly 1.
        Assert.That(callsAfterSecond - callsAfterFirst, Is.EqualTo(1),
            "Second search must reuse cached entry embeddings; only the " +
            "new query gets embedded. If the entry is re-embedded, the " +
            "lazy-cache is broken and SearchHybridAsync pays the full " +
            "embedding cost on every call.");
    }

    // ─── Fallback — no embedder configured ────────────────────────────

    [Test]
    public async Task HybridSearch_WithoutEmbedder_FallsBackToBm25()
    {
        var storeWithoutEmbedder = new MemoryStore(
            Path.Combine(_tempDir, "memory3.json"));
        storeWithoutEmbedder.Write("sess-A", "k1", "preference",
            content: "drop-2 voicings", tags: []);

        var results = await storeWithoutEmbedder.SearchHybridAsync("sess-A", "voicings");

        Assert.That(results, Has.Count.EqualTo(1),
            "Without an embedder, SearchHybridAsync must fall back to pure " +
            "BM25 — same matching contract, just no cosine layer. Callers " +
            "must NOT see a behavior change beyond ranking quality.");

        // Verify no embedding got persisted.
        var entry = storeWithoutEmbedder.Read("sess-A", "k1")!;
        Assert.That(entry.Embedding, Is.Null,
            "When no embedder is available, entries must NOT acquire " +
            "empty embeddings — the field stays null so a future " +
            "constructor with an embedder can backfill cleanly.");
    }

    // ─── Hybrid weighting — both layers contribute ────────────────────

    [Test]
    public async Task HybridWeights_AreStable()
    {
        // Pin the weights to 0.5 each so a future tuning regression
        // recompiles consumers and runs through a retrieval-quality
        // baseline before landing. Same posture as Bm25K1/Bm25B.
        Assert.That(MemoryStore.HybridBm25Weight, Is.EqualTo(0.5));
        Assert.That(MemoryStore.HybridCosineWeight, Is.EqualTo(0.5));
        Assert.That(MemoryStore.HybridBm25Weight + MemoryStore.HybridCosineWeight,
            Is.EqualTo(1.0),
            "Weights must sum to 1 so the hybrid score is in [0, 1].");
    }

    // ─── Backward compat — pre-v0.4 entries deserialize cleanly ──────

    [Test]
    public void LegacyEntry_WithoutEmbeddingField_DeserializesAsNull()
    {
        // Simulate a memory.json file written by a pre-v0.4 process:
        // no Embedding field on the entry. Forward-compat path: must
        // deserialize cleanly with Embedding=null.
        var legacyJson = """
        [
          {
            "Key": "legacy",
            "Type": "preference",
            "Content": "drop-2 voicings",
            "Tags": ["user-stated"],
            "Timestamp": "2026-05-01T00:00:00Z",
            "SessionId": "sess-A"
          }
        ]
        """;
        var path = Path.Combine(_tempDir, "legacy.json");
        File.WriteAllText(path, legacyJson);

        var store = new MemoryStore(path);
        var entry = store.Read("sess-A", "legacy");

        Assert.That(entry, Is.Not.Null,
            "Pre-v0.4 memory.json files must still load — the Embedding " +
            "field is additive with a null default.");
        Assert.That(entry!.Embedding, Is.Null,
            "Missing Embedding field deserializes to null (record default).");
    }

    // ─── Concurrent Write race (corr-001 from PR #195 review) ─────────

    [Test]
    public async Task ConcurrentWrite_DuringLazyBackfill_DoesNotClobberNewerEntry()
    {
        // Reproduces the race the reviewer flagged: a SearchHybridAsync
        // captures entry v1, Write replaces it with v2 mid-flight, the
        // search's lazy-backfill must NOT overwrite v2 with `v1 with
        // { Embedding = ... }`. The slow embedder forces the
        // interleaving deterministically.
        var slowEmbedder = new SlowStubEmbedder(delayMs: 200);
        var store = new MemoryStore(
            Path.Combine(_tempDir, "race.json"),
            logger: null,
            embedder: slowEmbedder);

        store.Write("sess-A", "k1", "preference",
            content: "v1 content", tags: []);

        // Start the search; it'll await on the slow embedder.
        var searchTask = store.SearchHybridAsync("sess-A", "anything");

        // Wait long enough that the search has snapshotted the corpus
        // and is awaiting the embedder. Then Write v2.
        await Task.Delay(50);
        store.Write("sess-A", "k1", "preference",
            content: "v2 content overwritten", tags: ["v2-tag"]);

        // Now let the search complete.
        await searchTask;

        // The post-state entry must be v2, NOT a v1-with-embedding zombie.
        var entry = store.Read("sess-A", "k1")!;
        Assert.That(entry.Content, Is.EqualTo("v2 content overwritten"),
            "Race regression: lazy-backfill must not clobber a concurrent Write. " +
            "Captured v1, Write replaced with v2, embed-call resolved AFTER — the " +
            "AddOrUpdate guard must detect that current.Timestamp != captured.Timestamp " +
            "and skip the embedding assignment. Otherwise v1's content/tags zombie back.");
        Assert.That(entry.Tags, Is.EquivalentTo(new[] { "v2-tag" }),
            "Tags must be v2's, not v1's.");
    }

    // ─── corr-002: no-BM25-signal cosine-only path ────────────────────

    [Test]
    public async Task NoBm25Signal_CosineOnlyWeighting_StillReturnsResults()
    {
        // Construct a corpus where NONE of the entries share a token
        // with the query — BM25 = 0 for all. The cosine layer should
        // carry the ranking on its own. Without the corr-002 fix the
        // hybrid score would cap at 0.5 * cosine; with the fix the
        // score is cosine directly.
        //
        // Use a DENSE stub embedder that produces guaranteed-positive
        // cosine for any two inputs — otherwise the hash-bucket stub
        // produces orthogonal vectors for disjoint strings and the
        // filter (Hybrid > 0) excludes the entries before we can verify
        // the weighting branch.
        var store = new MemoryStore(
            Path.Combine(_tempDir, "nobm25.json"),
            logger: null,
            embedder: new DenseStubEmbedder());
        store.Write("sess-A", "k1", "fact",
            content: "fingerstyle technique practice", tags: []);
        store.Write("sess-A", "k2", "fact",
            content: "jazz harmony fundamentals", tags: []);

        // Query has NO token overlap with either entry (after stopwords).
        var results = await store.SearchHybridAsync("sess-A", "drop voicings");

        Assert.That(results, Is.Not.Empty,
            "Hybrid search must return entries when BM25 finds nothing but " +
            "cosine has signal. With corr-002 fix, scores are cosine-only " +
            "in this branch — entries with cosine > 0 still surface.");
    }

    // ─── corr-003: Key string excluded from embedding text ────────────

    [Test]
    public async Task EmbeddingText_ExcludesKey_HashSuffixedKeysDoNotPolluteVector()
    {
        // Two entries with identical content but different (hash-shaped)
        // keys MUST produce identical embeddings — otherwise the hash
        // suffix is leaking into the semantic vector and entries with
        // different keys but same content would cluster by key, not
        // by meaning.
        var embedder = new RecordingEmbedder();
        var store = new MemoryStore(
            Path.Combine(_tempDir, "key-pollution.json"),
            logger: null,
            embedder: embedder);

        store.Write("sess-A", "preference_voicings_a1b2c3d4", "preference",
            content: "drop-2 voicings for jazz comping", tags: []);
        store.Write("sess-A", "response_e5f6a7b8", "preference",
            content: "drop-2 voicings for jazz comping", tags: []);

        await store.SearchHybridAsync("sess-A", "jazz");

        Assert.That(embedder.AllRecordedTexts, Is.Not.Empty,
            "Embedder must have been called at least once.");
        foreach (var text in embedder.AllRecordedTexts)
        {
            Assert.That(text, Does.Not.Contain("preference_voicings_a1b2c3d4"),
                "EmbeddingText must NOT include the entry's key. Hash-suffixed " +
                "keys would inject non-semantic tokens that distort the vector.");
            Assert.That(text, Does.Not.Contain("response_e5f6a7b8"),
                "EmbeddingText must NOT include the entry's key.");
        }
    }

    // ─── corr-004: record equality with array fields ──────────────────

    [Test]
    public void MemoryEntry_Equality_UsesSemanticArrayComparison()
    {
        var ts = new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero);
        var a = new MemoryEntry("k", "fact", "content",
            ["jazz", "user-stated"], ts, "sess-A");
        var b = new MemoryEntry("k", "fact", "content",
            ["jazz", "user-stated"], ts, "sess-A");

        Assert.That(a, Is.EqualTo(b),
            "MemoryEntry with semantically identical Tags must compare equal " +
            "regardless of array reference identity. The record-synthesized " +
            "equality would use reference equality here, breaking " +
            "HashSet/Distinct/AreEqual.");
        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()),
            "Equal entries must hash equal.");
    }

    [Test]
    public void MemoryEntry_Equality_IgnoresEmbedding()
    {
        var ts = new DateTimeOffset(2026, 5, 12, 0, 0, 0, TimeSpan.Zero);
        var withoutEmb = new MemoryEntry("k", "fact", "c", [], ts, "sess-A",
            Embedding: null);
        var withEmb = new MemoryEntry("k", "fact", "c", [], ts, "sess-A",
            Embedding: [0.1f, 0.2f, 0.3f]);

        Assert.That(withoutEmb, Is.EqualTo(withEmb),
            "Entries identical apart from Embedding must compare equal — " +
            "Embedding is a derived cache, not semantic identity.");
    }

    // ─── corr-005: embedder failure falls back to BM25 ────────────────

    [Test]
    public async Task EmbedderFailure_FallsBackToBm25_WithoutThrowing()
    {
        var failingEmbedder = new ThrowingEmbedder();
        var store = new MemoryStore(
            Path.Combine(_tempDir, "embed-fail.json"),
            logger: null,
            embedder: failingEmbedder);

        store.Write("sess-A", "k1", "preference",
            content: "drop-2 voicings for jazz", tags: []);

        var results = await store.SearchHybridAsync("sess-A", "voicings");

        Assert.That(results, Has.Count.EqualTo(1),
            "When the embedder throws, SearchHybridAsync must catch and fall " +
            "back to BM25-only ranking — callers expect Search-like graceful " +
            "degradation, not propagated exceptions.");
    }

    // ─── Stub embedder — deterministic, no Ollama dependency ──────────

    private sealed class StubEmbedder : IEmbeddingGenerator<string, Embedding<float>>
    {
        public int CallCount { get; private set; }

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            var embeddings = values
                .Select(text => new Embedding<float>(MakeVector(text)))
                .ToArray();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        /// <summary>
        /// Deterministic 16-dim vector derived from the input text's
        /// token hash. Two semantically-similar strings (e.g. share a
        /// common token) produce embeddings with non-zero cosine; two
        /// disjoint strings produce orthogonal vectors. Enough to
        /// exercise the rank-ordering paths without an LLM.
        /// </summary>
        private static float[] MakeVector(string text)
        {
            var vec = new float[16];
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var hash = word.ToLowerInvariant().GetHashCode();
                var slot = ((hash % 16) + 16) % 16;
                vec[slot] += 1.0f;
            }
            // Normalize so cosine is in [-1, 1].
            var mag = MathF.Sqrt(vec.Sum(v => v * v));
            if (mag > 0)
                for (var i = 0; i < vec.Length; i++) vec[i] /= mag;
            return vec;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>
    /// Stub that delays each batch by <paramref name="delayMs"/> ms so
    /// tests can interleave a concurrent Write between the corpus
    /// snapshot and the embedder's resolution. Used by the
    /// ConcurrentWrite race test.
    /// </summary>
    private sealed class SlowStubEmbedder(int delayMs) : IEmbeddingGenerator<string, Embedding<float>>
    {
        public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            await Task.Delay(delayMs, cancellationToken);
            var embeddings = values
                .Select(text => new Embedding<float>(new float[8]))
                .ToArray();
            return new GeneratedEmbeddings<Embedding<float>>(embeddings);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>
    /// Records every text the embedder is asked to embed. Used by the
    /// Key-exclusion test to verify EmbeddingText doesn't leak hash-shaped
    /// keys into the embedding input.
    /// </summary>
    private sealed class RecordingEmbedder : IEmbeddingGenerator<string, Embedding<float>>
    {
        private readonly List<string> _all = new();
        public IReadOnlyList<string> AllRecordedTexts => _all;

        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var texts = values.ToList();
            _all.AddRange(texts);
            var embeddings = texts
                .Select(_ => new Embedding<float>(new float[8]))
                .ToArray();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>
    /// Dense stub — every dimension has a positive baseline so cosine
    /// is always &gt; 0 even for disjoint inputs. Used by the no-BM25-
    /// signal test, which needs the cosine-only branch to actually
    /// return entries.
    /// </summary>
    private sealed class DenseStubEmbedder : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var embeddings = values
                .Select(MakeDenseVector)
                .Select(v => new Embedding<float>(v))
                .ToArray();
            return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(embeddings));
        }

        private static float[] MakeDenseVector(string text)
        {
            // 8-dim vector. Baseline of 0.5 in every dimension so any two
            // vectors have cosine ≥ 0.5 even when text content is fully
            // disjoint. Per-word contributions still differentiate
            // semantically similar strings.
            var vec = new float[8];
            for (var i = 0; i < vec.Length; i++) vec[i] = 0.5f;
            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var hash = word.ToLowerInvariant().GetHashCode();
                var slot = ((hash % 8) + 8) % 8;
                vec[slot] += 0.3f;
            }
            // Normalize.
            var mag = MathF.Sqrt(vec.Sum(v => v * v));
            for (var i = 0; i < vec.Length; i++) vec[i] /= mag;
            return vec;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    /// <summary>
    /// Stub that throws on every call. Exercises the graceful-fallback
    /// path in SearchHybridAsync (corr-005). Without the try/catch the
    /// exception propagates out and breaks the caller's contract.
    /// </summary>
    private sealed class ThrowingEmbedder : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("simulated embedder failure");

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
