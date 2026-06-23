namespace GA.Business.Core.Orchestration.Abstractions;

using GA.Business.Core.Orchestration.Models;
using GA.Core.Functional;

/// <summary>
/// The chat intake seam (campaign slice #1). The single host-neutral entry every
/// GaApi chat transport crosses: it concentrates <c>validate → concurrency-gate →
/// orchestrate</c> on an <b>opaque</b> session id and hands back a typed outcome the
/// transport frames. It never touches <c>HttpContext</c> and never emits an SSE byte —
/// session-id <i>resolution</i> (HTTP cookie / SignalR ConnectionId) and response
/// framing stay in the per-transport thin adapter.
/// </summary>
/// <remarks>
/// Supersedes the thin <see cref="IChatApplicationService"/> wrapper as the surface
/// adapters depend on; the wrapper is removed once all three GaApi transports route
/// through this seam (PR D). See <c>docs/adr/0005-gaapi-single-canonical-chat-host.md</c>
/// and the <c>#1</c> section of <c>docs/plans/2026-06-21-arch-deepening-campaign-plan.md</c>.
/// Streaming (<c>IntakeStreamingAsync</c>) lands with the AG-UI migration (PR C) — added
/// only when a streaming transport routes through the seam, not speculatively.
/// </remarks>
public interface IChatIntake
{
    /// <summary>
    /// Validate, gate, and dispatch a non-streaming chat request. The success value is
    /// the orchestrator's <see cref="ChatResponse"/>; the failure value distinguishes a
    /// rejected request from a busy gate so the adapter can frame each correctly.
    /// </summary>
    Task<Result<ChatResponse, ChatIntakeError>> IntakeAsync(
        ChatIntakeRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Transport-neutral chat request handed to <see cref="IChatIntake"/>. <paramref name="SessionId"/>
/// is opaque: the adapter resolves it (HTTP cookie / SignalR ConnectionId) and the seam
/// only forwards it.
/// </summary>
public sealed record ChatIntakeRequest(string Message, string? SessionId = null);

/// <summary>
/// Closed outcome of a rejected intake. The adapter maps each case to its wire shape
/// (e.g. <see cref="Validation"/> → HTTP 400 / SSE error frame; <see cref="Busy"/> →
/// HTTP 503 / <c>RUN_ERROR</c> / SignalR <c>Error</c>).
/// </summary>
public abstract record ChatIntakeError
{
    private ChatIntakeError() { }

    /// <summary>The request was malformed (e.g. empty message).</summary>
    public sealed record Validation(string Reason) : ChatIntakeError;

    /// <summary>The concurrency gate was full; the caller should retry shortly.</summary>
    public sealed record Busy : ChatIntakeError;
}
