namespace GaApi.Hubs;

using System.Collections.Concurrent;
using System.Text;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using Microsoft.AspNetCore.SignalR;
using Services;

/// <summary>
///     SignalR hub for real-time chatbot interactions with shared orchestration pipeline.
/// </summary>
public sealed class ChatbotHub(
    ILogger<ChatbotHub> logger,
    ProductionOrchestrator orchestrator,
    ChatbotSessionOrchestrator sessionOrchestrator,
    ISemanticKnowledgeSource semanticKnowledge)
    : Hub
{
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();

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
        var cancellationToken = Context.ConnectionAborted;

        try
        {
            var response = await orchestrator.AnswerAsync(
                new ChatRequest(trimmedMessage), cancellationToken);

            // Emit routing metadata to client
            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
            await Clients.Caller.SendAsync("MessageRoutingMetadata", new
            {
                agentId = routing.AgentId,
                confidence = routing.Confidence,
                routingMethod = routing.RoutingMethod
            });

            // Stream answer in chunks
            var answer = response.NaturalLanguageAnswer ?? string.Empty;
            foreach (var chunk in SplitIntoChunks(answer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Clients.Caller.SendAsync("ReceiveMessageChunk", chunk);
            }

            // Normalise and store history
            var updatedHistory = sessionOrchestrator.NormalizeHistory(
                history.Concat([
                    new ChatMessage { Role = "user", Content = trimmedMessage },
                    new ChatMessage { Role = "assistant", Content = answer }
                ]));
            _conversations[connectionId] = updatedHistory;

            await Clients.Caller.SendAsync("MessageComplete", answer);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Streaming cancelled for {ConnectionId}", connectionId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error processing message from {ConnectionId}", connectionId);
            await Clients.Caller.SendAsync("Error", "Failed to process message. Please try again.");
        }
    }

    public Task ClearHistory()
    {
        var connectionId = Context.ConnectionId;
        _conversations.TryRemove(connectionId, out _);
        logger.LogInformation("Cleared conversation history for {ConnectionId}", connectionId);
        return Task.CompletedTask;
    }

    public Task<List<ChatMessage>> GetHistory()
    {
        var connectionId = Context.ConnectionId;
        return _conversations.TryGetValue(connectionId, out var history)
            ? Task.FromResult(history.ToList())
            : Task.FromResult(new List<ChatMessage>());
    }

    public async Task<List<SemanticSearchResult>> SearchKnowledge(string query, int limit = 10)
    {
        try
        {
            var cancellationToken = Context.ConnectionAborted;
            var results = await semanticKnowledge.SearchAsync(query, limit, cancellationToken);

            return
            [
                .. results.Select((r, i) => new SemanticSearchResult
                {
                    Id = $"voicing-{i}",
                    Content = r.Content,
                    Category = "Voicings",
                    Score = r.Score,
                    Reason = "Semantic match"
                })
            ];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching knowledge for query: {Query}", query);
            throw;
        }
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client {ConnectionId} connected to chatbot hub", Context.ConnectionId);
        await Clients.Caller.SendAsync("Connected", "Welcome to Guitar Alchemist Chatbot!");
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        logger.LogInformation("Client {ConnectionId} disconnected from chatbot hub", connectionId);
        _conversations.TryRemove(connectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    private static IEnumerable<string> SplitIntoChunks(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        var sentences = System.Text.RegularExpressions.Regex
            .Split(text, @"(?<=[.!?])\s+")
            .Where(s => !string.IsNullOrWhiteSpace(s));

        foreach (var sentence in sentences)
            yield return sentence;
    }
}
