namespace GaApi.Controllers;

using System.Diagnostics;
using System.Text.Json;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using Services;

/// <summary>
///     REST API controller for chatbot interactions.
///     Provides a streaming SSE endpoint backed by the full agentic pipeline
///     (SemanticRouter → specialized agents → SpectralRagOrchestrator → grounded narrator).
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatbotController(
    ILogger<ChatbotController> logger,
    ProductionOrchestrator orchestrator,
    IChatService chatService,
    ILlmConcurrencyGate concurrencyGate)
    : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    /// <summary>
    ///     Send a message to the chatbot and receive a streaming response via Server-Sent Events.
    ///     The first SSE event is a routing metadata JSON object:
    ///     <c>data: {"type":"routing","agentId":"Theory","confidence":0.91,"routingMethod":"semantic"}</c>
    ///     Subsequent events are plain text chunks. The final event is <c>data: [DONE]</c>.
    /// </summary>
    [HttpPost("chat/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task ChatStream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteSseErrorAsync("Invalid chat request.", cancellationToken);
            return;
        }

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
        Response.Headers.Append("Connection", "keep-alive");

        // Commit status + headers to the client immediately so the
        // browser/test client can observe the SSE content-type before
        // the orchestrator finishes generating a response.
        await Response.StartAsync(cancellationToken);

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            await WriteSseErrorAsync("Service is busy. Please try again in a few seconds.", cancellationToken);
            return;
        }

        try
        {
            var response = await orchestrator.AnswerAsync(
                new GA.Business.Core.Orchestration.Models.ChatRequest(message), cancellationToken);

            // 1. Emit routing metadata event (first, before text)
            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
            var routingPayload = JsonSerializer.Serialize(new
            {
                type = "routing",
                agentId = routing.AgentId,
                confidence = routing.Confidence,
                routingMethod = routing.RoutingMethod
            }, _jsonOptions);
            await WriteSseLineAsync(routingPayload, cancellationToken);

            // 2. Stream the answer text in sentence-boundary chunks
            var answer = response.NaturalLanguageAnswer ?? string.Empty;
            foreach (var chunk in SplitIntoChunks(answer))
                await WriteSseLineAsync(chunk, cancellationToken);

            // 3. Signal completion
            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Chat stream cancelled for message: {Message}", message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error streaming chat response");
            // Headers already committed — cannot change status code.
            // Signal the error via an SSE error event so the client can react.
            await WriteSseErrorAsync("Failed to process message. Please try again.", cancellationToken);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    /// <summary>
    ///     Send a message to the chatbot and receive a complete JSON response.
    ///     Intended for MCP tool callers and other non-streaming clients.
    /// </summary>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatJsonResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatJsonResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
            return BadRequest(new { error = "Message cannot be empty." });

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { error = "Service is busy. Please try again in a few seconds." });

        try
        {
            var sw = Stopwatch.StartNew();
            var response = await orchestrator.AnswerAsync(
                new GA.Business.Core.Orchestration.Models.ChatRequest(message), cancellationToken);
            sw.Stop();

            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
            var traceId = Activity.Current?.TraceId.ToString();
            return Ok(new ChatJsonResponse(
                NaturalLanguageAnswer: response.NaturalLanguageAnswer ?? string.Empty,
                routing.AgentId,
                routing.Confidence,
                routing.RoutingMethod,
                ElapsedMs: sw.ElapsedMilliseconds,
                TraceId: traceId));
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    /// <summary>
    ///     Check whether the Ollama service is reachable and the chatbot is ready to respond.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ChatbotStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatbotStatus>> GetStatus(CancellationToken cancellationToken)
    {
        var isAvailable = await chatService.IsAvailableAsync(cancellationToken);
        return Ok(new ChatbotStatus
        {
            IsAvailable = isAvailable,
            Message = isAvailable
                ? "Chatbot is ready"
                : "Chatbot is not available. Please ensure Ollama is running.",
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    ///     Return a curated list of example queries to help users get started.
    /// </summary>
    [HttpGet("examples")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetExamples() =>
        Ok((List<string>)
        [
            "I'm a beginner who likes blues",
            "What are the modes of the major scale?",
            "Give me a 15-minute practice routine",
            "Start an interval quiz",
            "Help me practice the A minor pentatonic scale",
            "Explain voice leading in jazz",
            "What makes a chord sound jazzy?",
            "Show my progress",
            "What's the difference between major and minor?",
            "What are some common chord progressions?"
        ]);

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task WriteSseLineAsync(string data, CancellationToken ct)
    {
        await Response.WriteAsync($"data: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private Task WriteSseErrorAsync(string errorMessage, CancellationToken ct) =>
        WriteSseLineAsync(JsonSerializer.Serialize(new { error = errorMessage }), ct);

    private static IEnumerable<string> SplitIntoChunks(string text) =>
        Helpers.SseChunker.SplitIntoChunks(text);
}
