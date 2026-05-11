namespace GA.Business.ML.Agents.Memory;

using System.Collections.Concurrent;
using System.Text.Json;
using Curator;
using Microsoft.Extensions.Logging;

/// <summary>
/// File-backed store for chat transcript turns. Sibling to
/// <see cref="MemoryStore"/> — same atomic-write + session-scoping pattern,
/// but for *transient* per-turn chat content rather than *durable* memory.
/// Implements <see cref="IChatTranscriptStore"/> so the memory curator can
/// consume real transcripts in Phase 2 of the curator architecture.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists (PR #172 audit):</b> <c>MemoryHook.OnResponseSent</c>
/// has historically written <c>type=response</c> entries directly into
/// <see cref="MemoryStore"/>. That conflates transcript logs with durable
/// memory — see
/// <c>docs/solutions/architecture/2026-05-11-memoryhook-conflates-transcript-log-with-durable-memory.md</c>.
/// Phase 1 (this file) provides the new home for those entries; Phase 2
/// (a separate PR) rewires <c>MemoryHook</c> to write here instead.
/// </para>
/// <para>
/// <b>Data model:</b> each <see cref="AppendTurn"/> call records ONE turn
/// (user OR assistant) with its session id, role, content, and timestamp.
/// <see cref="GetRecentAsync"/> groups turns by session id, picks the
/// most-recent N sessions by their newest turn timestamp, and returns each
/// as a <see cref="ChatTranscript"/> with turns in chronological order.
/// </para>
/// <para>
/// <b>Eviction:</b> bounded at <see cref="DefaultMaxTurns"/> total turns.
/// When over budget, evicts the oldest 10% by Timestamp. The cap is
/// deliberately higher than <see cref="MemoryStore.DefaultMaxEntries"/>
/// because transcripts are much higher-volume — every chat turn writes
/// one row.
/// </para>
/// </remarks>
public sealed class ChatTranscriptStore : IChatTranscriptStore
{
    /// <summary>Default on-disk location: <c>~/.ga/transcripts.json</c>.</summary>
    public static readonly string DefaultStorePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ga", "transcripts.json");

    /// <summary>
    /// Soft cap on the total number of turns. Higher than
    /// <see cref="MemoryStore.DefaultMaxEntries"/> because transcripts are
    /// per-turn write-heavy; tune via the constructor in tests.
    /// </summary>
    public const int DefaultMaxTurns = 50_000;

    private readonly string _storePath;
    private readonly int _maxTurns;
    private readonly ILogger<ChatTranscriptStore>? _logger;
    private readonly ConcurrentDictionary<string, TranscriptTurnEntry> _turns;
    private readonly SemaphoreSlim _saveLock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Production constructor — backs the store with the default on-disk location.</summary>
    public ChatTranscriptStore() : this(DefaultStorePath, DefaultMaxTurns, logger: null) { }

    /// <summary>Testable constructor — explicit store path; default cap.</summary>
    public ChatTranscriptStore(string storePath) : this(storePath, DefaultMaxTurns, logger: null) { }

    /// <summary>DI-friendly constructor — default path + logger.</summary>
    public ChatTranscriptStore(ILogger<ChatTranscriptStore> logger) : this(DefaultStorePath, DefaultMaxTurns, logger) { }

    /// <summary>Full constructor — explicit store path, cap, and optional logger.</summary>
    public ChatTranscriptStore(string storePath, int maxTurns, ILogger<ChatTranscriptStore>? logger)
    {
        _storePath = storePath ?? throw new ArgumentNullException(nameof(storePath));
        _maxTurns  = maxTurns > 0 ? maxTurns : throw new ArgumentOutOfRangeException(nameof(maxTurns));
        _logger    = logger;
        _turns     = Load(_storePath, logger);
    }

    /// <summary>Whitelisted role values per the chat-message convention.</summary>
    /// <remarks>
    /// PR #173 security review (Sec-M1) — accepting arbitrary role strings
    /// is a prompt-injection vector once curator prompts concatenate
    /// <c>TranscriptTurn.Role</c> back in. Reject anything outside the
    /// closed set at write time.
    /// </remarks>
    public static readonly IReadOnlySet<string> AllowedRoles =
        new HashSet<string>(StringComparer.Ordinal) { "user", "assistant", "system" };

