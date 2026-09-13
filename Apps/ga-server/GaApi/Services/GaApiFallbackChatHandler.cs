namespace GaApi.Services;

using GA.Business.Core.Orchestration.Abstractions;

/// <summary>Uses the host's selected provider for the explicitly enabled direct-chat fallback.</summary>
public sealed class GaApiFallbackChatHandler(IChatService chatService) : IFallbackChatHandler
{
    public Task<string> AnswerAsync(string message, CancellationToken cancellationToken = default) =>
        chatService.ChatAsync(message, cancellationToken: cancellationToken);
}
