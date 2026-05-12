namespace GA.Business.ML.Agents.Memory;

using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
/// A single persistent memory entry.
/// </summary>
/// <param name="Key">Stable identifier within a session (or globally if SessionId is null).</param>
/// <param name="Type">Entry category (e.g. "response", "fact", "preference").</param>
/// <param name="Content">The memory payload.</param>
/// <param name="Tags">Optional searchable tags.</param>
/// <param name="Timestamp">When the entry was written (UTC).</param>
/// <param name="SessionId">
/// Session scope. Null = global entry (visible to every session — use for
/// learned facts that should be shared, e.g. user-confirmed preferences).
/// Non-null = scoped to the caller's session (typically a SignalR
/// ConnectionId or HTTP session cookie). Entries written without a session
/// scope by callers that don't plumb one yet still default to global —
/// callers that DO plumb a session ID get isolation between users / tabs.
/// Backward compat: entries written before this field existed deserialise
/// with SessionId=null and are treated as global.
/// </param>
public sealed record MemoryEntry(
    string Key,
    string Type,
    string Content,
    string[] Tags,
    DateTimeOffset Timestamp,
    string? SessionId = null,
    // PR after #194: per-entry embedding vector for hybrid BM25 + cosine
    // retrieval. Lazily populated on first SearchHybridAsync() call if
    // the store was constructed with an IEmbeddingGenerator. Backward
    // compat: pre-v0.4 entries on disk have no embedding field and
    // deserialize with Embedding=null; SearchHybridAsync skips the
    // cosine contribution for those entries and falls back to pure BM25.
    float[]? Embedding = null);

/// <summary>
/// In-process agent memory backed by a JSON file at <c>~/.ga/memory.json</c>.
/// Thread-safe via <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Session scoping (2026-05-10):</b> entries carry an optional
/// <see cref="MemoryEntry.SessionId"/>. Read and Search filter to entries
/// whose SessionId matches the caller's, plus entries with SessionId=null
/// (global / shared knowledge). Write requires the caller to pass a
/// SessionId explicitly — callers without a session pass null to write a
/// global entry.
/// </para>
/// <para>
/// The store's primary key is now <c>(SessionId, Key)</c>. Two sessions can
/// both have a "response_xyz" entry without colliding; a global entry with
/// the same key sits in a third slot. Backward-compat dictionary indexing
/// uses a composite key string internally — callers stay on the (key,
/// sessionId) surface.
/// </para>
/// <para>
/// Closes the leak documented in <see cref="Hooks.MemoryHook"/> remarks:
/// retrieval was OFF because the previous global store leaked between
/// anonymous chatbot sessions. With this fix, retrieval can be re-enabled
/// via <c>Memory:EnrichOnRetrieve=true</c> as long as the caller plumbs a
/// session identifier into <see cref="Hooks.ChatHookContext.SessionId"/>.
/// </para>
/// </remarks>
public sealed class MemoryStore
{
    /// <summary>Default on-disk location used by the parameterless constructor.</summary>
    public static readonly string DefaultStorePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ga", "memory.json");

    private readonly string _storePath;
    private readonly ILogger<MemoryStore>? _logger;

    /// <summary>
    /// Optional embedder for hybrid BM25 + cosine retrieval. When null,
    /// <see cref="SearchHybridAsync"/> degrades to pure BM25 and emits
    /// a one-shot warning so operators notice the misconfiguration
    /// rather than silently losing the embedding-ranking lift.
    /// </summary>
    private readonly Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>? _embedder;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    // Composite key = "<sessionId><key>" so two sessions can have the
    // same Key without colliding. Unit separator (U+001F) is illegal in
    // session IDs and keys, so split-on-U+001F is unambiguous.
    private readonly ConcurrentDictionary<string, MemoryEntry> _entries;

    // Serializes Save() so concurrent Write() calls don't corrupt the JSON
    // file. The previous File.WriteAllText racing pattern produced
    // FileShare violations under modest concurrency and silently lost the
    // entire store when Load() then hit a JsonException — see PR #151
    // review (reliability finding rel-006). Cap is 1; the lock is held only
    // for the serialize+write window so it's not a hot path.
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    /// <summary>
    /// SessionId value used by the storage layer to represent "global"
    /// (no session scope). Callers can pass <see cref="GlobalSessionId"/>
    /// or null interchangeably — both resolve to global.
    /// </summary>
    public const string? GlobalSessionId = null;

