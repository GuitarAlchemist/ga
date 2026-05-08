namespace GA.Business.Core.Orchestration.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;

/// <summary>
/// <see cref="IChatApplicationService"/> decorator that records each
/// orchestration call into the scoped <see cref="IAgenticTraceCapture"/>.
/// Adds no behaviour change — pure observability — so it's safe to enable
/// by default. Innermost decorator in the
/// <c>ReadinessGated → Fallback → Traceable → Harmonic</c> stack so
/// trace coverage extends to every outer behaviour layer.
/// </summary>
/// <remarks>
/// Codex CLI 2026-05-08 design review: keeps <see cref="IChatApplicationService"/>
/// narrow (returns <see cref="ChatResponse"/>) by writing trace through a
/// scoped capture rather than widening the interface or adding a Trace
/// field to <see cref="ChatResponse"/>. Hosts surface the captured trace
/// at the wire boundary (GaApi <c>ChatJsonResponse</c>, SignalR routing
/// payload, AG-UI custom event).
/// </remarks>
public sealed class TraceableChatApplicationService(
    IChatApplicationService inner,
    IAgenticTraceCapture capture) : IChatApplicationService
{
    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        capture.AddStep(
            "chat.request",
            "completed",
            0,
            new Dictionary<string, object?>
            {
                ["gen_ai.operation.name"] = "chat",
                ["history.turn_count"] = request.History?.Count ?? 0,
            });

        using var step = capture.StartStep(
            "orchestration.answer",
            new Dictionary<string, object?>
            {
                ["chat.mode"] = "full",
            });

        var response = await inner.ChatAsync(request, cancellationToken);

        step.Complete(
            finalAttributes: new Dictionary<string, object?>
            {
                ["agent.id"] = response.Routing?.AgentId,
                ["routing.method"] = response.Routing?.RoutingMethod,
                ["routing.confidence"] = response.Routing?.Confidence,
                ["grounding.source"] = response.Grounding?.Source,
                ["grounding.queryType"] = response.Grounding?.QueryType,
                ["candidate.count"] = response.Candidates.Count,
                ["response.length"] = response.NaturalLanguageAnswer?.Length ?? 0,
            });

        return response;
    }
}
