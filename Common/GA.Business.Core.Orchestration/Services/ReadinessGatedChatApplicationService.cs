namespace GA.Business.Core.Orchestration.Services;

using System.Diagnostics;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;

/// <summary>
/// <see cref="IChatApplicationService"/> decorator that pre-checks the
/// configured <see cref="IChatReadinessProbe"/> and short-circuits when the
/// chat surface isn't ready — returns a deterministic "service-not-ready"
/// <see cref="ChatResponse"/> instead of waiting for the orchestrator's own
/// timeout. Stacks over the trace decorator so the
/// <c>readiness.check</c> step lands inside the request's
/// <see cref="AgenticTrace"/>.
/// </summary>
/// <remarks>
/// Pinned safety guarantees from the codex CLI 2026-05-08 design review:
/// <list type="bullet">
///   <item>Always emits a <c>readiness.check</c> step (status <c>completed</c>
///   when ready, <c>blocked</c> when not) so dashboards can count blocked
///   requests independently of how the orchestrator behaved.</item>
///   <item>When blocked, returns confidence 0 and routing method
///   <c>readiness-blocked</c> — never claims a grounded answer.</item>
///   <item>Probe failures bubble through as not-ready rather than throwing,
///   so a buggy host probe can't take down chat.</item>
/// </list>
/// Roadmap P1 #7 commit 2/3.
/// </remarks>
public sealed class ReadinessGatedChatApplicationService(
    IChatApplicationService inner,
    IChatReadinessProbe probe,
    IAgenticTraceCapture capture) : IChatApplicationService
{
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        ChatReadinessResult readiness;
        try
        {
            readiness = await probe.CheckAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Probe threw — fail closed (not ready) but don't propagate the
            // exception. A wedged probe shouldn't take chat down; better to
            // return a fast service-unavailable than a 500.
            readiness = new ChatReadinessResult(
                IsReady: false,
                Reason: $"readiness probe threw {ex.GetType().Name} — failing closed");
        }
        sw.Stop();

        capture.AddStep(
            "readiness.check",
            readiness.IsReady ? "completed" : "blocked",
            sw.ElapsedMilliseconds,
            new Dictionary<string, object?>
            {
                ["readiness.is_ready"] = readiness.IsReady,
                ["readiness.reason"] = readiness.Reason,
            });

        if (readiness.IsReady)
        {
            return await inner.ChatAsync(request, cancellationToken);
        }

        // Short-circuit: deterministic, low-confidence, no grounding,
        // routing method named so dashboards can split readiness-blocked
        // out of regular agent-routed traffic.
        return new ChatResponse(
            NaturalLanguageAnswer:
                "The chatbot can't serve a request right now. " +
                $"Reason: {readiness.Reason}",
            Candidates: [],
            Routing: new AgentRoutingMetadata(
                AgentId: "readiness",
                Confidence: 0f,
                RoutingMethod: "readiness-blocked"));
    }
}
