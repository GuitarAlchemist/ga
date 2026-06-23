namespace GaApi.Services;

using Configuration;
using Microsoft.Extensions.Options;

/// <summary>
///     Normalizes conversation history for the chatbot pipeline: drops empty/non
///     user-or-assistant messages, trims content, and caps the kept history at
///     <see cref="ChatbotOptions.HistoryLimit" />. Consumed by
///     <c>ChatbotHub</c> before per-connection storage.
/// </summary>
/// <remarks>
///     The prompt-construction and LLM-delegation methods this type used to own
///     (<c>GetResponseAsync</c>, <c>StreamResponseAsync</c>, and their private
///     helpers) had no callers — every GaApi chat surface routes through
///     <c>IChatApplicationService</c> — and were removed in the architecture
///     deepening campaign (slice #8b). The remaining responsibility is narrow
///     enough that it could later fold into a static util; see the campaign plan.
/// </remarks>
public sealed class ChatbotSessionOrchestrator(
    IOptionsSnapshot<ChatbotOptions> options)
{
    private readonly ChatbotOptions _options = options.Value;

    public List<ChatMessage> NormalizeHistory(IEnumerable<ChatMessage>? history)
    {
        var normalized = history?
            .Where(message => message is not null)
            .Select(message => message!)
            .Where(message => !string.IsNullOrWhiteSpace(message.Content))
            .Where(message => message.Role is "user" or "assistant")
            .Select(message => new ChatMessage
            {
                Role = message.Role,
                Content = message.Content.Trim()
            })
            .ToList() ?? [];

        if (_options.HistoryLimit > 0 && normalized.Count > _options.HistoryLimit)
        {
            normalized = [.. normalized.TakeLast(_options.HistoryLimit)];
        }

        return normalized;
    }
}
