namespace GaApi.Controllers;

using AgUi;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.AgUi;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Agents;
using Services;

/// <summary>
/// AG-UI protocol endpoint for Guitar Alchemist.
/// Wraps <see cref="IHarmonicChatOrchestrator"/> output in typed AG-UI SSE events
/// so that React components (DiatonicChordTable, VexTabViewer) receive structured
/// domain data alongside the streaming text answer.
/// </summary>
/// <remarks>Both transports use IChatIntake for validation, gating, and decorated dispatch.</remarks>
[ApiController]
[Route("api/chatbot")]
public class AgUiChatController(
    ILogger<AgUiChatController> logger,
    IChatIntake chatIntake,
    ContextualChordService contextualChordService) : ControllerBase
{
    /// <summary>
    ///     Returns all registered orchestrator skills with their name and description.
    ///     Agents and MCP clients can call this to discover what skills are active
    ///     and what trigger phrases they match, without reading source files.
    /// </summary>
    [HttpGet("skills")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ListSkills([FromServices] IEnumerable<IOrchestratorSkill> skills)
        => Ok(skills.Select(s => new { s.Name, s.Description }));

    /// <summary>
    ///     Non-streaming sibling of <see cref="AgUiStream"/>.
    ///     Runs the same orchestrator path and returns the final answer + domain metadata
    ///     as a JSON object — suitable for agent frameworks that cannot consume SSE.
    /// </summary>
    [HttpPost("agui/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> AgUiJson([FromBody] RunAgentInput input, CancellationToken cancellationToken)
    {
        var (userMessage, history) = SplitCurrentTurn(input.Messages);

        if (string.IsNullOrWhiteSpace(userMessage))
            return BadRequest("No user message found in the request.");

        // Phase C P1 (task #107 INFO-003) — server-issued cookie is the SessionId
        // source. ThreadId is a client-controlled AG-UI state primitive, NOT a
        // server-side memory partition key (using it would be session fixation, the
        // VULN-001 shape). The seam takes SessionId as opaque; it owns gating +
        // dispatch and frames nothing — this thin adapter frames the typed result.
        var sessionId = HttpChatSessionCookie.GetOrIssue(HttpContext);

        var result = await chatIntake.IntakeAsync(
            new ChatIntakeRequest(userMessage, sessionId, history),
            cancellationToken);

        return result.Match<IActionResult>(
            response => Ok(new
            {
                answer = response.NaturalLanguageAnswer,
                routing = response.Routing,
                candidates = response.Candidates,
                filters = response.QueryFilters,
                progression = response.Progression,
            }),
            error => error switch
            {
                ChatIntakeError.Busy => StatusCode(StatusCodes.Status503ServiceUnavailable,
                    "Service is busy. Please try again."),
                ChatIntakeError.Validation v => BadRequest(v.Reason),
                _ => StatusCode(StatusCodes.Status500InternalServerError, "Unexpected error.")
            });
    }

    /// <summary>
    ///     Send a message to the GA agent and receive an AG-UI–compliant SSE stream.
    ///     Event sequence: RUN_STARTED → STATE_SNAPSHOT → STEP_STARTED → text events →
    ///     domain CUSTOM events → STATE_DELTA → RUN_FINISHED.
    ///     On error, RUN_ERROR is emitted (status code is never changed after headers commit).
    /// </summary>
    [HttpPost("agui/stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task AgUiStream([FromBody] RunAgentInput input, CancellationToken cancellationToken)
    {
        var (userMessage, history) = SplitCurrentTurn(input.Messages);

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // Phase C P1 (task #107 INFO-003) — issue the session cookie BEFORE
        // StartAsync because Response.Cookies.Append modifies response headers,
        // which become read-only once StartAsync commits the wire. We pay the
        // small cost of minting a cookie before the concurrency gate check
        // because SSE never returns 503 (it emits a RUN_ERROR frame on a 200
        // response), so there's no "503-orphan" trade-off to optimise for.
        // Validation already ran (userMessage check above), so we don't mint
        // cookies for shape-invalid requests.
        var sessionId = HttpChatSessionCookie.GetOrIssue(HttpContext);

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");

        await Response.StartAsync(cancellationToken);

        var writer = new AgUiEventWriter(Response);
        var threadId = input.ThreadId;     // AG-UI client-side state primitive
        var runId = AgUiEventWriter.NewRunId();
        var msgId = $"msg_{runId[..8]}";

        try
        {
            var started = false;
            async Task StartTextAsync()
            {
                if (started) return;
                await writer.WriteRunStartedAsync(threadId, runId, cancellationToken);
                await writer.WriteStateSnapshotAsync(new
                {
                    key = (string?)null,
                    mode = (string?)null,
                    chords = Array.Empty<object>(),
                    candidates = Array.Empty<object>(),
                    progression = Array.Empty<string>(),
                    analysisPhase = "idle",
                    lastError = (string?)null,
                }, cancellationToken);
                await writer.WriteTextStartAsync(msgId, cancellationToken);
                started = true;
            }

            var result = await chatIntake.IntakeStreamingAsync(
                new ChatIntakeRequest(userMessage, sessionId, history),
                async token =>
                {
                    await StartTextAsync();
                    await writer.WriteTextChunkAsync(msgId, token, cancellationToken);
                },
                cancellationToken);
            if (result.IsFailure)
            {
                var (message, code) = result.GetErrorOrThrow() switch
                {
                    ChatIntakeError.Busy => ("Service is busy. Please try again.", "SERVICE_BUSY"),
                    ChatIntakeError.Validation validation => (validation.Reason, "INVALID_REQUEST"),
                    _ => ("Failed to process message. Please try again.", "INTERNAL_ERROR")
                };
                await writer.WriteRunErrorAsync(message, code, cancellationToken);
                return;
            }

            var response = result.GetValueOrThrow();
            await StartTextAsync();
            await writer.WriteTextEndAsync(msgId, cancellationToken);

            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");

            // ── 5. STEP_STARTED (routing metadata — institutional rule) ─────────
            await writer.WriteStepStartedAsync(routing.AgentId, runId, cancellationToken);

            // ── 6. Domain CUSTOM events ─────────────────────────────────────────
            var filters = response.QueryFilters;
            var finalKey = filters?.Key;
            var finalMode = (string?)null;

            if (finalKey is not null)
            {
                // Split "G major" → mode = "major"
                var parts = finalKey.Split(' ', 2);
                if (parts.Length == 2) finalMode = parts[1];

                var chordsResult = await contextualChordService.GetChordsForKeyAsync(finalKey);
                if (chordsResult.IsSuccess)
                    await writer.WriteCustomAsync("ga:diatonic", chordsResult.GetValueOrThrow(), cancellationToken);

                // ga:scale — 7 note descriptors for the live fretboard scale overlay
                var scaleNotes = ScaleNoteService.GetNotes(finalKey);
                if (scaleNotes is not null)
                    await writer.WriteCustomAsync("ga:scale", scaleNotes, cancellationToken);
            }

            if (response.Candidates is { Count: > 0 })
                await writer.WriteCustomAsync("ga:candidates", response.Candidates, cancellationToken);

            if (response.Progression is not null)
                await writer.WriteCustomAsync("ga:progression", response.Progression.Steps, cancellationToken);

            // ── 7. STATE_DELTA — sync final analysis phase ─────────────────────
            await writer.WriteStateDeltaAsync(
            [
                new JsonPatchOperation("replace", "/analysisPhase", "complete"),
                new JsonPatchOperation("replace", "/key",  finalKey),
                new JsonPatchOperation("replace", "/mode", finalMode),
            ], cancellationToken);

            // ── 8. RUN_FINISHED ────────────────────────────────────────────────
            await writer.WriteRunFinishedAsync(threadId, runId, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("AG-UI stream cancelled for run {RunId}", runId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in AG-UI stream for run {RunId}", runId);
            await writer.WriteRunErrorAsync("Failed to process message. Please try again.", "INTERNAL_ERROR", cancellationToken);
        }
    }

    // The last user message is the current turn. History carries only the other nonblank turns,
    // because the orchestrator treats ChatRequest.History as prior context (GaChatbot.Api parity).
    private static (string? Message, List<ConversationTurn> History) SplitCurrentTurn(IReadOnlyList<AgUiMessage> messages)
    {
        var currentIndex = -1;
        for (var i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role == "user")
            {
                currentIndex = i;
                break;
            }
        }

        List<ConversationTurn> history =
        [
            .. messages
                .Where((message, index) => index != currentIndex && !string.IsNullOrWhiteSpace(message.Content))
                .Select(message => new ConversationTurn(message.Role, message.Content!, DateTimeOffset.UtcNow))
        ];
        return (currentIndex < 0 ? null : messages[currentIndex].Content?.Trim(), history);
    }
}

/// <summary>
/// AG-UI RunAgentInput request body.
/// Mirrors the TypeScript <c>RunAgentInput</c> from @ag-ui/core.
/// </summary>
public sealed record RunAgentInput(
    string ThreadId,
    string RunId,
    IReadOnlyList<AgUiMessage> Messages,
    object? State = null);

/// <summary>Single message in an AG-UI thread.</summary>
public sealed record AgUiMessage(
    string Role,
    string? Content,
    string? Id = null);
