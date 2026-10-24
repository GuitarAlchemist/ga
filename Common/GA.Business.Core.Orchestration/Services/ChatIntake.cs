namespace GA.Business.Core.Orchestration.Services;

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
    public Task<Result<ChatResponse, ChatIntakeError>> IntakeAsync(
        ChatIntakeRequest request,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(request, null, cancellationToken);

    public Task<Result<ChatResponse, ChatIntakeError>> IntakeStreamingAsync(
        ChatIntakeRequest request, Func<string, Task> onToken, CancellationToken cancellationToken = default) =>
        ExecuteAsync(request, onToken, cancellationToken);

    private async Task<Result<ChatResponse, ChatIntakeError>> ExecuteAsync(
        ChatIntakeRequest request, Func<string, Task>? onToken, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var message = request.Message?.Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return Result<ChatResponse, ChatIntakeError>.Failure(
                new ChatIntakeError.Validation("Message cannot be empty."));
        }

        if (!await concurrencyGate.TryEnterAsync(cancellationToken))
        {
            return Result<ChatResponse, ChatIntakeError>.Failure(new ChatIntakeError.Busy());
        }

        try
        {
            var chatRequest = new ChatRequest(message, SessionId: request.SessionId, History: request.History);
            var emittedText = false;
            async Task EmitAsync(string token)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(token)) return;
                await onToken!(token);
                emittedText = true;
            }
            var response = onToken is null
                ? await chatService.ChatAsync(chatRequest, cancellationToken)
                : await chatService.ChatStreamingAsync(chatRequest, EmitAsync, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            // Readiness blocks and pre-token fallback can return a final answer without tokens.
            if (onToken is not null && !emittedText && !string.IsNullOrEmpty(response.NaturalLanguageAnswer))
                await EmitAsync(response.NaturalLanguageAnswer);
            return Result<ChatResponse, ChatIntakeError>.Success(response);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }
}
