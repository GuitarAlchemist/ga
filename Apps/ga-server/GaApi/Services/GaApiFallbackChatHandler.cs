namespace GaApi.Services;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using Microsoft.Extensions.AI;
using AiChatMessage = Microsoft.Extensions.AI.ChatMessage;

/// <summary>Uses the host's selected provider for the explicitly enabled direct-chat fallback.</summary>
public sealed class GaApiFallbackChatHandler(IChatClient chatClient) : IFallbackChatHandler
{
    public async Task<string> AnswerAsync(
        string message,
        IReadOnlyList<ConversationTurn>? history,
        CancellationToken cancellationToken = default)
    {
        List<AiChatMessage> messages =
        [
            .. (history ?? []).Select(turn => new AiChatMessage(ToChatRole(turn.Role), turn.Content)),
            new(ChatRole.User, message)
        ];

        var response = await chatClient.GetResponseAsync(messages, cancellationToken: cancellationToken);
        return response.Text;
    }

    private static ChatRole ToChatRole(string role) =>
        role.ToLowerInvariant() switch
        {
            "assistant" => ChatRole.Assistant,
            "system" => ChatRole.System,
            _ => ChatRole.User
        };
}