    /// <summary>
    /// Records one conversational turn. Generates a stable internal id so
    /// concurrent writes from the same session don't collide. The
    /// <paramref name="role"/> must be one of <see cref="AllowedRoles"/>
    /// — rejecting unknown values closes a prompt-injection vector flagged
    /// by the PR #173 security review (Sec-M1).
    /// </summary>
    public void AppendTurn(string sessionId, string role, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        ArgumentNullException.ThrowIfNull(content);

        if (!AllowedRoles.Contains(role))
            throw new ArgumentException(
                $"Role '{role}' is not allowed. Whitelist: {string.Join(", ", AllowedRoles)}. " +
                "Accepting arbitrary role strings is a prompt-injection vector once curator " +
                "prompts concatenate Role back into their system context.",
                nameof(role));

        var entry = new TranscriptTurnEntry(
            Id:        Guid.NewGuid().ToString("N"),
            SessionId: sessionId,
            Role:      role,
            Content:   content,
            Timestamp: DateTimeOffset.UtcNow);

        _turns[entry.Id] = entry;
        EnforceCap();
        Save();
    }

    /// <summary>
    /// Returns the most recently active <paramref name="maxSessions"/>
    /// transcripts across <b>all</b> sessions in the store, newest first.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Cross-session by design — operator-only consumer (PR #173 review
    /// Sec-H1):</b> this method is NOT scoped to any one SessionId. The
    /// memory curator consumes it from an operator/CLI context to surface
    /// emergent patterns across many user conversations. If you wire a
    /// chat-runtime caller to this method, you've introduced a cross-
    /// session content-leak vector — the same defect class that PR #157
    /// closed for MemoryStore. Callers that need a single user's
    /// transcripts must filter the result by SessionId or use a future
    /// per-session helper.
    /// </para>
    /// </remarks>
    public Task<IReadOnlyList<ChatTranscript>> GetRecentAsync(
        int maxSessions,
        CancellationToken cancellationToken = default)
    {
        if (maxSessions <= 0)
            return Task.FromResult<IReadOnlyList<ChatTranscript>>([]);

        // Group turns by session, sort sessions by their newest turn's
        // timestamp (newest first, ties broken by SessionId for
        // determinism — PR #173 review CR-M2), take N, then convert each
        // group into a ChatTranscript with turns in chronological order.
        var sessions = _turns.Values
            .GroupBy(t => t.SessionId, StringComparer.Ordinal)
            .Select(g => new
            {
                SessionId = g.Key,
                Turns     = g.OrderBy(t => t.Timestamp).ToList(),
                Newest    = g.Max(t => t.Timestamp),
            })
            .OrderByDescending(s => s.Newest)
            .ThenBy(s => s.SessionId, StringComparer.Ordinal)
            .Take(maxSessions)
            .Select(s => new ChatTranscript(
                SessionId: s.SessionId,
                StartedAt: s.Turns[0].Timestamp,
                Turns:     s.Turns.Select(t => new TranscriptTurn(t.Role, t.Content, t.Timestamp)).ToList()))
            .ToList();

        return Task.FromResult<IReadOnlyList<ChatTranscript>>(sessions);
    }

    /// <summary>Total number of turns currently stored (across all sessions). Diagnostic.</summary>
    public int TurnCount => _turns.Count;

    /// <summary>Distinct session count. Diagnostic — does not load anything new.</summary>
    public int SessionCount =>
        _turns.Values.Select(t => t.SessionId).ToHashSet(StringComparer.Ordinal).Count;

    // ── Eviction ─────────────────────────────────────────────────────────

    private void EnforceCap()
    {
        if (_turns.Count <= _maxTurns) return;

        // Evict oldest 10% so this work amortises rather than running per-write.
        var evictTarget = _turns.Count - (_maxTurns * 9 / 10);
        var oldest = _turns.Values
            .OrderBy(t => t.Timestamp)
            .Take(evictTarget)
            .Select(t => t.Id)
            .ToList();
        foreach (var id in oldest)
            _turns.TryRemove(id, out _);

        _logger?.LogInformation(
            "ChatTranscriptStore: evicted {Count} oldest turns to stay under cap of {Max}.",
            oldest.Count, _maxTurns);
    }

