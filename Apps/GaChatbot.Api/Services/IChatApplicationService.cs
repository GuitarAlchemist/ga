namespace GaChatbot.Api.Services;

using GA.Business.Core.Orchestration.Models;
// AgenticTrace + AgenticTraceStep moved to GA.Business.Core.Orchestration.Trace
// in roadmap P1 #7 commit 1 so GaApi controllers and any future host can produce
// the same wire shape. GaChatbot.Api consumes the moved types here — kept as a
// re-export-via-using rather than aliasing so no GaChatbot.Api caller has to
// change. Codex CLI 2026-05-08 risk-list item 1 (duplicate-type silent miswire).
using GA.Business.Core.Orchestration.Trace;
using GaChatbot.Api.Controllers;

public interface IChatApplicationService
{
    Task<ChatExecutionResult> ChatAsync(ChatExecutionRequest request, CancellationToken cancellationToken = default);

    IAsyncEnumerable<ChatStreamUpdate> ChatStreamAsync(ChatExecutionRequest request, CancellationToken cancellationToken = default);

    Task<ChatbotStatus> GetStatusAsync(CancellationToken cancellationToken = default);
}

/// <param name="Message">The user's current turn.</param>
/// <param name="History">Prior turns supplied by the caller, if any.</param>
/// <param name="SessionId">
/// Opaque per-conversation identifier resolved by the transport layer
/// (<see cref="HttpChatSessionCookie"/> for this host). Flows through to
/// <see cref="GA.Business.Core.Orchestration.Models.ChatRequest.SessionId"/>,
/// which is what scopes <c>MemoryStore</c> reads/writes and
/// <c>ChatTranscriptStore</c> turns to one conversation. <c>null</c> means
/// "no session" — the orchestrator then mints a throwaway per-request ID, so
/// nothing written during the turn is reachable from any later turn.
/// </param>
public sealed record ChatExecutionRequest(
    string Message,
    List<ConversationTurn>? History = null,
    string? SessionId = null);

public sealed record ChatExecutionResult(
    string NaturalLanguageAnswer,
    AgentRoutingMetadata Routing,
    GroundingMetadata? Grounding = null,
    AgenticTrace? Trace = null);

public sealed record ChatStreamUpdate(
    string? Chunk = null,
    AgentRoutingMetadata? Routing = null,
    GroundingMetadata? Grounding = null,
    AgenticTrace? Trace = null,
    bool IsCompleted = false);