    private const char KeySeparator = '';

    /// <summary>
    /// Production constructor — backs the store with the default on-disk
    /// location (<see cref="DefaultStorePath"/> = <c>~/.ga/memory.json</c>).
    /// Used by DI registration in <c>GaPlugin</c>.
    /// </summary>
    public MemoryStore() : this(DefaultStorePath, logger: null) { }

    /// <summary>
    /// Testable constructor — accepts an explicit store path so tests can
    /// isolate from the user's real <c>~/.ga/memory.json</c>. Production
    /// callers should use the parameterless constructor (or the DI-friendly
    /// <see cref="MemoryStore(ILogger{MemoryStore})"/> overload when a
    /// logger is available).
    /// </summary>
    public MemoryStore(string storePath) : this(storePath, logger: null) { }

    /// <summary>
    /// DI-friendly constructor — uses the default store path but accepts
    /// an ILogger so non-JSON Load() failures (permission denied, disk
    /// full, etc.) surface in the operator's logs instead of silently
    /// starting fresh. Without this, a permissions regression on
    /// <c>~/.ga/memory.json</c> looks identical to "memory forgot
    /// everything between restarts."
    /// </summary>
    public MemoryStore(ILogger<MemoryStore> logger) : this(DefaultStorePath, logger) { }

    /// <summary>
    /// Full constructor — explicit store path plus optional logger.
    /// Tests use this with a temp path; production uses one of the
    /// overloads above.
    /// </summary>
    public MemoryStore(string storePath, ILogger<MemoryStore>? logger)
        : this(storePath, logger, embedder: null) { }

    /// <summary>
    /// Hybrid-retrieval constructor — adds an optional embedder for
    /// <see cref="SearchHybridAsync"/>. When the embedder is null the
    /// store falls back to pure BM25; when provided it lazy-populates
    /// per-entry embeddings on first hybrid-search of each entry.
    /// </summary>
    public MemoryStore(
        string storePath,
        ILogger<MemoryStore>? logger,
        Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>? embedder)
    {
        _storePath = storePath ?? throw new ArgumentNullException(nameof(storePath));
        _logger    = logger;
        _embedder  = embedder;
        _entries   = Load(_storePath, logger);
    }

    /// <summary>
    /// DI-friendly hybrid constructor — default path, logger, embedder.
    /// </summary>
    public MemoryStore(
        ILogger<MemoryStore> logger,
        Microsoft.Extensions.AI.IEmbeddingGenerator<string, Microsoft.Extensions.AI.Embedding<float>>? embedder)
        : this(DefaultStorePath, logger, embedder) { }

    // Bounded so anonymous chatbot traffic at the public demo URL can't grow
    // ~/.ga/memory.json unbounded — see PR #151 review (reliability rel-007).
    // When over budget, evict the oldest entries by Timestamp before adding
    // the new one. Override via Memory:MaxEntries config in callers if needed.
    public const int DefaultMaxEntries = 10_000;

    /// <summary>
    /// Writes (upserts) a memory entry scoped to <paramref name="sessionId"/>.
    /// Pass null for a global entry visible to every session. Evicts oldest
    /// entries when the store exceeds <see cref="DefaultMaxEntries"/>.
    /// </summary>
    public void Write(string? sessionId, string key, string type, string content, string[]? tags = null)
    {
        ValidateKeyOrThrow(key);
        ValidateSessionIdOrThrow(sessionId);

        var entry = new MemoryEntry(key, type, content, tags ?? [], DateTimeOffset.UtcNow, sessionId);
        _entries[CompositeKey(sessionId, key)] = entry;

        if (_entries.Count > DefaultMaxEntries)
        {
            // Evict the oldest 10% so this work runs amortised, not per-write.
            // ConcurrentDictionary snapshot is consistent enough for trimming.
            // Eviction is GLOBAL across sessions on purpose — disk size cap is a
            // host-level concern, not a per-session quota.
            var evictTarget = _entries.Count - (DefaultMaxEntries * 9 / 10);
            var oldest = _entries.Values
                .OrderBy(e => e.Timestamp)
                .Take(evictTarget)
                .Select(e => CompositeKey(e.SessionId, e.Key))
                .ToList();
            foreach (var k in oldest)
                _entries.TryRemove(k, out _);
        }

        Save();
    }

