namespace GA.Business.Core.Orchestration.Abstractions;

/// <summary>
/// Pre-orchestration readiness check. The
/// <see cref="GA.Business.Core.Orchestration.Services.ReadinessGatedChatApplicationService"/>
/// decorator calls this before delegating to the orchestrator, so a
/// known-unhealthy provider returns fast (sub-second) instead of waiting
/// for the orchestrator's own timeout.
/// </summary>
/// <remarks>
/// Intentionally narrow — captures only the binary "should we proceed?"
/// signal plus a human-readable reason. Hosts already have richer status
/// surfaces (e.g. <c>GaChatbot.Api.Services.IChatProviderReadinessProbe</c>
/// returning <c>ChatbotStatus</c>) for their <c>/status</c> endpoints; that
/// shape would drag GaChatbot-specific DTOs into the layer-5 boundary.
/// Hosts that want both can register an adapter that implements this
/// interface in terms of their richer probe.
///
/// The default implementation in
/// <see cref="GA.Business.Core.Orchestration.Services.PermissiveChatReadinessProbe"/>
/// always reports ready. Roadmap P1 #7 commit 2/3.
/// </remarks>
public interface IChatReadinessProbe
{
    /// <summary>Quick check; must complete in well under a second.</summary>
    Task<ChatReadinessResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a readiness check.
/// </summary>
/// <param name="IsReady">True when orchestration should proceed.</param>
/// <param name="Reason">Human-readable rationale, surfaced into trace and
/// short-circuit responses. Operators read this in dashboards.</param>
public sealed record ChatReadinessResult(bool IsReady, string Reason);
