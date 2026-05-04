namespace GaChatbot.Api.Services;

using GA.Business.Core.Orchestration.Models;
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

public sealed record AgenticTrace(
    string TraceId,
    string Protocol,
    string RunId,
    IReadOnlyList<AgenticTraceStep> Steps);

public sealed record AgenticTraceStep(
    string Name,
    string Status,
    long ElapsedMs,
    IReadOnlyDictionary<string, object?> Attributes);
