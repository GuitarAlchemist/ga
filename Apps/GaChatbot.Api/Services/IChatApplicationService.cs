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

public sealed record ChatExecutionRequest(
    string Message,
    List<ConversationTurn>? History = null);

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