    // ── Persistence ──────────────────────────────────────────────────────

    private void Save()
    {
        var dir = Path.GetDirectoryName(_storePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        // Snapshot under the lock so concurrent AppendTurns can't tear the
        // serialise; atomic-rename so a crash mid-write can't leave a
        // half-flushed file that Load() silently swallows on next boot.
        // Same pattern as MemoryStore.Save (PR #151 rel-006 fix).
        _saveLock.Wait();
        try
        {
            var json    = JsonSerializer.Serialize(_turns.Values.ToList(), JsonOpts);
            var tmpPath = _storePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, _storePath, overwrite: true);
        }
        finally
        {
            _saveLock.Release();
        }
    }

    private static ConcurrentDictionary<string, TranscriptTurnEntry> Load(
        string storePath, ILogger<ChatTranscriptStore>? logger)
    {
        var dict = new ConcurrentDictionary<string, TranscriptTurnEntry>();
        if (!File.Exists(storePath)) return dict;

        try
        {
            var json = File.ReadAllText(storePath);
            var entries = JsonSerializer.Deserialize<List<TranscriptTurnEntry>>(json, JsonOpts);
            if (entries is not null)
            {
                foreach (var e in entries) dict[e.Id] = e;
            }
        }
        catch (JsonException jex)
        {
            // PR #173 review (CR-H1 / Sec-L2): mirror MemoryStore's
            // quarantine-on-corruption pattern. Without rename, the very
            // next Save() atomic-rename overwrites the corrupt bytes and
            // we lose them for postmortem. Renaming preserves forensics
            // and makes the loss visible to the operator.
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
            logger?.LogWarning(jex,
                "ChatTranscriptStore: {Path} contained invalid JSON — quarantined as {Corrupt} and starting fresh.",
                storePath, corruptPath ?? "(rename failed)");
        }
        catch (Exception ex)
        {
            // PR #173 review (CR-H2): MemoryStore catches a bare Exception
            // after JsonException so SecurityException / PathTooLongException
            // / NotSupportedException don't crash construction. Mirror that:
            // start fresh on any IO-class error, log loudly so it's visible.
            logger?.LogWarning(ex,
                "ChatTranscriptStore: failed to read {Path}; starting with empty store. " +
                "Common causes: file permissions, locked-by-other-process, disk full, " +
                "path too long, security policy. Investigate before writing more turns.",
                storePath);
        }
        return dict;
    }
}

/// <summary>
/// One persisted transcript turn. Internal storage shape — the curator-
/// facing <see cref="TranscriptTurn"/> record (defined in
/// <c>IChatTranscriptStore.cs</c>) is what callers see.
/// </summary>
/// <param name="Id">Unique per-turn key, generated at write time. Used to
/// dedupe across concurrent Saves.</param>
/// <param name="SessionId">Session scope — typically a SignalR
/// <c>ConnectionId</c> or an HTTP session cookie. Never null (validated in
/// <see cref="ChatTranscriptStore.AppendTurn"/>).</param>
/// <param name="Role">Chat-message role: <c>"user"</c>, <c>"assistant"</c>,
/// or rarely <c>"system"</c>.</param>
/// <param name="Content">Turn text. May be truncated by the caller; this
/// store doesn't enforce a max-length. <b>Empty content is valid and
/// meaningful</b> — an assistant turn with only a tool-call result, or a
/// user turn that's whitespace-only after sanitization, can legitimately
/// have an empty payload. Curator authors must NOT filter empty content
/// out as "noise" — it's a structural placeholder (PR #173 review CR-L1).</param>
/// <param name="Timestamp">UTC write time. Used for newest-first session
/// ordering in <see cref="ChatTranscriptStore.GetRecentAsync"/>.</param>
public sealed record TranscriptTurnEntry(
    string Id,
    string SessionId,
    string Role,
    string Content,
    DateTimeOffset Timestamp);
