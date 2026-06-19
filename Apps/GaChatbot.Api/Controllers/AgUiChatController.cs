namespace GaChatbot.Api.Controllers;

using GA.Business.Core.Orchestration.Models;
using GaChatbot.Api.Helpers;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/chatbot")]
public sealed class AgUiChatController(
    ILogger<AgUiChatController> logger,
    ILlmConcurrencyGate concurrencyGate,
    IChatApplicationService chatApplicationService) : ControllerBase
{
    [HttpPost("agui/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task AgUiStream([FromBody] RunAgentInput input, CancellationToken cancellationToken)
    {
        var userMessageIndex = FindLastUserMessageIndex(input.Messages);
        var userMessage = userMessageIndex < 0
            ? null
            : input.Messages[userMessageIndex].Content?.Trim();

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await Response.StartAsync(cancellationToken);

        var writer = new AgUiEventWriter(Response);
        var threadId = string.IsNullOrWhiteSpace(input.ThreadId) ? $"thread_{Guid.NewGuid():N}" : input.ThreadId;
        var runId = string.IsNullOrWhiteSpace(input.RunId) ? AgUiEventWriter.NewRunId() : input.RunId;
        var messageId = $"msg_{(runId.Length > 8 ? runId[..8] : runId)}";
        var textStarted = false;

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            await writer.WriteRunErrorAsync("Service is busy. Please try again.", "SERVICE_BUSY", cancellationToken);
            return;
        }

        try
        {
            await writer.WriteRunStartedAsync(threadId, runId, cancellationToken);

            var chatRequest = new ChatExecutionRequest(userMessage, ToConversationTurns(input.Messages, userMessageIndex));
            await foreach (var update in chatApplicationService.ChatStreamAsync(chatRequest, cancellationToken))
            {
                if (update.Routing is not null)
                {
                    await writer.WriteStepStartedAsync(update.Routing.AgentId, runId, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(update.Chunk))
                {
                    if (!textStarted)
                    {
                        await writer.WriteTextStartAsync(messageId, cancellationToken);
                        textStarted = true;
                    }

                    await writer.WriteTextChunkAsync(messageId, update.Chunk, cancellationToken);
                }
            }

            if (textStarted)
            {
                await writer.WriteTextEndAsync(messageId, cancellationToken);
            }

            await writer.WriteRunFinishedAsync(threadId, runId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("AG-UI stream cancelled for run {RunId}", runId);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error in AG-UI stream for run {RunId}", runId);
            if (textStarted)
            {
                await writer.WriteTextEndAsync(messageId, cancellationToken);
            }

            await writer.WriteRunErrorAsync("Failed to process message. Please try again.", "INTERNAL_ERROR", cancellationToken);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    [HttpPost("agui/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AgUiJson([FromBody] RunAgentInput input, CancellationToken cancellationToken)
    {
        var userMessageIndex = FindLastUserMessageIndex(input.Messages);
        var userMessage = userMessageIndex < 0
            ? null
            : input.Messages[userMessageIndex].Content?.Trim();

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return BadRequest(new { error = "No user message found in the request." });
        }

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Service is busy. Please try again." });
        }

        try
        {
            var response = await chatApplicationService.ChatAsync(
                new ChatExecutionRequest(userMessage, ToConversationTurns(input.Messages, userMessageIndex)),
                cancellationToken);

            return Ok(new
            {
                answer = response.NaturalLanguageAnswer,
                routing = response.Routing,
                grounding = response.Grounding
            });
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    private static int FindLastUserMessageIndex(IReadOnlyList<AgUiMessage> messages)
    {
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (string.Equals(messages[i].Role, "user", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static List<ConversationTurn> ToConversationTurns(
        IReadOnlyList<AgUiMessage> messages,
        int currentUserMessageIndex) =>
        [.. messages
            .Where((message, index) => index != currentUserMessageIndex && !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new ConversationTurn(message.Role, message.Content!, DateTimeOffset.UtcNow))];
}

public sealed record RunAgentInput(
    string ThreadId,
    string RunId,
    IReadOnlyList<AgUiMessage> Messages,
    object? State = null);

public sealed record AgUiMessage(
    string Role,
    string? Content,
    string? Id = null);
