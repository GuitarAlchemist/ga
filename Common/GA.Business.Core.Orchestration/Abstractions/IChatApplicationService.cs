namespace GA.Business.Core.Orchestration.Abstractions;

using GA.Business.Core.Orchestration.Models;

/// <summary>
/// Host-neutral application service for chatbot interactions. The single
/// supported entry point GaApi controllers / hubs / future hosts call into,
/// abstracting away whether the underlying orchestrator is
/// <see cref="GA.Business.Core.Orchestration.Services.ProductionOrchestrator"/>
/// (today) or a different implementation later.
/// </summary>
/// <remarks>
/// Distinct from <see cref="IHarmonicChatOrchestrator"/> by intent: the
/// orchestrator owns the routing/RAG/skill-dispatch pipeline; the
/// application service is a thin consumer-facing wrapper that any host can
/// take a dependency on. Future readiness probing, trace assembly, and
/// fallback behaviour belong here, not on hosts. Codex CLI second-opinion
/// 2026-05-07 — roadmap P0 #2 first cut, option C-prime.
///
/// Note: <see cref="GaChatbot.Api.Services.IChatApplicationService"/> is a
/// separate, GaChatbot.Api-specific interface that bundles richer wire
/// (Trace, readiness, ChatExecutionResult) and stays frozen until a
/// concrete deploy reason emerges. This contract is deliberately the
/// narrowest possible alternative.
/// </remarks>
public interface IChatApplicationService
{
    /// <summary>
    /// Process a chat request through the full orchestration pipeline.
    /// </summary>
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken cancellationToken = default);
}
