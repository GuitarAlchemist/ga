namespace GA.Business.ML.Agents.Memory.Curator;

/// <summary>
/// Header-only interface (v0.1) — the production transcript-capture story is
/// not yet wired. Memory curator depends on this contract so v0.2 can drop in
/// a real implementation without changing curator code.
/// </summary>
/// <remarks>
/// For v0.1 spike runs, tests and the CLI pass a hand-written fixture
/// implementation (e.g. <see cref="FixtureChatTranscriptStore"/>). For v0.2,
/// the production implementation will read from whatever transcript log the
/// chatbot persists per <see cref="Hooks.ChatHookContext"/> session.
/// </remarks>
public interface IChatTranscriptStore
{
    /// <summary>
    /// Returns up to <paramref name="maxSessions"/> recent chat session
    /// transcripts. Order is newest-first. Empty list is a legitimate
    /// answer (no transcripts available — the curator will operate over
    /// the memory store alone).
    /// </summary>
    Task<IReadOnlyList<ChatTranscript>> GetRecentAsync(
        int maxSessions,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Plain transcript record handed to the curator. Order of <see cref="Turns"/>
/// is chronological within a session.
/// </summary>
public sealed record ChatTranscript(
    string SessionId,
    DateTimeOffset StartedAt,
    IReadOnlyList<TranscriptTurn> Turns);

/// <summary>One conversational turn — user or assistant — within a transcript.</summary>
public sealed record TranscriptTurn(string Role, string Content, DateTimeOffset Timestamp);

/// <summary>
/// In-memory fixture for v0.1. Tests and the CLI pre-load this with the
/// transcripts they want the curator to consider.
/// </summary>
public sealed class FixtureChatTranscriptStore(IReadOnlyList<ChatTranscript> transcripts) : IChatTranscriptStore
{
    public Task<IReadOnlyList<ChatTranscript>> GetRecentAsync(
        int maxSessions,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChatTranscript>>(
            transcripts.Take(maxSessions).ToList());
}
