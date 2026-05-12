namespace GA.Business.ML.Agents.Memory;

using Curator;

/// <summary>
/// Operator-only reader over the transcript store. Exposes
/// <see cref="GetRecentAsync"/>, which returns the most recently active
/// sessions <b>across ALL sessions in the store</b>. This cross-session
/// view is by design for the memory curator and similar offline tooling;
/// it MUST NOT be consumed from a chat-runtime path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this interface exists (2026-05-12, PR #174 follow-up):</b> the
/// cross-session semantics of <see cref="GetRecentAsync"/> are a
/// content-leak vector if a chat-runtime caller wires it up — reading
/// "recent transcripts" inside an in-flight chat request would leak other
/// users' conversations into the calling user's prompt. The same defect
/// class that PR #157 closed for <see cref="MemoryStore"/>.
/// </para>
/// <para>
/// The type name carries the contract. Anyone reaching for
/// <c>IOperatorTranscriptReader</c> in a chat-runtime code path now has
/// to acknowledge they're outside per-session scope — and at that point
/// the right answer is to file an issue for a per-session reader, not to
/// bypass the boundary.
/// </para>
/// <para>
/// <b>Legitimate consumers:</b> <c>GaMemoryCli</c>'s curator command, the
/// memory curator background job, future ops tooling that needs to see
/// recent activity across users for quality / abuse / sentiment analysis.
/// </para>
/// <para>
/// <b>Illegitimate consumers:</b> anything that runs inside
/// <c>ProductionOrchestrator.ProcessAsync</c> or its hook chain.
/// </para>
/// <para>
/// <b>History:</b> renamed from <c>IChatTranscriptStore</c> on 2026-05-12.
/// The prior name suggested "a store for transcripts" — neutral enough
/// that a future contributor could have plausibly added it to a hook's
/// constructor. The new name is intentionally ugly to discourage that.
/// </para>
/// </remarks>
public interface IOperatorTranscriptReader
{
    /// <summary>
    /// Returns up to <paramref name="maxSessions"/> recent chat session
    /// transcripts. Order is newest-first across <b>all</b> sessions in
    /// the store. Empty list is a legitimate answer (no transcripts
    /// available — the curator will operate over the memory store alone).
    /// </summary>
    Task<IReadOnlyList<ChatTranscript>> GetRecentAsync(
        int maxSessions,
        CancellationToken cancellationToken = default);
}
