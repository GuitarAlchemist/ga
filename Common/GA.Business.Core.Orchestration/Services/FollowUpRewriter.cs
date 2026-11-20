namespace GA.Business.Core.Orchestration.Services;

using System.Text;
using GA.Business.ML.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

/// <summary>
/// Rewrites a follow-up ("And a C shape?", "Show me an easy way to play it") into a question that
/// stands on its own, using the conversation. Routing and the deterministic skills read only the
/// current message; without the rewrite they act on half a question.
/// </summary>
/// <remarks>
/// Enabled by <c>Chatbot:FollowUpRewrite=true</c> (experiment, off by default). Runs only when the
/// turn has history. Any failure, an empty reply, or a reply much longer than a question keeps the
/// user's message unchanged.
/// </remarks>
public sealed class FollowUpRewriter(
    IConfiguration configuration,
    ILogger<FollowUpRewriter> logger,
    IChatClient? chatClient = null)
{
    public const string ConfigKey = "Chatbot:FollowUpRewrite";

    // The prior turns that resolve a reference are almost always the last few.
    private const int MaxTurns = 6;
    private const int MaxTurnChars = 600;
    // A reply far longer than any question is an answer, not a rewrite.
    private const int MinRewriteCap = 300;

    private const string Instructions =
        """
        Rewrite the user's last message as a question that can be understood without the conversation.
        Replace pronouns and elliptical references ("it", "that one", "and a C shape?") with what they refer to,
        taken from the conversation. Keep the user's language, wording, and intent; add nothing else.
        If the last message already stands on its own, return it unchanged.
        Reply with the rewritten message only.
        """;

    public bool Enabled => chatClient is not null && configuration.GetValue<bool>(ConfigKey);

    public async Task<string> RewriteAsync(
        string message,
        IReadOnlyList<ChatHistoryTurn> history,
        CancellationToken cancellationToken = default)
    {
        if (!Enabled || history.Count == 0 || string.IsNullOrWhiteSpace(message)) return message;

        var prompt = new StringBuilder(Instructions).AppendLine().AppendLine("CONVERSATION:");
        foreach (var turn in history.TakeLast(MaxTurns))
        {
            var text = turn.Content.Length > MaxTurnChars ? turn.Content[..MaxTurnChars] + "…" : turn.Content;
            prompt.AppendLine($"{turn.Role}: {text}");
        }
        prompt.AppendLine().AppendLine($"LAST MESSAGE: {message}");

        try
        {
            var response = await chatClient!.GetResponseAsync(
                [new ChatMessage(ChatRole.User, prompt.ToString())],
                new ChatOptions { MaxOutputTokens = 200 },
                cancellationToken);
            var rewritten = response.Text.Trim();
            if (rewritten.Length == 0 || rewritten.Length > Math.Max(MinRewriteCap, message.Length * 3))
            {
                logger.LogDebug("[FollowUpRewrite] Kept the message: unusable rewrite of {Length} chars", rewritten.Length);
                return message;
            }

            return rewritten;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "[FollowUpRewrite] Kept the message: the rewrite failed");
            return message;
        }
    }
}
