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
    string? SessionId = null);

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
    {
        _storePath = storePath ?? throw new ArgumentNullException(nameof(storePath));
        _logger    = logger;
        _entries   = Load(_storePath, logger);
    }

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
    /// Token-overlap search across content, tags, and key, filtered to entries
    /// whose SessionId matches <paramref name="sessionId"/> OR is null
    /// (global). Pass <paramref name="sessionId"/>=null to search only global
    /// entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why token-overlap over substring containment (2026-05-11 v0.2):</b>
    /// the prior implementation required the full lowercased query to be a
    /// substring of <see cref="MemoryEntry.Content"/> / a tag / the key.
    /// That broke real-world recall — a user asking "what voicings should I
    /// use for jazz?" would not surface a stored "I prefer drop-2 voicings
    /// for jazz comping" entry, because neither string contains the other
    /// verbatim. The integration test surfaced this as a recall ceiling
    /// even when the loop architecturally worked.
    /// </para>
    /// <para>
    /// <b>New predicate:</b> tokenize both query and entry haystack
    /// (content + key + tags) into lowercase alphanumeric tokens, drop
    /// stopwords + very short tokens, count overlap. Entries with at least
    /// one non-stopword token in common with the query are returned,
    /// ordered by overlap count DESC, then by Timestamp DESC.
    /// </para>
    /// <para>
    /// <b>What this is NOT:</b> a BM25 / TF-IDF / vector-embedding search.
    /// It's a deliberately tiny step up from substring matching — enough to
    /// unlock real-world recall for the MemoryHook retrieval-injection
    /// path, without introducing dependencies. Higher-quality search would
    /// reuse the existing embedder used by SemanticIntentRouter — that's a
    /// separate task.
    /// </para>
    /// <para>
    /// <b>Backward compatibility:</b> any query that previously matched
    /// via substring containment also matches via token overlap (a
    /// substring shares all its tokens with the haystack). New behavior is
    /// strictly additive — no entry that was previously returned is now
    /// hidden. The order may shift because the secondary sort moved from
    /// "newest first" to "overlap first, then newest"; callers that
    /// depended on strict timestamp order (no callers in the current
    /// codebase) need to re-sort.
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

        return _entries.Values
            .Where(e => InScope(e, sessionId))
            .Where(e => type is null || e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Where(e => tags is null || tags.Any(t => e.Tags.Contains(t, StringComparer.OrdinalIgnoreCase)))
            .Select(e => (Entry: e, Overlap: CountOverlap(queryTokens, e)))
            .Where(scored => scored.Overlap > 0)
            .OrderByDescending(scored => scored.Overlap)
            .ThenByDescending(scored => scored.Entry.Timestamp)
            .Select(scored => scored.Entry)
            .ToList();
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
    /// a HashSet so callers can do cheap set operations.
    /// </summary>
    private static HashSet<string> TokenizeAndFilter(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(text)) return tokens;

        // Walk the string once instead of regex.Split for speed on the
        // hot path. Non-alphanumeric character ends the current token;
        // alphanumeric extends it.
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
                    tokens.Add(tok);
                start = -1;
            }
        }
        return tokens;
    }

    private static int CountOverlap(HashSet<string> queryTokens, MemoryEntry entry)
    {
        // Build the haystack token bag once per entry from content + key + tags.
        // Re-tokenizing per query is fine at current memory sizes (low-thousands
        // of entries max); switching to a cached token bag is a future
        // optimization if the store grows.
        var haystack = TokenizeAndFilter(entry.Content);
        haystack.UnionWith(TokenizeAndFilter(entry.Key));
        foreach (var tag in entry.Tags)
            haystack.UnionWith(TokenizeAndFilter(tag));

        var overlap = 0;
        foreach (var q in queryTokens)
            if (haystack.Contains(q)) overlap++;
        return overlap;
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
