namespace GA.Business.Core.Orchestration.Services;

using System.Collections.Concurrent;
using GA.Business.Core.Orchestration.Models;

/// <summary>
/// In-memory conversation history store keyed by session ID.
/// Thread-safe, bounded per session and across sessions.
/// </summary>
/// <remarks>
/// Two bounds:
/// <list type="bullet">
///   <item><see cref="MaxTurnsPerSession"/> caps turns kept per session.</item>
///   <item><see cref="MaxSessions"/> caps total sessions; on overflow the oldest
///   <see cref="EvictionBatch"/> sessions by last-access timestamp are dropped.</item>
/// </list>
/// Without the session cap, a long-running process accumulates state for every
/// distinct sessionId it has ever observed — clients that mint fresh IDs per
/// page-load grow the dictionary without limit.
/// </remarks>
public sealed class ConversationHistoryStore
{
    private const int MaxTurnsPerSession = 50;
    private const int MaxSessions = 1000;
    private const int EvictionBatch = 100;

    private sealed class SessionEntry
    {
        public List<ConversationTurn> Turns { get; } = [];
        public long LastAccessTicks;
    }

    private readonly ConcurrentDictionary<string, SessionEntry> _sessions = new();
    private readonly Lock _evictionLock = new();

    /// <summary>
    /// Appends a turn to the session, trimming to <see cref="MaxTurnsPerSession"/>.
    /// </summary>
    public void AddTurn(string sessionId, string role, string content)
    {
        var entry = _sessions.GetOrAdd(sessionId, _ => new SessionEntry());
        Interlocked.Exchange(ref entry.LastAccessTicks, DateTimeOffset.UtcNow.UtcTicks);

        lock (entry.Turns)
        {
            entry.Turns.Add(new ConversationTurn(role, content, DateTimeOffset.UtcNow));
            if (entry.Turns.Count > MaxTurnsPerSession)
                entry.Turns.RemoveRange(0, entry.Turns.Count - MaxTurnsPerSession);
        }

        EvictIfOverCapacity();
    }

    /// <summary>
    /// Returns the full history for a session (empty list if unknown).
    /// </summary>
    public IReadOnlyList<ConversationTurn> GetHistory(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var entry)) return [];
        Interlocked.Exchange(ref entry.LastAccessTicks, DateTimeOffset.UtcNow.UtcTicks);
        lock (entry.Turns) { return entry.Turns.ToList(); }
    }

    /// <summary>
    /// Formats the most recent <paramref name="maxTurns"/> as a context string for LLM injection.
    /// </summary>
    public string FormatAsContext(string sessionId, int maxTurns = 10)
    {
        var history = GetHistory(sessionId);
        if (history.Count == 0) return string.Empty;

        var recent = history.TakeLast(maxTurns);
        return string.Join("\n", recent.Select(t => $"[{t.Role}]: {t.Content}"));
    }

    private void EvictIfOverCapacity()
    {
        if (_sessions.Count <= MaxSessions) return;

        // Serialize eviction so concurrent overflow on hot paths doesn't double-evict
        // or thrash. The check above is racy by design — entering the lock revalidates.
        lock (_evictionLock)
        {
            if (_sessions.Count <= MaxSessions) return;

            var victims = _sessions
                .Select(kvp => (kvp.Key, Ticks: Interlocked.Read(ref kvp.Value.LastAccessTicks)))
                .OrderBy(t => t.Ticks)
                .Take(EvictionBatch)
                .Select(t => t.Key)
                .ToList();

            foreach (var key in victims)
                _sessions.TryRemove(key, out _);
        }
    }
}
