namespace GaApi.Controllers;

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
    IChatService chatService)
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
            {
                cancellationToken.ThrowIfCancellationRequested();
                await WriteSseLineAsync(chunk, cancellationToken);
            }

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

        var response = await orchestrator.AnswerAsync(
            new GA.Business.Core.Orchestration.Models.ChatRequest(message), cancellationToken);

        var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
        return Ok(new ChatJsonResponse(
            response.NaturalLanguageAnswer ?? string.Empty,
            routing.AgentId,
            routing.Confidence,
            routing.RoutingMethod));
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
            "Show me some easy beginner chords",
            "What are the modes of the major scale?",
            "Explain voice leading in jazz",
            "How do I play a barre chord?",
            "What makes a chord sound jazzy?",
            "Show me some dark, moody chords",
            "Explain the circle of fifths",
            "What's the difference between major and minor?",
            "How do I improve my fingerpicking?",
            "What are some common chord progressions?"
        ]);

    // ── helpers ──────────────────────────────────────────────────────────────

    private async Task WriteSseLineAsync(string data, CancellationToken ct)
    {
        await Response.WriteAsync($"data: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    private async Task WriteSseErrorAsync(string errorMessage, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { error = errorMessage });
        await Response.WriteAsync($"data: {payload}\n\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    /// <summary>
    /// Splits a response into sentence-boundary chunks for progressive rendering.
    /// Keeps chunks ≤ 200 characters; never splits mid-word.
    /// </summary>
    private static IEnumerable<string> SplitIntoChunks(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        // Split on sentence boundaries (.  !  ?) keeping delimiter attached
        var sentences = System.Text.RegularExpressions.Regex
            .Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s));

        foreach (var sentence in sentences)
            yield return sentence;
    }
}
