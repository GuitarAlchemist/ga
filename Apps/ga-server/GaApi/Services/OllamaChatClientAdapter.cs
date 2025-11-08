namespace GaApi.Services;

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

/// <summary>
///     Adapter that exposes <see cref="IOllamaChatService" /> as an <see cref="IChatClient" /> for components
///     (e.g. Microsoft Agent Framework) that rely on the new Extensions.AI abstractions.
/// </summary>
public sealed class OllamaChatClientAdapter : IChatClient
{
    private readonly Uri _baseUri;
    private readonly IOllamaChatService _chatService;
    private readonly ILogger<OllamaChatClientAdapter> _logger;
    private readonly string _model;

    public OllamaChatClientAdapter(
        IOllamaChatService chatService,
        IConfiguration configuration,
        ILogger<OllamaChatClientAdapter> logger)
    {
        _chatService = chatService;
        _logger = logger;
        _baseUri = new Uri(configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
        _model = configuration["Ollama:ChatModel"] ?? "llama3.2:3b";
    }

    public ChatClientMetadata Metadata => new("Ollama Chat", _baseUri, _model);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (systemPrompt, history, userMessage) = PrepareConversation(chatMessages);

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            _logger.LogWarning("Chat invocation is missing a user message. Returning empty response.");
            return new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, string.Empty));
        }

        var responseText = await _chatService.ChatAsync(
            userMessage,
            history,
            systemPrompt,
            cancellationToken);

        return new ChatResponse(new Microsoft.Extensions.AI.ChatMessage(ChatRole.Assistant, responseText));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (systemPrompt, history, userMessage) = PrepareConversation(chatMessages);

        if (string.IsNullOrWhiteSpace(userMessage))
        {
            yield break;
        }

        await foreach (var chunk in _chatService.ChatStreamAsync(
                           userMessage,
                           history,
                           systemPrompt,
                           cancellationToken))
        {
            if (string.IsNullOrEmpty(chunk))
            {
                continue;
            }

            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)]
            };
        }
    }

    public void Dispose()
    {
        // Adapter does not own any unmanaged resources.
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    private static (string? SystemPrompt, List<ChatMessage> History, string UserMessage) PrepareConversation(
        IEnumerable<Microsoft.Extensions.AI.ChatMessage> chatMessages)
    {
        var systemPrompt = chatMessages.LastOrDefault(m => m.Role == ChatRole.System)?.Text;
        var history = new List<ChatMessage>();

        foreach (var message in chatMessages)
        {
            if (message.Role == ChatRole.User || message.Role == ChatRole.Assistant)
            {
                history.Add(new ChatMessage
                {
                    Role = message.Role == ChatRole.User ? "user" : "assistant",
                    Content = message.Text ?? string.Empty
                });
            }
        }

        var userMessage = history.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty;

        if (history.Count > 0 && history[^1].Role == "user")
        {
            history.RemoveAt(history.Count - 1);
        }

        return (systemPrompt, history, userMessage);
    }

    public TService? GetService<TService>(object? serviceKey = null)
    {
        return default;
    }
}
