namespace GaApi.Services;

using System.Runtime.CompilerServices;
using System.Text;
using Configuration;
using Microsoft.Extensions.Options;

/// <summary>
///     Coordinates semantic enrichment, prompt construction, and conversations
///     before delegating to the underlying large language model.
///     Inspired by Spring Boot service orchestration patterns.
/// </summary>
public sealed class ChatbotSessionOrchestrator(
    IOllamaChatService chatClient,
    ISemanticKnowledgeSource semanticKnowledge,
    IOptionsSnapshot<ChatbotOptions> options,
    ILogger<ChatbotSessionOrchestrator> logger)
{
    private readonly IOllamaChatService _chatClient = chatClient;
    private readonly ILogger<ChatbotSessionOrchestrator> _logger = logger;
    private readonly ChatbotOptions _options = options.Value;
    private readonly ISemanticKnowledgeSource _semanticKnowledge = semanticKnowledge;

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
            normalized = normalized
                .TakeLast(_options.HistoryLimit)
                .ToList();
        }

        return normalized;
    }

    public Task<string> GetResponseAsync(ChatSessionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var normalizedHistory = NormalizeHistory(request.ConversationHistory);
        return GetResponseInternalAsync(request.Message, normalizedHistory, request.UseSemanticSearch,
            cancellationToken);
    }

    public async IAsyncEnumerable<string> StreamResponseAsync(
        ChatSessionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedHistory = NormalizeHistory(request.ConversationHistory);
        var systemPrompt = await BuildSystemPromptAsync(request.Message, request.UseSemanticSearch, cancellationToken);

        await foreach (var chunk in _chatClient.ChatStreamAsync(
                           request.Message,
                           normalizedHistory,
                           systemPrompt,
                           cancellationToken))
        {
            yield return chunk;
        }
    }

    public async Task<string> BuildSystemPromptAsync(
        string message,
        bool useSemanticSearch,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string? context = null;
        if (useSemanticSearch && _options.EnableSemanticSearch)
        {
            context = await BuildSemanticContextAsync(message, cancellationToken);
        }

        return BuildSystemPrompt(context);
    }

    private async Task<string> GetResponseInternalAsync(
        string message,
        List<ChatMessage> history,
        bool useSemanticSearch,
        CancellationToken cancellationToken)
    {
        var systemPrompt = await BuildSystemPromptAsync(message, useSemanticSearch, cancellationToken);

        return await _chatClient.ChatAsync(
            message,
            history,
            systemPrompt,
            cancellationToken);
    }

    private async Task<string?> BuildSemanticContextAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            var results = await _semanticKnowledge.SearchAsync(
                message,
                Math.Max(1, _options.SemanticSearchLimit),
                cancellationToken);

            if (results.Count == 0)
            {
                return null;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Relevant guitar knowledge:");

            foreach (var result in results
                         .Where(r => !string.IsNullOrWhiteSpace(r.Content))
                         .Take(Math.Max(1, _options.SemanticContextDocuments)))
            {
                builder.AppendLine()
                    .AppendLine(result.Content.Trim());
            }

            return builder.ToString();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Semantic enrichment failed. Continuing without additional context.");
            return null;
        }
    }

    private static string BuildSystemPrompt(string? context)
    {
        var prompt = new StringBuilder();
        prompt.AppendLine("You are Guitar Alchemist, an expert guitar teacher and music theory assistant.");
        prompt.AppendLine("You help guitarists learn chords, scales, techniques, and music theory.");
        prompt.AppendLine("Provide clear, practical advice with specific fretboard examples where useful.");
        prompt.AppendLine("Explain complex concepts in simple terms, tailored to guitarists.");
        prompt.AppendLine();

        if (!string.IsNullOrWhiteSpace(context))
        {
            prompt.AppendLine("Use the following knowledge to help answer the user's question:");
            prompt.AppendLine(context.Trim());
            prompt.AppendLine();
        }

        prompt.AppendLine("Be concise but thorough. Use markdown formatting when helpful.");
        prompt.AppendLine("If you do not know something, say so honestly.");

        return prompt.ToString();
    }
}

public sealed record ChatSessionRequest(
    string Message,
    IEnumerable<ChatMessage>? ConversationHistory,
    bool UseSemanticSearch);
