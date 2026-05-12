namespace GA.Business.ML.Agents.Memory.Curator;

/// <summary>
/// In-memory fixture for v0.1 curator runs. Tests and the CLI pre-load
/// this with the transcripts they want the curator to consider.
/// </summary>
/// <remarks>
/// Implements <see cref="IOperatorTranscriptReader"/> — the cross-session
/// reader interface. Use only from operator / CLI contexts.
/// </remarks>
public sealed class FixtureChatTranscriptStore(IReadOnlyList<ChatTranscript> transcripts)
    : IOperatorTranscriptReader
{
    public Task<IReadOnlyList<ChatTranscript>> GetRecentAsync(
        int maxSessions,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ChatTranscript>>(
            transcripts.Take(maxSessions).ToList());
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