    /// <summary>
    /// Reads a single entry by key for the given session. Returns the
    /// session-scoped entry if one exists; otherwise falls through to a
    /// global entry (SessionId=null) with the same key; otherwise null.
    /// </summary>
    public MemoryEntry? Read(string? sessionId, string key)
    {
        if (sessionId is not null && _entries.TryGetValue(CompositeKey(sessionId, key), out var scoped))
            return scoped;
        return _entries.TryGetValue(CompositeKey(null, key), out var global) ? global : null;
    }

    /// <summary>
    /// BM25-ranked search across content, tags, and key, filtered to entries
    /// whose SessionId matches <paramref name="sessionId"/> OR is null
    /// (global). Pass <paramref name="sessionId"/>=null to search only global
    /// entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why BM25 over token-overlap (2026-05-12 v0.3):</b> token-overlap
    /// counted how many distinct query terms appeared in each entry but
    /// gave no weight to term rarity or document length. Two entries with
    /// the same overlap count ranked by insertion timestamp, which meant
    /// a long generic note containing the query term once would tie with
    /// a short focused entry where the term is the topic — and the older
    /// of the two would lose ranking. BM25 introduces (a) IDF weighting
    /// so rare terms dominate common terms, and (b) length normalization
    /// so a 200-token note doesn't outscore a 5-token preference just by
    /// having more token slots to land hits in.
    /// </para>
    /// <para>
    /// <b>Algorithm:</b> standard Okapi BM25 with the Robertson-Spärck
    /// Jones IDF variant (<c>log(1 + (N - df + 0.5) / (df + 0.5))</c>,
    /// guaranteed non-negative so a term that appears in every in-scope
    /// document still contributes a small positive score). Defaults
    /// <c>k1 = 1.5</c>, <c>b = 0.75</c> per the original BM25 paper —
    /// these are calibrated for natural-language prose and match what
    /// Elasticsearch / Lucene use out of the box. The corpus is built
    /// per-call from in-scope entries (session + type + tag filters
    /// applied first), so DF / avgDL reflect the user's view, not the
    /// global store.
    /// </para>
    /// <para>
    /// <b>What this is still NOT:</b> a vector-embedding search.
    /// Synonyms / paraphrases still need to share at least one stemmed
    /// token. Embedding-based recall over MemoryStore would reuse the
    /// SemanticIntentRouter embedder and is the next leverage step — but
    /// requires either an in-memory embedding index alongside the JSON
    /// store, or a separate ANN backend.
    /// </para>
    /// <para>
    /// <b>Backward compatibility:</b> BM25 returns the same set of
    /// entries as token-overlap (score &gt; 0 iff at least one query
    /// token overlaps the entry haystack). Ordering changes within that
    /// set: previously by overlap count then timestamp, now by BM25
    /// score then timestamp. Existing tests assert the rank-by-overlap
    /// invariant — BM25 preserves this because more distinct query-term
    /// hits each contribute a positive IDF-weighted term.
    /// </para>
    /// </remarks>
    public IReadOnlyList<MemoryEntry> Search(string? sessionId, string query, string? type = null, string[]? tags = null)
    {
        var queryTokens = TokenizeAndFilter(query);
        if (queryTokens.Count == 0)
        {
            // Pure-stopword or empty query — no signal to match on. Return
            // empty rather than fall through to "match everything", which
            // would dump the entire session-scoped store into the prompt.
            return [];
        }

        // Build the in-scope corpus AFTER session + type + tag filters so
        // BM25's DF / avgDL reflect the user's view. A document term that
        // appears in 100% of the store but only 10% of the in-scope view
        // is still a useful discriminator within that view.
        var corpus = _entries.Values
            .Where(e => InScope(e, sessionId))
            .Where(e => type is null || e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Where(e => tags is null || tags.Any(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .Select(TokenizeEntry)
            .ToList();

        if (corpus.Count == 0) return [];

        // Average document length over the in-scope corpus. Defensive:
        // if every entry is empty after stopword filtering, fall back to
        // 1 so the b * |D|/avgDL term doesn't divide by zero.
        var avgDocLength = corpus.Average(d => d.TokenCount);
        if (avgDocLength <= 0) avgDocLength = 1;

        // Pre-compute IDF for each query term against the in-scope corpus.
        // Robertson-Spärck Jones with the +1 wrapper keeps the value
        // non-negative even when every document contains the term.
        var idf = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var q in queryTokens)
        {
            var df = corpus.Count(d => d.TermFreq.ContainsKey(q));
            idf[q] = Math.Log(1.0 + (corpus.Count - df + 0.5) / (df + 0.5));
        }

        return corpus
            .Select(d => (d.Entry, Score: ComputeBm25(d, queryTokens, idf, avgDocLength)))
            .Where(scored => scored.Score > 0)
            .OrderByDescending(scored => scored.Score)
            .ThenByDescending(scored => scored.Entry.Timestamp)
            .Select(scored => scored.Entry)
            .ToList();
    }

    /// <summary>
    /// Hybrid BM25 + cosine-similarity search. Each entry's final score is
    /// <c>HybridBm25Weight * normalizedBm25 + HybridCosineWeight * cosine</c>
    /// where normalizedBm25 is the entry's BM25 score divided by the
    /// best BM25 score in the in-scope corpus (so the two terms are
    /// comparable). Entries with no precomputed embedding are
    /// lazy-embedded during this call and the result is cached back to
    /// disk via <see cref="Save"/> on the next write.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why hybrid:</b> BM25 alone misses paraphrases that share
    /// concept-space but no surface tokens ("drop-2 voicings I like"
    /// vs "two-note-skip jazz chord shapes"). Pure cosine alone over-
    /// matches semantically-related-but-not-asked entries (a query for
    /// "jazz" pulls every entry that ever mentioned music). The hybrid
    /// gets both signals.
    /// </para>
    /// <para>
    /// <b>When no embedder is configured</b> the method emits a one-
    /// shot warning and delegates to <see cref="Search"/> for pure
    /// BM25. The caller's contract is unchanged — they still get an
    /// ordered list — only the ranking quality differs.
    /// </para>
    /// <para>
    /// <b>Lazy backfill:</b> any in-scope entry without an embedding is
    /// embedded once during this call. The updated <see cref="MemoryEntry"/>
    /// replaces the existing one in <see cref="_entries"/>; the next
    /// <see cref="Write"/> serializes the embeddings to disk. This
    /// avoids paying the embedding cost on Write while still
    /// accumulating the cache over time.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<MemoryEntry>> SearchHybridAsync(
        string? sessionId,
        string query,
        string? type = null,
        string[]? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_embedder is null)
        {
            WarnNoEmbedderOnce();
            return Search(sessionId, query, type, tags);
        }

        var queryTokens = TokenizeAndFilter(query);
        if (queryTokens.Count == 0) return [];

        var corpus = _entries.Values
            .Where(e => InScope(e, sessionId))
            .Where(e => type is null || e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Where(e => tags is null || tags.Any(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .Select(TokenizeEntry)
            .ToList();
        if (corpus.Count == 0) return [];

        // BM25 layer — same math as Search() so the score is comparable.
        var avgDocLength = corpus.Average(d => d.TokenCount);
        if (avgDocLength <= 0) avgDocLength = 1;

        var idf = new Dictionary<string, double>(StringComparer.Ordinal);
        foreach (var q in queryTokens)
        {
            var df = corpus.Count(d => d.TermFreq.ContainsKey(q));
            idf[q] = Math.Log(1.0 + (corpus.Count - df + 0.5) / (df + 0.5));
        }

        var bm25Scores = corpus
            .Select(d => (d.Entry, Bm25: ComputeBm25(d, queryTokens, idf, avgDocLength)))
            .ToList();
        var maxBm25 = bm25Scores.Max(s => s.Bm25);
        if (maxBm25 <= 0) maxBm25 = 1;  // defensive — every entry has BM25 = 0; cosine carries the ranking

        // Cosine layer — embed the query once, embed any uncached entries.
        var queryEmbedding = await EmbedAsync(query, cancellationToken);

        // Lazy backfill: gather entries needing embedding, batch into a
        // single embedder call (the IEmbeddingGenerator surface is
        // designed for batches; per-entry calls would multiply latency).
        var needEmbedding = corpus
            .Where(d => d.Entry.Embedding is null || d.Entry.Embedding.Length == 0)
            .ToList();
        if (needEmbedding.Count > 0)
        {
            var texts = needEmbedding.Select(d => EmbeddingText(d.Entry)).ToList();
            var newEmbeddings = await _embedder.GenerateAsync(texts, cancellationToken: cancellationToken);
            for (var i = 0; i < needEmbedding.Count; i++)
            {
                var entry = needEmbedding[i].Entry;
                var vec   = newEmbeddings[i].Vector.ToArray();
                var updated = entry with { Embedding = vec };
                _entries[CompositeKey(updated.SessionId, updated.Key)] = updated;
            }
        }

        // Re-read entries from _entries to pick up the lazy-backfilled
        // embeddings (corpus has the stale snapshot).
        var ranked = new List<(MemoryEntry Entry, double Hybrid)>(bm25Scores.Count);
        foreach (var (entry, bm25) in bm25Scores)
        {
            var current = _entries.TryGetValue(CompositeKey(entry.SessionId, entry.Key), out var fresh)
                ? fresh
                : entry;
            var cosine = current.Embedding is { Length: > 0 }
                ? CosineSimilarity(queryEmbedding, current.Embedding)
                : 0.0;
            var hybrid = HybridBm25Weight * (bm25 / maxBm25)
                       + HybridCosineWeight * cosine;
            ranked.Add((current, hybrid));
        }

        return ranked
            .Where(r => r.Hybrid > 0)
            .OrderByDescending(r => r.Hybrid)
            .ThenByDescending(r => r.Entry.Timestamp)
            .Select(r => r.Entry)
            .ToList();
    }

    /// <summary>BM25 weight in the hybrid score. <c>0.5</c> balances
    /// exact-token recall against semantic-similarity recall.</summary>
    public const double HybridBm25Weight = 0.5;

    /// <summary>Cosine weight in the hybrid score. <c>0.5</c> balances
    /// semantic-similarity against exact-token recall.</summary>
    public const double HybridCosineWeight = 0.5;

    private int _warnNoEmbedderFired;

    private void WarnNoEmbedderOnce()
    {
        if (Interlocked.Exchange(ref _warnNoEmbedderFired, 1) != 0) return;
        _logger?.LogWarning(
            "MemoryStore.SearchHybridAsync called without an IEmbeddingGenerator " +
            "configured — falling back to pure BM25. Hybrid ranking lift is " +
            "lost. To enable, register an IEmbeddingGenerator<string, " +
            "Embedding<float>> in DI before constructing MemoryStore.");
    }

    private async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        var batch = await _embedder!.GenerateAsync([text], cancellationToken: ct);
        return batch[0].Vector.ToArray();
    }

    /// <summary>
    /// Builds the text to embed for an entry — content + key + tags
    /// concatenated. Mirrors the BM25 token-haystack so cosine and
    /// BM25 score the same surface.
    /// </summary>
    private static string EmbeddingText(MemoryEntry entry)
    {
        if (entry.Tags.Length == 0) return $"{entry.Content} {entry.Key}";
        return $"{entry.Content} {entry.Key} {string.Join(' ', entry.Tags)}";
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0) return 0;
        if (a.Length != b.Length) return 0;

        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot  += a[i] * b[i];
            magA += a[i] * a[i];
            magB += b[i] * b[i];
        }
        var denom = Math.Sqrt(magA) * Math.Sqrt(magB);
        return denom > 0 ? dot / denom : 0;
    }

