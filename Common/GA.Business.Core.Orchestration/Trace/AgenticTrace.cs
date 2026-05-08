namespace GA.Business.Core.Orchestration.Trace;

/// <summary>
/// Ordered, per-request, wire-visible trace of a chat run. Distinct from
/// OpenTelemetry spans (which are backend-telemetry and unordered as far as
/// consumers are concerned) — this record is emitted on the JSON / SSE wire
/// so frontends and operators can render the orchestration timeline.
/// </summary>
/// <param name="TraceId">W3C trace-context ID when an Activity is current,
/// otherwise the per-request <paramref name="RunId"/>.</param>
/// <param name="Protocol">Identifier of the trace shape — pinned to
/// <c>"w3c-trace-context+otel-genai+ag-ui"</c> so wire consumers can detect
/// shape drift. Bump if the AgenticTrace fields ever change incompatibly.</param>
/// <param name="RunId">Stable per-request id (typically <c>run_&lt;guid&gt;</c>)
/// for correlating client and server logs.</param>
/// <param name="Steps">Step list in chronological order.</param>
/// <remarks>
/// Moved from <c>Apps/GaChatbot.Api/Services/IChatApplicationService.cs</c>
/// to <c>Common/GA.Business.Core.Orchestration/Trace/</c> so GaApi controllers
/// and any future host can produce / consume the same shape. Roadmap P1 #7
/// commit 1; codex CLI design review 2026-05-08.
/// </remarks>
public sealed record AgenticTrace(
    string TraceId,
    string Protocol,
    string RunId,
    IReadOnlyList<AgenticTraceStep> Steps);

/// <summary>
/// One step in an <see cref="AgenticTrace"/>. Maps loosely to a logical
/// orchestration phase (request received, routing decided, orchestrator
/// invoked, fallback evaluated, response sent).
/// </summary>
/// <param name="Name">Step identifier — by convention dotted lowercase
/// (<c>chat.request</c>, <c>orchestration.answer</c>,
/// <c>orchestration.fallback</c>, <c>readiness.check</c>).</param>
/// <param name="Status"><c>completed</c> | <c>error</c> | <c>skipped</c> |
/// <c>blocked</c>. Used by frontends to render success/failure markers.</param>
/// <param name="ElapsedMs">Wall-clock duration of the step.</param>
/// <param name="Attributes">Free-form key-value tags. Conventionally OTEL-style
/// dotted keys (<c>agent.id</c>, <c>routing.method</c>, <c>tool.failure_reason</c>,
/// <c>fallback.reason</c>).</param>
public sealed record AgenticTraceStep(
    string Name,
    string Status,
    long ElapsedMs,
    IReadOnlyDictionary<string, object?> Attributes);
