namespace GA.Business.ML.Agents.Hooks;

using Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Hook that enriches requests with relevant memory context and persists
/// high-confidence responses as new memories.
/// </summary>
/// <remarks>
/// Retrieval (request-time injection) defaults to OFF — opt in via
/// <c>Memory:EnrichOnRetrieve=true</c>. The store is process-wide and not
/// session-scoped, so injecting accumulated history into every anonymous
/// request leaks chord references between unrelated conversations and
/// breaks any skill that does regex-based parsing on the full message
/// (ChordSubstitutionSkill saw "A" from a prior cached answer when the
/// user asked about "G7"). Persistence still runs so the data is
/// available once the feature is properly session-scoped.
/// </remarks>
public sealed class MemoryHook(
    MemoryStore memoryStore,
    IConfiguration configuration,
    ILogger<MemoryHook> logger) : IChatHook
{
    private readonly bool _enrichOnRetrieve =
        configuration.GetValue<bool?>("Memory:EnrichOnRetrieve") ?? false;

    /// <summary>
    /// Searches memory for context relevant to the incoming message and prepends it.
    /// Off by default; enable with <c>Memory:EnrichOnRetrieve=true</c>.
    /// </summary>
    public Task<HookResult> OnRequestReceived(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (!_enrichOnRetrieve) return Task.FromResult(HookResult.Continue);

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