    /// <summary>BM25 default <c>k1</c> (term-frequency saturation).</summary>
    /// <remarks>
    /// <para>
    /// <c>k1 = 1.5</c> per the original Okapi BM25 paper and matches the
    /// defaults in Elasticsearch / Lucene. Higher values reward repeated
    /// occurrences of the same query term within an entry more
    /// aggressively; lower values flatten the curve.
    /// </para>
    /// </remarks>
    public const double Bm25K1 = 1.5;

    /// <summary>BM25 default <c>b</c> (length normalization).</summary>
    /// <remarks>
    /// <para>
    /// <c>b = 0.75</c> per the original Okapi BM25 paper — applies 75% of
    /// the full length-normalization penalty. <c>b = 0</c> would disable
    /// length normalization; <c>b = 1</c> would normalize fully (a 100-
    /// token doc with one hit scores the same as a 10-token doc with one
    /// hit).
    /// </para>
    /// </remarks>
    public const double Bm25B = 0.75;

    private readonly record struct TokenizedEntry(
        MemoryEntry Entry,
        Dictionary<string, int> TermFreq,
        int TokenCount);

    private static TokenizedEntry TokenizeEntry(MemoryEntry entry)
    {
        var bag = new Dictionary<string, int>(StringComparer.Ordinal);
        var totalCount = 0;

        AppendTokensWithCounts(entry.Content, bag, ref totalCount);
        AppendTokensWithCounts(entry.Key,     bag, ref totalCount);
        foreach (var tag in entry.Tags)
            AppendTokensWithCounts(tag, bag, ref totalCount);

        return new TokenizedEntry(entry, bag, totalCount);
    }

