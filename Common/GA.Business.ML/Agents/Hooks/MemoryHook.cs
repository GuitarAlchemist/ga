namespace GA.Business.ML.Agents.Hooks;

using Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hook that enriches requests with relevant memory context and persists
/// high-confidence responses as new memories.
/// </summary>
public sealed class MemoryHook(MemoryStore memoryStore, ILogger<MemoryHook> logger) : IChatHook
{
    /// <summary>
    /// Searches memory for context relevant to the incoming message and prepends it.
    /// </summary>
    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
    {
        var matches = memoryStore.Search(ctx.CurrentMessage);
        if (matches.Count == 0) return Task.FromResult(HookResult.Continue);

        var contextBlock = string.Join("\n", matches.Take(3)
            .Select(m => $"[memory:{m.Key}] {m.Content}"));

        var enriched = $"[Relevant context from memory]\n{contextBlock}\n\n{ctx.CurrentMessage}";
        logger.LogDebug("MemoryHook: injecting {Count} memory entries", matches.Count);
        return Task.FromResult(HookResult.Mutate(enriched));
    }

    /// <summary>
    /// After a response is sent, if confidence is high enough and the response is non-trivial,
    /// persist it as a memory entry for future context enrichment.
    /// </summary>
    public Task<HookResult> OnResponseSent(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (ctx.Response is not { Confidence: >= 0.7f } response) return Task.FromResult(HookResult.Continue);
        if (response.Result.Length < 100) return Task.FromResult(HookResult.Continue);

        // Fire-and-forget write — don't block the response pipeline
        _ = Task.Run(() =>
        {
            try
            {
                var key = $"response_{ctx.CorrelationId:N}";
                memoryStore.Write(key, "response",
                    $"Q: {ctx.OriginalMessage}\nA: {response.Result[..Math.Min(500, response.Result.Length)]}",
                    [response.AgentId]);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MemoryHook: failed to persist response memory");
            }
        }, ct);

        return Task.FromResult(HookResult.Continue);
    }
}
