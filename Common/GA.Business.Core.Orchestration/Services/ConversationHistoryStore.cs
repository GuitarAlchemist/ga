namespace GA.Business.Core.Orchestration.Services;

using System.Collections.Concurrent;
using GA.Business.Core.Orchestration.Models;

/// <summary>
/// In-memory conversation history store keyed by session ID.
/// Thread-safe and bounded to <see cref="MaxTurnsPerSession"/> turns per session.
/// </summary>
public sealed class ConversationHistoryStore
{
    private const int MaxTurnsPerSession = 50;
    private readonly ConcurrentDictionary<string, List<ConversationTurn>> _sessions = new();

    /// <summary>
    /// Appends a turn to the session, trimming to <see cref="MaxTurnsPerSession"/>.
    /// </summary>
    public void AddTurn(string sessionId, string role, string content)
    {
        var turns = _sessions.GetOrAdd(sessionId, _ => []);
        lock (turns)
        {
            turns.Add(new ConversationTurn(role, content, DateTimeOffset.UtcNow));
            if (turns.Count > MaxTurnsPerSession)
                turns.RemoveRange(0, turns.Count - MaxTurnsPerSession);
        }
    }

    /// <summary>
    /// Returns the full history for a session (empty list if unknown).
    /// </summary>
    public IReadOnlyList<ConversationTurn> GetHistory(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var turns)) return [];
        lock (turns) { return turns.ToList(); }
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
}
