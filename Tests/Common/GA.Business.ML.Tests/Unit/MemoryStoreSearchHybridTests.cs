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
}
