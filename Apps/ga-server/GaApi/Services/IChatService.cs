namespace GaApi.Services;

public interface IChatService
{
    IAsyncEnumerable<string> ChatStreamAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<string> ChatAsync(
        string userMessage,
        List<ChatMessage>? conversationHistory = null,
        string? systemPrompt = null,
        CancellationToken cancellationToken = default);

    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
}
