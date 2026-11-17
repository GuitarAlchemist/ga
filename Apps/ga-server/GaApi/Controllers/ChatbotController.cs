namespace GaApi.Controllers;

using System.Diagnostics;
using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Providers;
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
// @ai:business-value SSE chatbot ingress — the demo flow every visitor sees on guitaralchemist.com; outage here = product invisible [T:manually-reviewed conf:0.95 src:product-owner@2026-05-24]
public class ChatbotController(
    ILogger<ChatbotController> logger,
    IChatIntake chatIntake,
    IAgenticTraceCapture traceCapture,
    IChatProviderAvailability statusService,
    ChatGenerationHealth generationHealth)
    : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly TimeSpan StatusCheckTimeout = TimeSpan.FromSeconds(1);

    /// <summary>
    ///     Send a message to the chatbot and receive a streaming response via Server-Sent Events.
    ///     The first SSE event is a routing metadata JSON object:
    ///     <c>data: {"type":"routing","agentId":"Theory","confidence":0.91,"routingMethod":"semantic"}</c>
    ///     Subsequent events are plain text chunks, written as the model produces them.
    ///     The final event is <c>data: [DONE]</c>.
    /// </summary>
    /// <remarks>
    ///     A thin adapter over <see cref="IChatIntake.RunAsync"/>: each chat event maps to one
    ///     SSE frame. The routing frame's trace covers the steps done when routing was known.
    /// </remarks>
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

        // Routing telemetry written while serving this request carries the page's ?ref= tag.
        using var trafficSource = RoutingTelemetryLog.BeginTrafficSource(request.Source, LocalCaller.IsDirect(HttpContext));

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");
        Response.Headers.Append("Connection", "keep-alive");

        // Commit status + headers to the client immediately so the
        // browser/test client can observe the SSE content-type before
        // the orchestrator finishes generating a response.
        await Response.StartAsync(cancellationToken);

        try
        {
            // Cross the chat intake seam (#1): validation + gating + dispatch live
            // there. A rejection becomes an SSE error frame (the wire never returns
            // 503 after StartAsync); a run streams the routing frame, text frames, [DONE].
            await foreach (var chatEvent in chatIntake.RunAsync(
                               new ChatIntakeRequest(message, sessionId, ToConversationTurns(request.ConversationHistory)),
                               cancellationToken))
            {
                switch (chatEvent)
                {
                    case ChatEvent.Routing routing:
                        // Trace and grounding ride on this frame to match the JSON wire's
                        // contract — codex CLI 2026-05-08 risk-list item 3 ("SSE parity
                        // is easy to miss").
                        await WriteSseLineAsync(JsonSerializer.Serialize(new
                        {
                            type = "routing",
                            agentId = routing.AgentId,
                            confidence = routing.Confidence,
                            routingMethod = routing.Method,
                            grounding = routing.Grounding,
                            trace = traceCapture.Build(),
                        }, _jsonOptions), cancellationToken);
                        break;
                    case ChatEvent.TextDelta delta:
                        await WriteSseLineAsync(delta.Text, cancellationToken);
                        break;
                    case ChatEvent.Completed:
                        await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
                        await Response.Body.FlushAsync(cancellationToken);
                        return;
                    case ChatEvent.Failed failed:
                        var errorMessage = failed.Rejection switch
                        {
                            ChatIntakeError.Busy => "Service is busy. Please try again in a few seconds.",
                            ChatIntakeError.Validation v => v.Reason,
                            _ => "Failed to process message. Please try again."
                        };
                        if (failed.Rejection is null)
                            logger.LogError("Chat stream run {RunId} failed: {Error}", failed.RunId, failed.Message);
                        await WriteSseErrorAsync(errorMessage, cancellationToken);
                        return;
                }
            }
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
        // Thin adapter over the chat intake seam (#1): validate + gate + dispatch
        // live in IChatIntake. This method only resolves the transport-specific
        // session id (HTTP cookie — Phase C plumbing, see ChatStream above) and
        // frames the typed result. The seam takes an opaque SessionId; it never
        // touches HttpContext.
        var sw = Stopwatch.StartNew();
        var sessionId = HttpChatSessionCookie.GetOrIssue(HttpContext);
        var result = await chatIntake.IntakeAsync(
            new ChatIntakeRequest(request.Message, sessionId, ToConversationTurns(request.ConversationHistory)),
            cancellationToken);
        sw.Stop();

        return result.Match<ActionResult<ChatJsonResponse>>(
            response =>
            {
                var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
                return Ok(new ChatJsonResponse(
                    NaturalLanguageAnswer: response.NaturalLanguageAnswer ?? string.Empty,
                    routing.AgentId,
                    routing.Confidence,
                    routing.RoutingMethod,
                    Grounding: response.Grounding,
                    ElapsedMs: sw.ElapsedMilliseconds,
                    TraceId: Activity.Current?.TraceId.ToString(),
                    Trace: traceCapture.Build()));
            },
            error => error switch
            {
                ChatIntakeError.Busy => StatusCode(StatusCodes.Status503ServiceUnavailable,
                    new { error = "Service is busy. Please try again in a few seconds." }),
                ChatIntakeError.Validation v => BadRequest(new { error = v.Reason }),
                _ => StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "Unexpected error." })
            });
    }

    /// <summary>
    ///     Check whether the Ollama service is reachable and the chatbot is ready to respond.
    ///     A failed last generation (see <see cref="ChatGenerationHealth"/>) reports unavailable.
    /// </summary>
    /// <remarks>
    ///     The check is bounded: an unreachable provider counts as unavailable after
    ///     <see cref="StatusCheckTimeout"/>. Unbounded, the Ollama client's retries held the
    ///     answer for about 16 s, and the public page calls this after every message.
    /// </remarks>
    [HttpGet("status")]
    [ProducesResponseType(typeof(ChatbotStatus), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatbotStatus>> GetStatus(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(StatusCheckTimeout);
        bool isAvailable;
        try
        {
            isAvailable = await statusService.IsAvailableAsync(timeout.Token).WaitAsync(StatusCheckTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException
                                   || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            isAvailable = false;
        }

        var status = new ChatbotStatus
        {
            IsAvailable = isAvailable,
            Message = isAvailable
                ? "Chatbot is ready"
                : "Chatbot is not available. Please ensure Ollama is running.",
            Timestamp = DateTime.UtcNow
        };

        // The availability check never generates, so a provider that answers it but
        // refuses to generate (Ollama Cloud without credits) looked ready. Report the
        // last real generation instead of running paid inference here.
        if (generationHealth.Latest is { } generation)
        {
            status.GenerationOk = generation.Ok;
            if (!generation.Ok)
            {
                status.IsAvailable = false;
                status.Message =
                    $"Chatbot is not available: last generation failed {generation.Age.TotalSeconds:F0}s ago: {generation.Diagnostic}";
            }
        }

        return Ok(status);
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
    ///     Return a categorized showcase script highlighting the chatbot's breadth.
    ///     Every prompt has been hand-verified against /api/chatbot/chat to produce
    ///     a real answer — no broken backend paths (e.g. unloaded OPTIC-K index),
    ///     no SKILL.md preambles, no misroutes. See ChatbotShowcaseSmokeTests
    ///     for the gate.
    /// </summary>
    /// <remarks>
    ///     Version 1.1 (2026-05-12) — trimmed from 15 to 12 prompts after a live
    ///     QA pass found 8 of 15 originals were broken (voicing prompts blocked
    ///     by missing index, progression prompts blocked by chord-symbol parser
    ///     scope, "barre chord" misrouted to practice-routine, etc.). Voicing
    ///     category removed entirely; restore when the OPTIC-K index ships.
    /// </remarks>
    [HttpGet("demo")]
    [ProducesResponseType(typeof(ChatbotDemoScript), StatusCodes.Status200OK)]
    public ActionResult<ChatbotDemoScript> GetDemo() =>
        Ok(new ChatbotDemoScript(
            Version: "1.1",
            Categories:
            [
                new ChatbotDemoCategory(
                    Id: "theory",
                    Name: "Music Theory",
                    Icon: "music_note",
                    Description: "Foundational questions about modes, keys, and the circle of fifths.",
                    Prompts:
                    [
                        new("Explain the circle of fifths", "Key signatures, perfect-fifth relationships, and enharmonics."),
                        new("What are the modes of the major scale", "Ionian through Locrian with formulas and character."),
                        new("What is the difference between major and minor", "Quality contrast with audible examples.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "scales-keys",
                    Name: "Scales & Keys",
                    Icon: "queue_music",
                    Description: "Notes, relative keys, and the diatonic chords of any major key.",
                    Prompts:
                    [
                        new("Show me the notes in C major", "Seven scale notes plus the relative minor."),
                        new("What is the relative minor of G major", "Relative-key pairing with shared key signature."),
                        new("What are the diatonic chords in G major", "Seven diatonic chords with Roman-numeral quality.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "progressions",
                    Name: "Progressions & Substitution",
                    Icon: "timeline",
                    Description: "Identify the key of a progression and reharmonize with substitutions.",
                    Prompts:
                    [
                        new("Identify the key of Am F C G", "Key detection from a four-chord progression."),
                        new("Suggest substitutions for G7 in a ii-V-I", "Harmonic substitutions ranked by ICV distance."),
                        new("Substitutions for C major", "Relative-minor and parallel options with voice-leading cost.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "operations",
                    Name: "Chord Operations",
                    Icon: "code",
                    Description: "Transposition and set-theory analysis on chords.",
                    Prompts:
                    [
                        new("Transpose C E G to D", "Interval calculation plus resulting chord."),
                        new("What are the common tones between Cmaj7 and Am7", "Pitch-class intersection with role-per-chord.")
                    ]),
                new ChatbotDemoCategory(
                    Id: "getting-started",
                    Name: "Getting Started",
                    Icon: "guitar",
                    Description: "Practical guitar starting points for new players.",
                    Prompts:
                    [
                        new("Show me some easy beginner chords", "Open-position chords with frettings in low-to-high notation.")
                    ])
            ]));

    /// <summary>
    ///     Return the QA badge of each showcase prompt, keyed by prompt text trimmed of trailing
    ///     <c>.?!</c>. The chatbot page treats it as best effort: prompts without an entry show
    ///     "not yet QA-validated".
    /// </summary>
    [HttpGet("qa-summary")]
    [ProducesResponseType(typeof(Dictionary<string, ChatbotQaSummary>), StatusCodes.Status200OK)]
    public ActionResult<Dictionary<string, ChatbotQaSummary>> GetQaSummary() =>
        Ok(ChatbotQaSummaryReader.GetCached());

    // ── helpers ──────────────────────────────────────────────────────────────

    private static List<ConversationTurn>? ToConversationTurns(List<ChatMessage>? history) =>
        history?
            .Where(message => message is not null && !string.IsNullOrWhiteSpace(message.Content))
            .Select(message => new ConversationTurn(message.Role, message.Content, DateTimeOffset.UtcNow))
            .ToList();

    // Each line of an SSE event body needs its own `data: ` prefix, and a blank line ends the
    // event. Writing a multi-line chunk (markdown tables, lists, code) as one `data:` line lets
    // the client drop every line after the first, and an embedded blank line ends the event
    // early: the public page rendered only the first paragraph of table answers. '\r' is
    // stripped so CRLF text cannot double-terminate a line.
    private async Task WriteSseLineAsync(string data, CancellationToken ct)
    {
        foreach (var line in data.Replace("\r", string.Empty).Split('\n'))
        {
            await Response.WriteAsync("data: ", ct);
            await Response.WriteAsync(line, ct);
            await Response.WriteAsync("\n", ct);
        }
        await Response.WriteAsync("\n", ct);
        await Response.Body.FlushAsync(ct);
    }

    // A failure goes out as a named `error` event, and no `[DONE]` follows it. An answer chunk
    // can be any text, including JSON shaped like this payload, but WriteSseLineAsync prefixes
    // every line of it with `data: `, so only this writer emits an `event:` line.
    private async Task WriteSseErrorAsync(string errorMessage, CancellationToken ct)
    {
        await Response.WriteAsync("event: error\n", ct);
        await WriteSseLineAsync(JsonSerializer.Serialize(new { error = errorMessage }, _jsonOptions), ct);
    }

}
