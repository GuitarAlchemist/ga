namespace GaChatbot.Api.Controllers;

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Text.Json;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GaChatbot.Api.Helpers;
using GaChatbot.Api.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/chatbot")]
public sealed class ChatbotController(
    ILogger<ChatbotController> logger,
    ILlmConcurrencyGate concurrencyGate,
    IChatApplicationService chatApplicationService) : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [HttpPost("chat/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task ChatStream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteSseErrorAsync("Message cannot be empty.", cancellationToken);
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await Response.StartAsync(cancellationToken);

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            await WriteSseErrorAsync("Service is busy. Please try again in a few seconds.", cancellationToken);
            return;
        }

        try
        {
            var chatRequest = new ChatExecutionRequest(message, ToConversationTurns(request.ConversationHistory));

            await foreach (var update in chatApplicationService.ChatStreamAsync(chatRequest, cancellationToken))
            {
                if (update.Routing is not null)
                {
                    var routingPayload = JsonSerializer.Serialize(new
                    {
                        type = "routing",
                        agentId = update.Routing.AgentId,
                        confidence = update.Routing.Confidence,
                        routingMethod = update.Routing.RoutingMethod,
                        trace = update.Trace,
                        grounding = update.Grounding is null
                            ? null
                            : new
                            {
                                source = update.Grounding.Source,
                                revision = update.Grounding.Revision,
                                queryType = update.Grounding.QueryType
                            }
                    }, JsonOptions);

                    await WriteSseLineAsync(routingPayload, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(update.Chunk))
                {
                    await WriteSseLineAsync(update.Chunk, cancellationToken);
                }

                if (update.IsCompleted)
                {
                    await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Chat stream cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming chatbot response");
            await WriteSseErrorAsync("Failed to process message. Please try again.", cancellationToken);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatJsonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatJsonResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { error = "Message cannot be empty." });
        }

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Service is busy. Please try again in a few seconds." });
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var response = await chatApplicationService.ChatAsync(
                new ChatExecutionRequest(message, ToConversationTurns(request.ConversationHistory)),
                cancellationToken);
            sw.Stop();

            return Ok(new ChatJsonResponse(
                response.NaturalLanguageAnswer,
                response.Routing.AgentId,
                response.Routing.Confidence,
                response.Routing.RoutingMethod,
                response.Grounding,
                sw.ElapsedMilliseconds,
                Activity.Current?.TraceId.ToString(),
                response.Trace));
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(ChatbotStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatbotStatus>> GetStatus(CancellationToken cancellationToken)
    {
        var status = await chatApplicationService.GetStatusAsync(cancellationToken);
        return Ok(status);
    }

    [HttpGet("examples")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetExamples() =>
        Ok((List<string>)
        [
            "Show me some easy beginner chords",
            "Explain voice leading in jazz",
            "What are the modes of the major scale?",
            "Generate a mellow ii-V-I in C",
            "How do I make this progression sound darker?"
        ]);

    private async Task WriteSseLineAsync(string data, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private Task WriteSseErrorAsync(string message, CancellationToken cancellationToken) =>
        WriteSseLineAsync(JsonSerializer.Serialize(new { error = message }, JsonOptions), cancellationToken);

    private static List<ConversationTurn>? ToConversationTurns(List<ChatMessage>? history) =>
        history?
            .Where(m => !string.IsNullOrWhiteSpace(m.Content))
            .Select(m => new ConversationTurn(m.Role, m.Content, DateTimeOffset.UtcNow))
            .ToList();
}

public sealed class ChatRequest
{
    /// <summary>
    /// The user's current message. Capped at 2 KB so a single request
    /// can't ship a megabyte of prompt-injection or DoS payload to the
    /// local LLM.
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Prior conversation turns. Capped at 50 turns total per request
    /// AND at 2 KB per turn content so the public chatbot endpoint
    /// (demos.guitaralchemist.com/chatbot) can't be used as a free
    /// LLM-resource-exhaustion proxy. Without these caps an unauthenticated
    /// attacker could ship 28 MB (Kestrel default body limit) of context
    /// to local Ollama, parking it behind LlmConcurrencyGate and starving
    /// real users — exactly the abuse vector flagged in PR #111 review.
    /// </summary>
    [MaxLength(MaxConversationHistoryTurns)]
    public List<ChatMessage>? ConversationHistory { get; set; }

    public bool UseSemanticSearch { get; set; } = true;

    public const int MaxConversationHistoryTurns = 50;
    public const int MaxConversationTurnContentChars = 2000;
}

public sealed record ChatJsonResponse(
    string NaturalLanguageAnswer,
    string AgentId,
    float Confidence,
    string RoutingMethod,
    GroundingMetadata? Grounding = null,
    long ElapsedMs = 0,
    string? TraceId = null,
    AgenticTrace? Trace = null);

public sealed class ChatMessage
{
    [MaxLength(32)]
    public string Role { get; set; } = string.Empty;

    [MaxLength(ChatRequest.MaxConversationTurnContentChars)]
    public string Content { get; set; } = string.Empty;
}

/// <summary>
/// Readiness probe response. <see cref="IsAvailable"/> requires the configured
/// chat AND embedding models to be installed in the provider — was previously
/// just "HTTP reachable", which silently reported healthy while live API was
/// wedged. The new checks distinguish "Ollama process up" (transport-level)
/// from "chatbot can actually serve a request" (path-level).
/// </summary>
public sealed class ChatbotStatus
{
    /// <summary>
    /// True iff the chatbot can serve a request: provider HTTP reachable AND
    /// configured chat model installed AND configured embedding model installed.
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>Human-readable summary; surface this to operators directly.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Provider name (<c>"ollama"</c>, <c>"github"</c>, <c>"docker"</c>).</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Configured chat model name (e.g. <c>"llama3.2:3b"</c>).</summary>
    public string? ChatModel { get; set; }

    /// <summary>True iff the chat model is installed in the provider.</summary>
    public bool? ChatModelInstalled { get; set; }

    /// <summary>Configured embedding model name (e.g. <c>"nomic-embed-text"</c>).</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>True iff the embedding model is installed in the provider.</summary>
    public bool? EmbeddingModelInstalled { get; set; }

    /// <summary>
    /// True iff the provider HTTP endpoint responds. Distinct from
    /// <see cref="IsAvailable"/>: we may be HTTP-reachable but missing models.
    /// </summary>
    public bool ProviderReachable { get; set; }

    /// <summary>
    /// True iff a synthetic catalog-skill query round-trips through the
    /// orchestrator within the probe's timeout. Catches process-level
    /// wedges (DI registry empty, hosted services deadlocked, orchestrator
    /// hung) that the provider-level probe cannot see. Independent of
    /// <see cref="ProviderReachable"/>: catalog skills don't need Ollama.
    /// </summary>
    public bool? OrchestratorRoundTripOk { get; set; }

    public DateTime Timestamp { get; set; }
}