    private static double ComputeBm25(
        TokenizedEntry doc,
        HashSet<string> queryTokens,
        Dictionary<string, double> idf,
        double avgDocLength)
    {
        double score = 0;
        foreach (var q in queryTokens)
        {
            if (!doc.TermFreq.TryGetValue(q, out var tf)) continue;
            // BM25 contribution for term q:
            //   idf(q) * tf * (k1 + 1) / (tf + k1 * (1 - b + b * |D| / avgDL))
            var lengthNorm = 1 - Bm25B + Bm25B * doc.TokenCount / avgDocLength;
            var numerator   = tf * (Bm25K1 + 1);
            var denominator = tf + Bm25K1 * lengthNorm;
            score += idf[q] * numerator / denominator;
        }
        return score;
    }

    /// <summary>
    /// Small stopword set covering English function words that would
    /// dilute overlap counts. Kept minimal on purpose — adding more
    /// risks dropping legitimate signal (e.g., "key" is a stopword in
    /// general English but load-bearing in music context).
    /// </summary>
    private static readonly HashSet<string> Stopwords = new(StringComparer.Ordinal)
    {
        "the", "a", "an", "and", "or", "but", "is", "are", "was", "were", "be",
        "to", "of", "in", "on", "at", "for", "with", "by", "from", "as",
        "this", "that", "these", "those", "it", "its",
        "what", "which", "who", "when", "where", "why", "how",
        "i", "me", "my", "we", "us", "our", "you", "your",
        "do", "does", "did", "can", "could", "should", "would", "will",
        "have", "has", "had", "not",
    };

