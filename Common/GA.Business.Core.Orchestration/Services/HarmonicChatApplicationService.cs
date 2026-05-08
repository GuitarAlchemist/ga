namespace GA.Business.Core.Orchestration.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;

/// <summary>
/// Host-neutral implementation of <see cref="IChatApplicationService"/>
/// that delegates straight through to <see cref="IHarmonicChatOrchestrator"/>
/// (= <see cref="ProductionOrchestrator"/> in production wiring). Intentionally
/// thin: holds no readiness, fallback, or trace logic so every host gets
/// identical behaviour and there's exactly one composition root for
/// orchestration features.
/// </summary>
/// <remarks>
/// The pass-through is the point. If a host wants richer wire (e.g. the
/// fallback + trace assembly that <c>GaChatbot.Api.Services.OrchestratedChat
/// ApplicationService</c> already does), it can stack a decorator on this
/// service rather than fork the contract — keeping the chat surface
/// canonical across hosts. Codex CLI second-opinion 2026-05-07 — roadmap
/// P0 #2 first cut.
/// </remarks>
public sealed class HarmonicChatApplicationService(IHarmonicChatOrchestrator orchestrator) : IChatApplicationService
{
    public Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default) =>
        orchestrator.AnswerAsync(request, cancellationToken);
}
