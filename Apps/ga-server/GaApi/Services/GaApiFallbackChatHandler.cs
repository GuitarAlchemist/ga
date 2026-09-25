namespace GaApi.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;

/// <summary>Uses the host's selected provider for the explicitly enabled direct-chat fallback.</summary>
public sealed class GaApiFallbackChatHandler(IChatService chatService) : IFallbackChatHandler
{
    public Task<string> AnswerAsync(
        string message,
        IReadOnlyList<ConversationTurn>? history,
        CancellationToken cancellationToken = default) =>
        chatService.ChatAsync(
            message,
            history?.Select(turn => new ChatMessage { Role = turn.Role, Content = turn.Content }).ToList(),
            cancellationToken: cancellationToken);
}
