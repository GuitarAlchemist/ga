namespace GaApi.Services;

public sealed record ChatSessionRequest(
    string Message,
    IEnumerable<ChatMessage>? ConversationHistory,
    bool UseSemanticSearch);
