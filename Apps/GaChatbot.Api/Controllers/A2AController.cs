namespace GaChatbot.Api.Controllers;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.Core.Orchestration.Trace;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
public sealed class A2AController(
    IConfiguration configuration,
    ILlmConcurrencyGate concurrencyGate,
    ConversationHistoryStore conversationHistoryStore,
    IChatApplicationService chatApplicationService) : ControllerBase
{
    private const string JsonRpcVersion = "2.0";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpGet("/.well-known/agent-card.json")]
    [ProducesResponseType(typeof(A2AAgentCard), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<A2AAgentCard> GetAgentCard()
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        var baseUri = $"{Request.Scheme}://{Request.Host}";
        return Ok(new A2AAgentCard(
            ProtocolVersion: "0.2.5",
            Name: "Guitar Alchemist Chatbot",
            Description: "Grounded music theory, fretboard, harmony, progression, and set-class analysis agent.",
            Url: $"{baseUri}/a2a",
            PreferredTransport: "JSONRPC",
            Capabilities: new A2AAgentCapabilities(Streaming: true),
            DefaultInputModes: ["text/plain"],
            DefaultOutputModes: ["text/plain"],
            Skills:
            [
                new A2AAgentSkill(
                    Id: "music-theory-chat",
                    Name: "Music theory chat",
                    Description: "Answers grounded guitar, harmony, scale, voicing, progression, and set-class questions.",
                    Tags: ["music-theory", "guitar", "harmony", "fretboard", "set-class"],
                    Examples:
                    [
                        "Explain voice leading in jazz.",
                        "Are 0146 and 0137 z-related?",
                        "Generate a mellow ii-V-I in C."
                    ])
            ]));
    }

    [HttpPost("/a2a")]
    [ProducesResponseType(typeof(A2AJsonRpcResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Invoke([FromBody] A2AJsonRpcRequest request, CancellationToken cancellationToken)
    {
        if (!IsEnabled())
        {
            return NotFound();
        }

        if (!string.Equals(request.JsonRpc, JsonRpcVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(request.Method))
        {
            return Ok(A2AJsonRpcResponse.Fail(request.Id, A2AJsonRpcError.InvalidRequest()));
        }

        if (string.Equals(request.Method, "message/stream", StringComparison.Ordinal))
        {
            if (!TryExtractUserMessage(request.Params, out var streamingMessage, out var streamingContext, out var streamingParamsError))
            {
                return Ok(A2AJsonRpcResponse.Fail(request.Id, streamingParamsError));
            }

            await StreamMessageAsync(request, streamingMessage, streamingContext, cancellationToken);
            return new EmptyResult();
        }

        if (!string.Equals(request.Method, "message/send", StringComparison.Ordinal))
        {
            return Ok(A2AJsonRpcResponse.Fail(request.Id, A2AJsonRpcError.MethodNotFound(request.Method)));
        }

        if (!TryExtractUserMessage(request.Params, out var message, out var inboundContext, out var invalidParamsError))
        {
            return Ok(A2AJsonRpcResponse.Fail(request.Id, invalidParamsError));
        }

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            return Ok(A2AJsonRpcResponse.Fail(
                request.Id,
                new A2AJsonRpcError(-32000, "Service is busy. Please try again in a few seconds.")));
        }

        try
        {
            var contextId = inboundContext.ContextId ?? NewContextId();
            var history = GetConversationHistory(contextId);
            var response = await chatApplicationService.ChatAsync(
                new ChatExecutionRequest(message, history),
                cancellationToken);
            conversationHistoryStore.AddTurn(contextId, "user", message);
            conversationHistoryStore.AddTurn(contextId, "assistant", response.NaturalLanguageAnswer);

            return Ok(A2AJsonRpcResponse.Success(
                request.Id,
                ToA2AMessage(response, inboundContext with { ContextId = contextId }, history?.Count ?? 0)));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Ok(A2AJsonRpcResponse.Fail(
                request.Id,
                new A2AJsonRpcError(-32001, "A2A request timed out.")));
        }
        catch (Exception)
        {
            return Ok(A2AJsonRpcResponse.Fail(request.Id, A2AJsonRpcError.InternalError()));
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    private async Task StreamMessageAsync(
        A2AJsonRpcRequest request,
        string message,
        A2AInboundMessage inboundContext,
        CancellationToken cancellationToken)
    {
        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await Response.StartAsync(cancellationToken);

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            await WriteSseResponseAsync(
                A2AJsonRpcResponse.Fail(
                    request.Id,
                    new A2AJsonRpcError(-32000, "Service is busy. Please try again in a few seconds.")),
                cancellationToken);
            return;
        }

        var contextId = inboundContext.ContextId ?? NewContextId();
        var taskId = inboundContext.TaskId ?? NewTaskId();

        try
        {
            await WriteSseResponseAsync(
                A2AJsonRpcResponse.Success(
                    request.Id,
                    new A2ATask(
                        Kind: "task",
                        Id: taskId,
                        ContextId: contextId,
                        Status: new A2ATaskStatus("working"))),
                cancellationToken);

            var artifactId = $"artifact_{Guid.NewGuid():N}";
            var chunkIndex = 0;
            var answerBuilder = new StringBuilder();

            await foreach (var update in chatApplicationService.ChatStreamAsync(
                new ChatExecutionRequest(message, GetConversationHistory(contextId)),
                cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(update.Chunk))
                {
                    continue;
                }

                answerBuilder.Append(update.Chunk);
                await WriteSseResponseAsync(
                    A2AJsonRpcResponse.Success(
                        request.Id,
                        new A2ATaskArtifactUpdateEvent(
                            Kind: "artifact-update",
                            TaskId: taskId,
                            ContextId: contextId,
                            Artifact: new A2AArtifact(
                                ArtifactId: artifactId,
                                Name: "answer",
                                Parts: [new A2ATextPart("text", update.Chunk)],
                                Index: chunkIndex),
                            Append: chunkIndex > 0,
                            LastChunk: false)),
                    cancellationToken);

                chunkIndex++;
            }

            conversationHistoryStore.AddTurn(contextId, "user", message);
            conversationHistoryStore.AddTurn(contextId, "assistant", answerBuilder.ToString());

            await WriteSseResponseAsync(
                A2AJsonRpcResponse.Success(
                    request.Id,
                    new A2ATaskStatusUpdateEvent(
                        Kind: "status-update",
                        TaskId: taskId,
                        ContextId: contextId,
                        Status: new A2ATaskStatus("completed"),
                        Final: true)),
                cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await WriteSseResponseAsync(
                A2AJsonRpcResponse.Fail(
                    request.Id,
                    new A2AJsonRpcError(-32001, "A2A stream timed out.")),
                CancellationToken.None);
        }
        catch (Exception)
        {
            await WriteSseResponseAsync(
                A2AJsonRpcResponse.Fail(request.Id, A2AJsonRpcError.InternalError()),
                CancellationToken.None);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    private async Task WriteSseResponseAsync(A2AJsonRpcResponse response, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"data: {JsonSerializer.Serialize(response, JsonOptions)}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private bool IsEnabled() =>
        configuration.GetValue("A2A:Enabled", true);

    private static bool TryExtractUserMessage(
        JsonElement? parameters,
        out string message,
        out A2AInboundMessage inboundContext,
        out A2AJsonRpcError error)
    {
        message = string.Empty;
        inboundContext = new A2AInboundMessage(null, null);
        error = A2AJsonRpcError.InvalidParams("Missing params.message.parts text content.");

        if (parameters is null ||
            parameters.Value.ValueKind != JsonValueKind.Object ||
            !parameters.Value.TryGetProperty("message", out var messageElement) ||
            messageElement.ValueKind != JsonValueKind.Object ||
            !messageElement.TryGetProperty("parts", out var partsElement) ||
            partsElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        inboundContext = new A2AInboundMessage(
            messageElement.GetStringPropertyOrDefault("contextId"),
            messageElement.GetStringPropertyOrDefault("taskId"));

        var textParts = partsElement
            .EnumerateArray()
            .Where(part => part.ValueKind == JsonValueKind.Object &&
                part.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            .Select(part => part.GetProperty("text").GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Select(text => text!.Trim());

        message = string.Join(Environment.NewLine + Environment.NewLine, textParts).Trim();
        if (!string.IsNullOrWhiteSpace(message))
        {
            return true;
        }

        return false;
    }

    private List<ConversationTurn>? GetConversationHistory(string contextId)
    {
        var history = conversationHistoryStore.GetHistory(contextId);
        return history.Count == 0 ? null : [.. history];
    }

    private static A2AMessage ToA2AMessage(
        ChatExecutionResult response,
        A2AInboundMessage inboundContext,
        int historyTurnCount) =>
        new(
            Kind: "message",
            Role: "agent",
            MessageId: $"msg_{Guid.NewGuid():N}",
            Parts: [new A2ATextPart("text", response.NaturalLanguageAnswer)],
            ContextId: inboundContext.ContextId ?? NewContextId(),
            TaskId: inboundContext.TaskId,
            Metadata: new A2AMessageMetadata(
                AgentId: response.Routing.AgentId,
                Confidence: response.Routing.Confidence,
                RoutingMethod: response.Routing.RoutingMethod,
                Grounding: response.Grounding,
                TraceId: Activity.Current?.TraceId.ToString(),
                HistoryTurnCount: historyTurnCount,
                Trace: response.Trace));

    private static string NewContextId() => $"ctx_{Guid.NewGuid():N}";

    private static string NewTaskId() => $"task_{Guid.NewGuid():N}";
}

internal sealed record A2AInboundMessage(string? ContextId, string? TaskId);

public sealed record A2AAgentCard(
    string ProtocolVersion,
    string Name,
    string Description,
    string Url,
    string PreferredTransport,
    A2AAgentCapabilities Capabilities,
    IReadOnlyList<string> DefaultInputModes,
    IReadOnlyList<string> DefaultOutputModes,
    IReadOnlyList<A2AAgentSkill> Skills,
    bool SupportsAuthenticatedExtendedCard = false);

public sealed record A2AAgentCapabilities(bool Streaming);

public sealed record A2AAgentSkill(
    string Id,
    string Name,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Examples);

public sealed class A2AJsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string? JsonRpc { get; set; }

    [JsonPropertyName("id")]
    public JsonElement? Id { get; set; }

    [JsonPropertyName("method")]
    public string? Method { get; set; }

    [JsonPropertyName("params")]
    public JsonElement? Params { get; set; }
}

public sealed class A2AJsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; init; } = "2.0";

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Id { get; init; }

    [JsonPropertyName("result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Result { get; init; }

    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public A2AJsonRpcError? Error { get; init; }

    public static A2AJsonRpcResponse Success(JsonElement? id, object result) =>
        new() { Id = id, Result = result };

    public static A2AJsonRpcResponse Fail(JsonElement? id, A2AJsonRpcError error) =>
        new() { Id = id, Error = error };
}

public sealed record A2AJsonRpcError(int Code, string Message)
{
    public static A2AJsonRpcError InvalidRequest() =>
        new(-32600, "Invalid JSON-RPC request.");

    public static A2AJsonRpcError MethodNotFound(string method) =>
        new(-32601, $"Method not found: {method}");

    public static A2AJsonRpcError InvalidParams(string message) =>
        new(-32602, message);

    public static A2AJsonRpcError InternalError() =>
        new(-32603, "Internal server error.");
}

public sealed record A2AMessage(
    string Kind,
    string Role,
    string MessageId,
    IReadOnlyList<A2ATextPart> Parts,
    string ContextId,
    string? TaskId,
    A2AMessageMetadata Metadata);

public sealed record A2ATextPart(string Kind, string Text);

public sealed record A2AMessageMetadata(
    string AgentId,
    float Confidence,
    string RoutingMethod,
    GroundingMetadata? Grounding,
    string? TraceId,
    int HistoryTurnCount = 0,
    AgenticTrace? Trace = null);

public sealed record A2ATask(
    string Kind,
    string Id,
    string ContextId,
    A2ATaskStatus Status);

public sealed record A2ATaskStatus(string State);

public sealed record A2ATaskArtifactUpdateEvent(
    string Kind,
    string TaskId,
    string ContextId,
    A2AArtifact Artifact,
    bool Append,
    bool LastChunk);

public sealed record A2AArtifact(
    string ArtifactId,
    string Name,
    IReadOnlyList<A2ATextPart> Parts,
    int Index);

public sealed record A2ATaskStatusUpdateEvent(
    string Kind,
    string TaskId,
    string ContextId,
    A2ATaskStatus Status,
    bool Final);

internal static class A2AJsonElementExtensions
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out var property)
            ? property
            : null;

    public static string? GetStringPropertyOrDefault(this JsonElement element, string propertyName) =>
        element.GetPropertyOrDefault(propertyName) is { ValueKind: JsonValueKind.String } property
            ? property.GetString()
            : null;
}
