namespace GaApi.Services;

using System.Runtime.CompilerServices;
using System.Text;
using Anthropic;
using Anthropic.Models.Messages;

/// <summary>
///     Claude API-based chat service for conversational AI.
///     Uses the official Anthropic SDK with streaming support via SSE.
/// </summary>
public class ClaudeChatService : IOllamaChatService
{
    private readonly AnthropicClient _client;
    private readonly ILogger<ClaudeChatService> _logger;
    private readonly string _model;

    public ClaudeChatService(IConfiguration configuration, ILogger<ClaudeChatService> logger)
    {
        _logger = logger;
        _model = configuration["Anthropic:Model"] ?? "claude-haiku-4-5-20251001";

        var apiKey = configuration["Anthropic:ApiKey"]
            ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        _client = apiKey is not null
            ? new AnthropicClient { ApiKey = apiKey }
            : new AnthropicClient(); // reads ANTHROPIC_API_KEY env var automatically
    }

    public async IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var messages = BuildMessages(conversationHistory, userMessage);

        var parameters = new MessageCreateParams
        {
            Model = _model,
            MaxTokens = 2048,
            Messages = messages,
            System = string.IsNullOrWhiteSpace(systemPrompt) ? default : systemPrompt
        };

        _logger.LogDebug("Claude stream: model={Model}, messages={Count}", _model, messages.Count);

        await foreach (var rawEvent in _client.Messages.CreateStreaming(parameters, cancellationToken))
        {
            if (rawEvent.TryPickContentBlockDelta(out var delta)
                && delta.Delta.TryPickText(out var text))
            {
                yield return text.Text;
            }
        }
    }

    public async Task<string> ChatAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default)
    {
        var sb = new StringBuilder();
        await foreach (var chunk in ChatStreamAsync(userMessage, conversationHistory, systemPrompt, cancellationToken))
        {
            sb.Append(chunk);
        }
        return sb.ToString();
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(true);

    private static List<MessageParam> BuildMessages(List<ChatMessage>? history, string userMessage)
    {
        var messages = new List<MessageParam>();

        if (history != null)
        {
            foreach (var msg in history)
            {
                // Skip system messages — they go in the System parameter, not messages array
                if (string.Equals(msg.Role, "system", StringComparison.OrdinalIgnoreCase))
                    continue;

                messages.Add(new MessageParam
                {
                    Role = string.Equals(msg.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                        ? Role.Assistant
                        : Role.User,
                    Content = msg.Content
                });
            }
        }

        messages.Add(new MessageParam { Role = Role.User, Content = userMessage });
        return messages;
    }
}
