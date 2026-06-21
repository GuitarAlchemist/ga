namespace GaApi.Hubs;

using System.Collections.Concurrent;
using System.Text;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Trace;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Services;

/// <summary>
///     SignalR hub for real-time chatbot interactions with shared orchestration pipeline.
///     Anonymous: this hub backs the public demo at demos.guitaralchemist.com/chatbot/,
///     so it accepts unauthenticated WebSocket negotiations. Per-conversation state
///     is keyed by Context.ConnectionId, not by an authenticated user identity.
///     Depends on the host-neutral <see cref="IChatApplicationService"/> rather
///     than ProductionOrchestrator directly so all GaApi chat surfaces share
///     one composition root for orchestration features (codex CLI 2026-05-07).
/// </summary>
[AllowAnonymous]
public sealed class ChatbotHub(
    ILogger<ChatbotHub> logger,
    IChatApplicationService chatService,
    IAgenticTraceCapture traceCapture,
    ChatbotSessionOrchestrator sessionOrchestrator,
    ISemanticKnowledgeSource semanticKnowledge,
    ILlmConcurrencyGate concurrencyGate)
    : Hub
{
    private static readonly ConcurrentDictionary<string, List<ChatMessage>> _conversations = new();
    private static readonly TimeSpan _pipelineBudget = TimeSpan.FromSeconds(25);
    private const int MaxStoredMessages = 50;

    // Per-connection rate limit. The HTTP PartitionedRateLimiter only fires
    // on the WebSocket upgrade — once a connection is established, every
    // SendMessage invocation is in-band and bypasses the limiter. Without
    // this gate, a single anonymous client can call SendMessage in a tight
    // loop and burn the deployment's ANTHROPIC_API_KEY (each call hits
    // Anthropic Haiku 4.5). See PR #151 review (security sec-2,
    // reliability rel-002). Token bucket: 1 message/sec sustained, 5 burst.
    private const int    RateLimitMaxBurst    = 5;
    private static readonly TimeSpan RateLimitRefillInterval = TimeSpan.FromSeconds(1);
    private const int    MaxMessageLength     = 4_000;
    private static readonly ConcurrentDictionary<string, RateLimitBucket> _rateLimits = new();

    private sealed class RateLimitBucket
    {
        public int Tokens;
        public DateTime LastRefill;
    }

    private static bool TryConsumeRateLimitToken(string connectionId)
    {
        var bucket = _rateLimits.GetOrAdd(connectionId, _ => new RateLimitBucket
        {
            Tokens     = RateLimitMaxBurst,
            LastRefill = DateTime.UtcNow,
        });
        lock (bucket)
        {
            var now     = DateTime.UtcNow;
            var elapsed = now - bucket.LastRefill;
            var refill  = (int)(elapsed.TotalSeconds / RateLimitRefillInterval.TotalSeconds);
            if (refill > 0)
            {
                bucket.Tokens     = Math.Min(RateLimitMaxBurst, bucket.Tokens + refill);
                bucket.LastRefill = now;
            }
            if (bucket.Tokens <= 0) return false;
            bucket.Tokens--;
            return true;
        }
    }

    public async Task SendMessage(string message, bool useSemanticSearch = true)
    {
        var connectionId = Context.ConnectionId;
        var trimmedMessage = message?.Trim();

        if (string.IsNullOrWhiteSpace(trimmedMessage))
        {
            await Clients.Caller.SendAsync("Error", "Message cannot be empty.");
            return;
        }

        if (trimmedMessage.Length > MaxMessageLength)
        {
            await Clients.Caller.SendAsync("Error",
                $"Message exceeds {MaxMessageLength} characters. Please shorten and retry.");
            return;
        }

        if (!TryConsumeRateLimitToken(connectionId))
        {
            logger.LogInformation(
                "ChatbotHub: rate limit hit for connection {ConnectionId} — anonymous flooding suspected",
                connectionId);
            await Clients.Caller.SendAsync("Error",
                "You're sending messages too quickly. Please wait a moment.");
            return;
        }

        var history = _conversations.GetOrAdd(connectionId, _ => []);
        var connectionAborted = Context.ConnectionAborted;

        if (!await concurrencyGate.TryEnterAsync(connectionAborted))
        {
            await Clients.Caller.SendAsync("Error", "Service is busy. Please try again in a few seconds.");
            return;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(connectionAborted);
        cts.CancelAfter(_pipelineBudget);
        var cancellationToken = cts.Token;

        try
        {
            // SessionId = Context.ConnectionId — the anonymous demo doesn't
            // have an authenticated user identity, but SignalR's server-issued
            // ConnectionId IS a 128-bit cryptographically-random per-tab handle
            // (per Microsoft.AspNetCore.SignalR's default IConnectionIdGenerator).
            // That's exactly what MemoryHook needs to scope retrieval per
            // conversation rather than across every anonymous tab — see PR
            // #157 Phase A (storage layer) for the leak this closes.
            //
            // After this Phase B change ships AND Memory:EnrichOnRetrieve=true
            // is flipped in config, MemoryHook will scope reads/writes to
            // (sessionId=connectionId) instead of skipping retrieval entirely.
            var response = await chatService.ChatAsync(
                new ChatRequest(trimmedMessage, SessionId: connectionId),
                cancellationToken);

            // Emit routing metadata to client. Grounding is included when the
            // intent that handled the request was deterministic-compute-backed
            // (e.g. ix-algebra) so the SPA can surface "this came from a
            // verifiable source vs an LLM guess" — the same field
            // GaChatbot.Api's ChatJsonResponse and ChatbotController return.
            // Without this, the deployed /chatbot/ UI silently strips grounding
            // even though ProductionOrchestrator produces it.
            var routing = response.Routing ?? new AgentRoutingMetadata("direct", 0f, "none");
            await Clients.Caller.SendAsync("MessageRoutingMetadata", new
            {
                agentId = routing.AgentId,
                confidence = routing.Confidence,
                routingMethod = routing.RoutingMethod,
                grounding = response.Grounding is null
                    ? null
                    : new
                    {
                        source = response.Grounding.Source,
                        revision = response.Grounding.Revision,
                        queryType = response.Grounding.QueryType
                    },
                trace = traceCapture.Build()
            });

            // Stream answer in chunks
            var answer = response.NaturalLanguageAnswer ?? string.Empty;
            foreach (var chunk in SplitIntoChunks(answer))
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Clients.Caller.SendAsync("ReceiveMessageChunk", chunk);
            }

            // Normalise and store history; cap at MaxStoredMessages to bound per-session memory
            var updatedHistory = sessionOrchestrator.NormalizeHistory(
                history.Concat([
                    new ChatMessage { Role = "user", Content = trimmedMessage },
                    new ChatMessage { Role = "assistant", Content = answer }
                ]));
            _conversations[connectionId] = updatedHistory.Count > MaxStoredMessages
                ? [.. updatedHistory.TakeLast(MaxStoredMessages)]
                : updatedHistory;

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
        finally
        {
            concurrencyGate.Release();
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
            : Task.FromResult<List<ChatMessage>>([]);
    }

    public async Task<List<SemanticSearchResult>> SearchKnowledge(string query, int limit = 10)
    {
        try
        {
            var cancellationToken = Context.ConnectionAborted;
            var results = await semanticKnowledge.SearchAsync(query, Math.Clamp(limit, 1, 50), cancellationToken);

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
            logger.LogError(ex, "Error searching knowledge for query: {Query}",
                query.Length > 100 ? query[..100] + "…" : query);
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
        _rateLimits.TryRemove(connectionId, out _);
        await base.OnDisconnectedAsync(exception);
    }

    private static IEnumerable<string> SplitIntoChunks(string text) =>
        GA.Business.Core.Orchestration.Helpers.SseChunker.SplitIntoChunks(text);
}
