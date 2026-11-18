namespace GA.Business.Core.Orchestration.Abstractions;

using GA.Business.Core.Orchestration.Models;

/// <summary>
/// One event of a chat run, as <see cref="IChatIntake.RunAsync"/> yields it. A run that
/// completes yields one <see cref="Routing"/>, then any number of <see cref="TextDelta"/>,
/// then one <see cref="Completed"/>. A rejected request yields a lone <see cref="Failed"/>,
/// and a run that fails part way ends with <see cref="Failed"/> instead of
/// <see cref="Completed"/>. <see cref="Routing"/> always comes before the first
/// <see cref="TextDelta"/>.
/// </summary>
/// <remarks>
/// A closed union: the constructor is private, so only the nested cases exist. Plain
/// records with no transport types, so the SSE, AG-UI and SignalR adapters only map each
/// event to their wire format. Every event carries the run's id. There are no sequence
/// numbers and no replay yet: a broker-backed run (resume after disconnect, several GaApi
/// instances, background runs) would add them behind the same seam.
/// </remarks>
public abstract record ChatEvent
{
    private ChatEvent(string runId) => RunId = runId;

    /// <summary>The id shared by every event of one run.</summary>
    public string RunId { get; }

    /// <summary>
    /// The routing decision known when the answer starts. <see cref="Completed"/> carries
    /// the final metadata, which can differ: the fallback decorator clamps the confidence
    /// of a deterministic failure to 0 after its text was sent.
    /// </summary>
    public sealed record Routing(
        string RunId,
        string AgentId,
        float Confidence,
        string Method,
        GroundingMetadata? Grounding) : ChatEvent(RunId);

    /// <summary>A piece of the answer text, in order.</summary>
    public sealed record TextDelta(string RunId, string Text) : ChatEvent(RunId);

    /// <summary>The run finished; <paramref name="Response"/> holds the whole answer and its metadata.</summary>
    public sealed record Completed(string RunId, ChatResponse Response) : ChatEvent(RunId);

    /// <summary>
    /// The run ended without an answer. <paramref name="Rejection"/> is set when the intake
    /// refused the request (validation, busy gate); it is null when the pipeline failed
    /// after it started, and <paramref name="Message"/> then describes that error for logs.
    /// </summary>
    public sealed record Failed(string RunId, string Message, ChatIntakeError? Rejection = null) : ChatEvent(RunId);
}
