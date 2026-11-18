namespace GA.Business.Core.Orchestration.Services;

using System.Runtime.CompilerServices;
using System.Threading.Channels;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Core.Functional;

/// <summary>
/// Default <see cref="IChatIntake"/>: validate → acquire the shared
/// <see cref="ILlmConcurrencyGate"/> → dispatch through the canonical
/// <see cref="IChatApplicationService"/> (the existing decorator stack:
/// readiness → fallback → trace → orchestrator) → release. Returns a typed
/// <see cref="ChatIntakeError"/> on rejection so the transport adapter frames the
/// outcome; this type never produces an HTTP status or SSE byte.
/// </summary>
/// <remarks>
/// Both dispatch modes preserve message, opaque session identity, and caller history.
/// Length-cap unification remains a separate policy change.
/// </remarks>
public sealed class ChatIntake(
    IChatApplicationService chatService,
    ILlmConcurrencyGate concurrencyGate) : IChatIntake
{
    /// <summary>Events the pipeline may produce before the transport reads them.</summary>
    private const int EventBufferSize = 64;

    private static readonly AgentRoutingMetadata UnknownRouting = new("direct", 0f, "none");

    public async Task<Result<ChatResponse, ChatIntakeError>> IntakeAsync(
        ChatIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Validate(request) is not { } message)
        {
            return Result<ChatResponse, ChatIntakeError>.Failure(EmptyMessage);
        }

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            return Result<ChatResponse, ChatIntakeError>.Failure(new ChatIntakeError.Busy());
        }

        try
        {
            var response = await chatService.ChatAsync(ToChatRequest(request, message), cancellationToken);
            return Result<ChatResponse, ChatIntakeError>.Success(response);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }

    public async IAsyncEnumerable<ChatEvent> RunAsync(
        ChatIntakeRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var runId = Guid.NewGuid().ToString("N");
        if (Validate(request) is not { } message)
        {
            yield return new ChatEvent.Failed(runId, EmptyMessage.Reason, EmptyMessage);
            yield break;
        }

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            yield return new ChatEvent.Failed(runId, "Service is busy.", new ChatIntakeError.Busy());
            yield break;
        }

        // The pipeline writes events into a bounded channel while the transport reads them,
        // so a slow client holds back the model instead of buffering the whole answer.
        using var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var events = Channel.CreateBounded<ChatEvent>(
            new BoundedChannelOptions(EventBufferSize) { SingleReader = true, SingleWriter = true });
        var producer = ProduceAsync(runId, ToChatRequest(request, message), events.Writer, run);
        try
        {
            await foreach (var chatEvent in events.Reader.ReadAllAsync(cancellationToken))
            {
                yield return chatEvent;
            }
        }
        finally
        {
            // The transport stopped reading, was cancelled, or saw the last event: end the
            // run, then free the gate only once the pipeline has really stopped.
            await run.CancelAsync();
            await producer;
            concurrencyGate.Release();
        }
    }

    /// <summary>
    /// Runs the pipeline and writes its events. Routing is held back until the first text
    /// token, so a fallback that replaces the answer before any text also replaces its
    /// routing; text that arrives before any routing report goes out after an unknown
    /// routing, so the event order holds for every pipeline. Never throws: a pipeline error becomes <see cref="ChatEvent.Failed"/>, and a
    /// cancelled run just ends.
    /// </summary>
    private async Task ProduceAsync(
        string runId, ChatRequest request, ChannelWriter<ChatEvent> writer, CancellationTokenSource run)
    {
        var token = run.Token;
        (AgentRoutingMetadata Routing, GroundingMetadata? Grounding)? reported = null;
        var routed = false;
        var emittedText = false;

        async Task RouteAsync(AgentRoutingMetadata routing, GroundingMetadata? grounding)
        {
            await writer.WriteAsync(
                new ChatEvent.Routing(runId, routing.AgentId, routing.Confidence, routing.RoutingMethod, grounding),
                token);
            routed = true;
        }

        async Task EmitAsync(string text)
        {
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrEmpty(text)) return;
            if (!routed) await RouteAsync(reported?.Routing ?? UnknownRouting, reported?.Grounding);
            await writer.WriteAsync(new ChatEvent.TextDelta(runId, text), token);
            emittedText = true;
        }

        Task ReportRoutingAsync(AgentRoutingMetadata routing, GroundingMetadata? grounding)
        {
            if (!emittedText) reported = (routing, grounding);
            return Task.CompletedTask;
        }

        try
        {
            var response = await chatService.ChatStreamingAsync(request, EmitAsync, token, ReportRoutingAsync);
            token.ThrowIfCancellationRequested();
            // Readiness blocks and pre-token fallback return a final answer without tokens;
            // its routing is the response's own, not one reported for a replaced answer.
            if (!emittedText) reported = null;
            if (!routed) await RouteAsync(response.Routing ?? UnknownRouting, response.Grounding);
            if (!emittedText && !string.IsNullOrEmpty(response.NaturalLanguageAnswer))
                await EmitAsync(response.NaturalLanguageAnswer);
            await writer.WriteAsync(new ChatEvent.Completed(runId, response), token);
        }
        catch (Exception) when (token.IsCancellationRequested)
        {
            // The caller cancelled or the transport stopped reading; nobody is listening.
        }
        catch (Exception exception)
        {
            try
            {
                await writer.WriteAsync(
                    new ChatEvent.Failed(runId, $"{exception.GetType().Name}: {exception.Message}"), token);
            }
            catch (OperationCanceledException)
            {
                // The transport stopped reading before the failure could be reported.
            }
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private static readonly ChatIntakeError.Validation EmptyMessage = new("Message cannot be empty.");

    private static string? Validate(ChatIntakeRequest request) =>
        request.Message?.Trim() is { Length: > 0 } message ? message : null;

    private static ChatRequest ToChatRequest(ChatIntakeRequest request, string message) =>
        new(message, SessionId: request.SessionId, History: request.History);
}
