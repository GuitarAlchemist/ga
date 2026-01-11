namespace GaApi.Hubs;

using System.Collections.Concurrent;
using System.Text;
using Microsoft.AspNetCore.SignalR;
using Services;

/// <summary>
///     SignalR hub for real-time chatbot interactions with shared orchestration pipeline.
/// </summary>
public sealed class ChatbotHub(
    ILogger<ChatbotHub> logger,
    ChatbotSessionOrchestrator sessionOrchestrator,
    ISemanticKnowledgeSource semanticKnowledge)
    : Hub
{
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();

    private readonly ILogger<ChatbotHub> _logger = logger;
    private readonly ISemanticKnowledgeSource _semanticKnowledge = semanticKnowledge;
    private readonly ChatbotSessionOrchestrator _sessionOrchestrator = sessionOrchestrator;

    public async Task SendMessage(string message, bool useSemanticSearch = true)
    {
        var connectionId = Context.ConnectionId;
        var trimmedMessage = message?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedMessage))
        {
            await Clients.Caller.SendAsync("Error", "Message cannot be empty.");
            return;
        }

        var history = _conversations.GetOrAdd(connectionId, _ => []);
        var request = new ChatSessionRequest(trimmedMessage, history, useSemanticSearch);
        var responseBuilder = new StringBuilder();
        var cancellationToken = Context.ConnectionAborted;

        try
        {
            await foreach (var chunk in _sessionOrchestrator.StreamResponseAsync(request, cancellationToken))
            {
                responseBuilder.Append(chunk);
                await Clients.Caller.SendAsync("ReceiveMessageChunk", chunk);
            }

            var fullResponse = responseBuilder.ToString();

            var updatedHistory = _sessionOrchestrator.NormalizeHistory(
                history.Concat(new[]
                {
                    new ChatMessage { Role = "user", Content = trimmedMessage },
                    new ChatMessage { Role = "assistant", Content = fullResponse }
                }));

            _conversations[connectionId] = updatedHistory;

            await Clients.Caller.SendAsync("MessageComplete", fullResponse);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Streaming cancelled for {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message from {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("Error", "Failed to process message. Please try again.");
        }
    }

    public Task ClearHistory()
    {
        var connectionId = Context.ConnectionId;
        _conversations.TryRemove(connectionId, out _);
        _logger.LogInformation("Cleared conversation history for {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }

    public Task<List<ChatMessage>> GetHistory()
    {
        var connectionId = Context.ConnectionId;
        if (_conversations.TryGetValue(connectionId, out var history))
        {
            return Task.FromResult(history.ToList());
        }

        return Task.FromResult(new List<ChatMessage>());
    }

    public async Task<List<SemanticSearchResult>> SearchKnowledge(string query, int limit = 10)
    {
        try
        {
            var cancellationToken = Context.ConnectionAborted;
            var results = await _semanticKnowledge.SearchAsync(query, limit, cancellationToken);

            return [.. results.Select((r, i) => new SemanticSearchResult
            {
                Id = $"voicing-{i}",
                Content = r.Content,
                Category = "Voicings",
                Score = r.Score,
                Reason = "Semantic match"
            })];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching knowledge for query: {Query}", query);
            throw;
        }
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client {ConnectionId} connected to chatbot hub", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", "Welcome to Guitar Alchemist Chatbot!");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        _logger.LogInformation("Client {ConnectionId} disconnected from chatbot hub", connectionId);
        _conversations.TryRemove(connectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
///     Semantic search result payload for hub consumers.
/// </summary>
public sealed class SemanticSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Reason { get; set; } = string.Empty;
}
