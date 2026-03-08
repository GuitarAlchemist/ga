namespace GaApi.Controllers;

using AgUi;
using GA.Business.Core.Orchestration.AgUi;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using Helpers;
using Services;

/// <summary>
/// AG-UI protocol endpoint for Guitar Alchemist.
/// Wraps <see cref="ProductionOrchestrator"/> output in typed AG-UI SSE events
/// so that React components (DiatonicChordTable, VexTabViewer) receive structured
/// domain data alongside the streaming text answer.
/// </summary>
[ApiController]
[Route("api/chatbot")]
public class AgUiChatController(
    ILogger<AgUiChatController> logger,
    ProductionOrchestrator orchestrator,
    ContextualChordService contextualChordService,
    ILlmConcurrencyGate concurrencyGate) : ControllerBase
{
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
        var userMessage = input.Messages
            .LastOrDefault(m => m.Role == "user")?.Content?.Trim();

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        Response.StatusCode = StatusCodes.Status200OK;
        Response.Headers.Append("Content-Type",       "text/event-stream");
        Response.Headers.Append("Cache-Control",      "no-cache");
        Response.Headers.Append("X-Accel-Buffering",  "no");

        await Response.StartAsync(cancellationToken);

        var writer   = new AgUiEventWriter(Response);
        var threadId = input.ThreadId;
        var runId    = AgUiEventWriter.NewRunId();
        var msgId    = $"msg_{runId[..8]}";

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            await writer.WriteRunErrorAsync("Service is busy. Please try again.", "SERVICE_BUSY", cancellationToken);
            return;
        }

        try
        {
            // ── 1. RUN_STARTED ─────────────────────────────────────────────────
            await writer.WriteRunStartedAsync(threadId, runId, cancellationToken);

            // ── 2. Initial STATE_SNAPSHOT (empty domain state) ─────────────────
            await writer.WriteStateSnapshotAsync(new
            {
                key            = (string?)null,
                mode           = (string?)null,
                chords         = Array.Empty<object>(),
                candidates     = Array.Empty<object>(),
                progression    = Array.Empty<string>(),
                analysisPhase  = "idle",
                lastError      = (string?)null,
            }, cancellationToken);

            // ── 3. Invoke orchestrator with true token streaming ────────────────
            var chatRequest = new GA.Business.Core.Orchestration.Models.ChatRequest(userMessage, threadId);

            // ── 4. STEP_STARTED (routing metadata first — institutional rule) ───
            // We emit STEP_STARTED after routing completes; streaming fills in the gap.
            await writer.WriteTextStartAsync(msgId, cancellationToken);

            var response = await orchestrator.AnswerStreamingAsync(
                chatRequest,
                async token => await writer.WriteTextChunkAsync(msgId, token, cancellationToken),
                cancellationToken);

            await writer.WriteTextEndAsync(msgId, cancellationToken);

            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
            await writer.WriteStepStartedAsync(routing.AgentId, runId, cancellationToken);

            // ── 5. Domain CUSTOM events ─────────────────────────────────────────
            var filters       = response.QueryFilters;
            var finalKey      = filters?.Key;
            var finalMode     = (string?)null;

            if (finalKey is not null)
            {
                // Split "G major" → mode = "major"
                var parts = finalKey.Split(' ', 2);
                if (parts.Length == 2) finalMode = parts[1];

                var chordsResult = await contextualChordService.GetChordsForKeyAsync(finalKey);
                if (chordsResult.IsSuccess)
                    await writer.WriteCustomAsync("ga:diatonic", chordsResult.GetValueOrThrow(), cancellationToken);
            }

            if (response.Candidates is { Count: > 0 })
                await writer.WriteCustomAsync("ga:candidates", response.Candidates, cancellationToken);

            if (response.Progression is not null)
                await writer.WriteCustomAsync("ga:progression", response.Progression.Steps, cancellationToken);

            // ── 6. STATE_DELTA — sync final analysis phase ─────────────────────
            await writer.WriteStateDeltaAsync(
            [
                new JsonPatchOperation("replace", "/analysisPhase", "complete"),
                new JsonPatchOperation("replace", "/key",  finalKey),
                new JsonPatchOperation("replace", "/mode", finalMode),
            ], cancellationToken);

            // ── 7. RUN_FINISHED ────────────────────────────────────────────────
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
        finally
        {
            concurrencyGate.Release();
        }
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
