namespace GaApi.Controllers;

using System.Diagnostics;
using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using Services;

/// <summary>
///     REST API controller for chatbot interactions.
///     Provides a streaming SSE endpoint backed by the full agentic pipeline
///     (SemanticRouter → specialized agents → SpectralRagOrchestrator → grounded narrator).
/// </summary>
/// <remarks>
///     Depends on <see cref="IChatApplicationService"/> rather than the
///     concrete <see cref="GA.Business.Core.Orchestration.Services.ProductionOrchestrator"/>:
///     all GaApi chat surfaces go through the same host-neutral application
///     service so future readiness / trace decorators apply uniformly.
///     Codex CLI second-opinion 2026-05-07 — roadmap P0 #2 first cut.
///     Trace surfaced via <see cref="IAgenticTraceCapture"/> at the wire boundary
///     (roadmap P1 #7 commit 1 — codex CLI 2026-05-08 design review).
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class ChatbotController(
    ILogger<ChatbotController> logger,
    IChatApplicationService chatService,
    IAgenticTraceCapture traceCapture,
    IChatService statusService,
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

        // Phase C (task #103) — HTTP transport plumbs SessionId from a
        // server-issued cookie so HTTP callers get session-scoped memory
        // the same way SignalR callers do (PR #160). Without this, HTTP
        // requests fall through to the orchestrator's per-request Guid
        // fallback and their MemoryHook writes are unreachable from any
        // future request.
        //
        // Phase C P1 (task #107) — fix ordering bug: GetOrIssue MUST run
        // BEFORE StartAsync because Response.Cookies.Append modifies
        // response headers, and headers become read-only once StartAsync
        // commits the wire. The previous placement after StartAsync would
        // have thrown InvalidOperationException, surfacing as an SSE
        // RUN_ERROR frame instead of issuing a cookie.
        //
        // Trade-off vs. VULN-004 (defer past gate): SSE never returns 503;
        // a busy gate produces a 200-OK + SSE error frame, so we don't
        // mint cookies "wasted on 503s." Validation (message-empty)
        // already ran, so shape-invalid requests still don't get cookies.
        var sessionId = HttpChatSessionCookie.GetOrIssue(HttpContext);

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
            var response = await chatService.ChatAsync(
                new GA.Business.Core.Orchestration.Models.ChatRequest(message, SessionId: sessionId),
                cancellationToken);

            // 1. Emit routing metadata event (first, before text). Trace and
            // grounding are included on this frame to match the JSON wire's
            // contract — codex CLI 2026-05-08 risk-list item 3 ("SSE parity
            // is easy to miss").
            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
            var routingPayload = JsonSerializer.Serialize(new
            {
                type = "routing",
                agentId = routing.AgentId,
                confidence = routing.Confidence,
                routingMethod = routing.RoutingMethod,
                grounding = response.Grounding,
                trace = traceCapture.Build(),
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
            // Phase C plumbing — see ChatStream above.
            var sessionId = HttpChatSessionCookie.GetOrIssue(HttpContext);
            var response = await chatService.ChatAsync(
                new GA.Business.Core.Orchestration.Models.ChatRequest(message, SessionId: sessionId),
                cancellationToken);
            sw.Stop();

            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
            var traceId = Activity.Current?.TraceId.ToString();
            return Ok(new ChatJsonResponse(
                NaturalLanguageAnswer: response.NaturalLanguageAnswer ?? string.Empty,
                routing.AgentId,
                routing.Confidence,
                routing.RoutingMethod,
                Grounding: response.Grounding,
                ElapsedMs: sw.ElapsedMilliseconds,
                TraceId: traceId,
                Trace: traceCapture.Build()));
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
        var isAvailable = await statusService.IsAvailableAsync(cancellationToken);
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

    /// <summary>
    ///     Return a categorized showcase script highlighting the chatbot's breadth:
    ///     theory, voicings, progressions, DSL evaluation, and analysis surfaces.
    ///     Consumed by the Showcase Panel in the React UI.
    /// </summary>
    [HttpGet("demo")]
    [ProducesResponseType(typeof(ChatbotDemoScript), StatusCodes.Status200OK)]
    public ActionResult<ChatbotDemoScript> GetDemo() =>
        Ok(new ChatbotDemoScript(
            Version: "1.0",
            Categories:
            [
                new ChatbotDemoCategory(
                    Id: "theory",
                    Name: "Music Theory",
                    Icon: "music_note",
                    Description: "Core questions about scales, intervals, modes, and key relationships.",
                    Prompts:
                    [
                        new("Explain the circle of fifths", "Foundational key-relationship diagram."),
                        new("What are the modes of the major scale?", "Ionian through Locrian, with character notes."),
                        new("What's the difference between major and minor?", "Quality contrast with examples.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "voicings",
                    Name: "Chord Voicings",
                    Icon: "queue_music",
                    Description: "OPTIC-K vector search over the chord-voicing corpus.",
                    Prompts:
                    [
                        new("Show me chord voicings for Cmaj7", "Triggers ga_search_voicings against the index."),
                        new("Give me easier voicings of F#m7b5", "Surfaces fewer-finger alternatives."),
                        new("Find voicings with a similar interval profile to Em9", "ICV-neighbor search.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "progressions",
                    Name: "Progressions & Substitution",
                    Icon: "timeline",
                    Description: "Analyze, complete, and reharmonize chord progressions.",
                    Prompts:
                    [
                        new("Analyze the progression Cmaj7 Am7 Dm7 G7", "Roman-numeral analysis + key detection."),
                        new("Suggest substitutions for the G7 in a ii-V-I", "Tritone, secondary dominant, and modal options."),
                        new("Complete the progression Cmaj7 Am7 ...", "Likely continuations grounded in voice-leading.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "dsl",
                    Name: "DSL & Evaluation",
                    Icon: "code",
                    Description: "Live evaluation of the GA F# DSL — scripts, transposition, voice-leading.",
                    Prompts:
                    [
                        new("Transpose C E G to D", "Calls ga_transpose_chord under the hood."),
                        new("What are the common tones between Cmaj7 and Am7?", "Set-theory common-tone analysis."),
                        new("Compute voice-leading from Cmaj7 to Fmaj7", "Pairwise voice-leading minimization.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "fretboard",
                    Name: "Fretboard & Technique",
                    Icon: "guitar",
                    Description: "Practical guitar questions — fingerings, barre chords, fingerpicking.",
                    Prompts:
                    [
                        new("Show me some easy beginner chords", "Open-position triads with tab."),
                        new("How do I play a barre chord?", "Technique breakdown with fingering."),
                        new("How do I improve my fingerpicking?", "Practice routine guidance.")
                    ])
            ]));

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
