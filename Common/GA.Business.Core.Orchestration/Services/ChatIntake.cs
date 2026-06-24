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
/// Behavior-preserving for the first transport (<c>POST /api/chatbot/chat</c>): the
/// only inputs forwarded to the orchestrator are <c>Message</c> + <c>SessionId</c>,
/// matching the controller before the seam existed. Length-cap unification (the
/// divergent 2000/4000/none across transports) is a deliberate policy change deferred
/// until all three transports route through the seam — see the <c>#1</c> plan section.
/// </remarks>
public sealed class ChatIntake(
    IChatApplicationService chatService,
    ILlmConcurrencyGate concurrencyGate) : IChatIntake
{
    public async Task<Result<ChatResponse, ChatIntakeError>> IntakeAsync(
        ChatIntakeRequest request,
        CancellationToken cancellationToken = default)
    {
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
            var response = await chatService.ChatAsync(
                new ChatRequest(message, SessionId: request.SessionId),
                cancellationToken);
            return Result<ChatResponse, ChatIntakeError>.Success(response);
        }
        finally
        {
            concurrencyGate.Release();
        }
    }
}