    /// <summary>
    /// Splits the input on non-alphanumeric characters, lowercases each
    /// token, drops empty / single-character / stopword tokens. Returns
    /// a HashSet — used on the query side where we only care about
    /// distinct terms (BM25 contribution is summed per unique query term).
    /// </summary>
    private static HashSet<string> TokenizeAndFilter(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return tokens;
        WalkTokens(text, tok => tokens.Add(tok));
        return tokens;
    }

    /// <summary>
    /// Same tokenization rules as <see cref="TokenizeAndFilter"/>, but
    /// accumulates a term-frequency multiset into <paramref name="bag"/>
    /// and increments <paramref name="totalCount"/> for every kept token.
    /// Used on the entry side to feed BM25 — both TF (per term, in the
    /// bag) and document length (sum of all token occurrences) are needed.
    /// </summary>
    private static void AppendTokensWithCounts(string text, Dictionary<string, int> bag, ref int totalCount)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Avoid capturing the ref param inside a closure (illegal); inline the
        // increment by walking tokens manually here.
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : '\0';
            var isWord = char.IsLetterOrDigit(c);
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                var tok = text[start..i].ToLowerInvariant();
                if (tok.Length >= 2 && !Stopwords.Contains(tok))
                {
                    bag[tok] = bag.TryGetValue(tok, out var existing) ? existing + 1 : 1;
                    totalCount++;
                }
                start = -1;
            }
        }
    }

    /// <summary>
    /// Shared single-pass tokenizer used by <see cref="TokenizeAndFilter"/>.
    /// Walks the string once, yields each kept token to <paramref name="onToken"/>.
    /// </summary>
    private static void WalkTokens(string text, Action<string> onToken)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            var c = i < text.Length ? text[i] : '\0';
            var isWord = char.IsLetterOrDigit(c);
            if (isWord && start < 0)
            {
                start = i;
            }
            else if (!isWord && start >= 0)
            {
                var tok = text[start..i].ToLowerInvariant();
                if (tok.Length >= 2 && !Stopwords.Contains(tok))
                    onToken(tok);
                start = -1;
            }
        }
    }

    /// <summary>
    /// Returns summary statistics for entries visible to <paramref name="sessionId"/>
    /// (session-scoped + global). Pass null for global-only stats.
    /// </summary>
    public (int TotalEntries, IReadOnlyDictionary<string, int> ByType) Stats(string? sessionId = null)
    {
        var visible = _entries.Values.Where(e => InScope(e, sessionId)).ToList();
        var byType = visible
            .GroupBy(e => e.Type)
            .ToDictionary(g => g.Key, g => g.Count());
        return (visible.Count, byType);
    }

    /// <summary>
    /// Returns total entry count across ALL sessions. For host-level disk
    /// budgeting / eviction monitoring — not for per-session UX.
    /// </summary>
    public int TotalEntriesAllSessions() => _entries.Count;

    private static bool InScope(MemoryEntry entry, string? sessionId)
    {
        // Global entries (SessionId=null) are visible to every caller. Scoped
        // entries are visible only when the caller's sessionId matches exactly.
        if (entry.SessionId is null) return true;
        if (sessionId is null) return false;
        return string.Equals(entry.SessionId, sessionId, StringComparison.Ordinal);
    }

    private static string CompositeKey(string? sessionId, string key)
        => $"{sessionId ?? string.Empty}{KeySeparator}{key}";

    private static void ValidateKeyOrThrow(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Memory key cannot be empty.", nameof(key));
        if (key.IndexOf(KeySeparator) >= 0)
            throw new ArgumentException(
                $"Memory key cannot contain the unit separator (U+001F).", nameof(key));
    }

    private static void ValidateSessionIdOrThrow(string? sessionId)
    {
        if (sessionId is null) return;
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException(
                "SessionId must be either null (global) or a non-empty string.", nameof(sessionId));
        if (sessionId.IndexOf(KeySeparator) >= 0)
            throw new ArgumentException(
                $"SessionId cannot contain the unit separator (U+001F).", nameof(sessionId));
    }

    private void Save()
    {
        // Snapshot under the lock so concurrent Writes that mutate _entries
        // mid-serialise don't produce truncated JSON. MemoryFileWriter then
        // handles the atomic-rename + owner-only mode (PR #174 review
        // follow-up: 0600 on Unix, default ACL inheritance on Windows).
        _saveLock.Wait();
        try
        {
            var json = JsonSerializer.Serialize(_entries.Values.ToList(), JsonOpts);
            MemoryFileWriter.WriteAtomic(_storePath, json, _logger);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static ConcurrentDictionary<string, MemoryEntry> Load(string storePath, ILogger<MemoryStore>? logger = null)
    {
        var dict = new ConcurrentDictionary<string, MemoryEntry>();
        if (!File.Exists(storePath)) return dict;

        try
        {
            var json = File.ReadAllText(storePath);
            var entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json, JsonOpts);
            if (entries is not null)
            {
                foreach (var e in entries)
                {
                    // Pre-session-scope entries deserialise with SessionId=null
                    // because the field didn't exist on disk. That's the
                    // intended migration path: legacy entries become global.
                    dict[CompositeKey(e.SessionId, e.Key)] = e;
                }
            }
        }
        catch (JsonException jex)
        {
            // Corrupt file — rename it so operators see the loss instead of
            // silently starting fresh; preserves the bytes for postmortem.
            string? corruptPath = null;
            try
            {
                corruptPath = storePath + $".corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                File.Move(storePath, corruptPath, overwrite: false);
            }
            catch
            {
                // Best-effort rename; don't block boot if it fails.
            }
            // Surface the loss to the operator (PR #157 review rel-001):
            // without this, "memory forgot everything between restarts"
            // looks identical to a permissions regression.
            logger?.LogWarning(jex,
                "MemoryStore: {Path} contained invalid JSON — quarantined as {Corrupt} and starting fresh.",
                storePath, corruptPath ?? "(rename failed)");
        }
        catch (Exception ex)
        {
            // Other IO errors (file locked, permission denied) — start fresh
            // rather than crash boot, but DO log. The previous swallow-all
            // behaviour hid permissions regressions for days (PR #157
            // review rel-001).
            logger?.LogWarning(ex,
                "MemoryStore: failed to read {Path}; starting with empty store. " +
                "Common causes: file permissions, locked-by-other-process, disk full.",
                storePath);
        }

        return dict;
    }
}
