namespace GA.Business.Core.Orchestration.Trace;

/// <summary>
/// Per-request trace accumulator. Decorators on <c>IChatApplicationService</c>
/// (<c>TraceableChatApplicationService</c>, <c>FallbackChatApplicationService</c>,
/// <c>ReadinessGatedChatApplicationService</c>) write steps into the capture;
/// the host controller pulls the assembled <see cref="AgenticTrace"/> out at
/// the wire boundary via <see cref="Build"/>.
/// </summary>
/// <remarks>
/// <para>
/// Scoped lifetime — one capture per request. Crossing a scope boundary loses
/// the in-progress steps, which is intentional: a per-process singleton would
/// interleave traces from concurrent requests.
/// </para>
/// <para>
/// Codex CLI 2026-05-08 design review explicitly chose this shape over either
/// (a) widening <c>IChatApplicationService</c> to carry trace in a result envelope
/// or (b) Activity-tags-only. Reasoning: <c>AgenticTrace</c> is wire-visible and
/// chronological; OTEL spans are backend telemetry and not ordered for clients.
/// </para>
/// </remarks>
public interface IAgenticTraceCapture
{
    /// <summary>Stable per-request id; correlates client and server logs.</summary>
    string RunId { get; }

    /// <summary>
    /// Append a synchronous step. Use for events with a known elapsed time
    /// already measured (e.g. response.length measured after the work is
    /// done). Prefer <see cref="StartStep"/> for in-progress timing.
    /// </summary>
    void AddStep(
        string name,
        string status,
        long elapsedMs,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>
    /// Open a step that auto-completes on dispose. Pass the timed-step result
    /// to the inner orchestration call; call <see cref="ITimedStep.Complete"/>
    /// with final attributes to merge them into the step record.
    /// </summary>
    ITimedStep StartStep(
        string name,
        IReadOnlyDictionary<string, object?>? attributes = null);

    /// <summary>Snapshot the current trace. Safe to call mid-request.</summary>
    AgenticTrace Build();
}

/// <summary>
/// In-progress step. Disposed via <c>using</c> so the wall-clock timer stops
/// even if an exception throws inside the step's body. Calling
/// <see cref="Complete"/> explicitly with final attributes merges them into
/// the step record before it lands in the trace.
/// </summary>
public interface ITimedStep : IDisposable
{
    void Complete(
        string status = "completed",
        IReadOnlyDictionary<string, object?>? finalAttributes = null);
}
