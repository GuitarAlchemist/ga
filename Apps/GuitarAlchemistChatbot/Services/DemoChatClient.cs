namespace GuitarAlchemistChatbot.Services;

using System.Runtime.CompilerServices;

/// <summary>
///     Demo chat client that provides full functionality using in-memory inference
/// </summary>
public class DemoChatClient(ILogger<DemoChatClient> logger) : IChatClient
{
    private readonly InMemoryMusicTheoryEngine _musicEngine = new();

    public ChatClientMetadata Metadata => new("Demo Guitar Alchemist", new Uri("https://localhost"), "demo");

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await Task.Delay(200, cancellationToken); // Simulate processing time

        var response = await GenerateIntelligentResponseAsync(chatMessages, options, cancellationToken);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, response));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> chatMessages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await GenerateIntelligentResponseAsync(chatMessages, options, cancellationToken);

        // Simulate streaming by yielding words one at a time
        var words = response.Split(' ');

        for (var i = 0; i < words.Length; i++)
        {
            await Task.Delay(30, cancellationToken); // Simulate typing delay

            var word = i == words.Length - 1 ? words[i] : words[i] + " ";

            // Create update with just the text content
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(word)]
            };
        }
    }

    public void Dispose()
    {
        // Nothing to dispose in demo mode
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return null;
    }

    private async Task<string> GenerateIntelligentResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options,
        CancellationToken cancellationToken)
    {
        try
        {
            var userMessage = messages.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? "";
            var conversationHistory = messages.Where(m => m.Role != ChatRole.System).ToList();

            logger.LogInformation("Generating in-memory response for: {Message}", userMessage);

            // Check if this is a function call request
            if (options?.Tools?.Any() == true)
            {
                var functionResult = await _musicEngine.ProcessFunctionCallAsync(userMessage);
                if (functionResult != null)
                {
                    return functionResult;
                }
            }

            // Generate contextual response based on conversation
            return await _musicEngine.GenerateResponseAsync(userMessage, conversationHistory);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating in-memory response");
            return
                "I encountered an issue processing your request. Could you please rephrase your question about guitar or music theory?";
        }
    }

    public TService? GetService<TService>(object? serviceKey = null)
    {
        return default;
    }
}
