namespace GaApi.Controllers;

using System.Text.Json;
using Services;

/// <summary>
///     REST API controller for chatbot interactions
///     Provides both streaming and non-streaming endpoints
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatbotController(
    ILogger<ChatbotController> logger,
    ChatbotSessionOrchestrator sessionOrchestrator,
    IOllamaChatService chatService)
    : ControllerBase
{
    /// <summary>
    ///     Send a message to the chatbot and receive a streaming response via Server-Sent Events.
    ///     The response Content-Type is <c>text/event-stream</c>; each <c>data:</c> line is a JSON chunk.
    ///     The final event is <c>data: [DONE]</c>.
    /// </summary>
    /// <param name="request">Chat message and optional conversation history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("chat/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]          // text/event-stream
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task ChatStream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteSseError("Invalid chat request.", cancellationToken);
            return;
        }

        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await WriteSseError("Message cannot be empty.", cancellationToken);
            return;
        }

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var sessionRequest = new ChatSessionRequest(
            message,
            request.ConversationHistory,
            request.UseSemanticSearch);

        try
        {
            await foreach (var chunk in sessionOrchestrator.StreamResponseAsync(sessionRequest, cancellationToken))
            {
                await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }

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
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            await WriteSseError("Failed to process message. Please try again.", cancellationToken);
        }
    }

    /// <summary>
    ///     Check whether the Ollama service is reachable and the chatbot is ready to respond.
    /// </summary>
    /// <returns>Availability flag, human-readable message, and timestamp.</returns>
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
    ///     Return a curated list of example queries to help users get started with the chatbot.
    /// </summary>
    /// <returns>A list of example query strings.</returns>
    [HttpGet("examples")]
    [ProducesResponseType(typeof(List<string>), StatusCodes.Status200OK)]
    public ActionResult<List<string>> GetExamples() =>
        Ok(new List<string>
        {
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
        });

    private async Task WriteSseError(string errorMessage, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { error = errorMessage });
        await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}
