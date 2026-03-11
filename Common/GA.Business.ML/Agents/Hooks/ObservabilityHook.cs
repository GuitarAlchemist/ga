namespace GA.Business.ML.Agents.Hooks;

using System.Diagnostics;

/// <summary>
/// Adds OpenTelemetry spans at skill lifecycle points using <see cref="ChatbotActivitySource"/>.
/// Extracts the hard-coded activity creation from <c>ProductionOrchestrator</c>
/// into a standalone hook so the orchestrator has zero observability coupling.
/// </summary>
/// <remarks>
/// Registered as a singleton. Activities are keyed by <see cref="ChatHookContext.CorrelationId"/>
/// (a per-request GUID) so concurrent requests hitting the same skill never collide.
/// </remarks>
public sealed class ObservabilityHook : IChatHook
{
    // Keyed by per-request CorrelationId — started OnBeforeSkill, stopped OnAfterSkill.
    // Using Guid key prevents collisions when two concurrent requests match the same skill.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, Activity?> _activities = new();

    public Task<HookResult> OnBeforeSkill(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (ctx.MatchedSkillName is { } name)
        {
            var activity = ChatbotActivitySource.Source.StartActivity("orchestrator.skill");
            activity?.SetTag("skill.name", name);
            activity?.SetTag("query.length", ctx.CurrentMessage.Length);
            _activities[ctx.CorrelationId] = activity;
        }

        return Task.FromResult(HookResult.Continue);
    }

    public Task<HookResult> OnAfterSkill(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (_activities.TryRemove(ctx.CorrelationId, out var activity))
        {
            activity?.SetTag("skill.confidence", ctx.Response?.Confidence);
            activity?.Dispose();
        }

        return Task.FromResult(HookResult.Continue);
    }
}
