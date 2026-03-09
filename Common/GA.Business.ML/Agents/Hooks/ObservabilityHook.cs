namespace GA.Business.ML.Agents.Hooks;

using System.Diagnostics;

/// <summary>
/// Adds OpenTelemetry spans at skill lifecycle points using <see cref="ChatbotActivitySource"/>.
/// Extracts the hard-coded activity creation from <c>ProductionOrchestrator</c>
/// into a standalone hook so the orchestrator has zero observability coupling.
/// </summary>
public sealed class ObservabilityHook : IChatHook
{
    // Keyed by MatchedSkillName — started OnBeforeSkill, stopped OnAfterSkill
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Activity?> _activities = new();

    public Task<HookResult> OnBeforeSkill(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (ctx.MatchedSkillName is { } name)
        {
            var activity = ChatbotActivitySource.Source.StartActivity("orchestrator.skill");
            activity?.SetTag("skill.name", name);
            activity?.SetTag("query.length", ctx.CurrentMessage.Length);
            _activities[name] = activity;
        }

        return Task.FromResult(HookResult.Continue);
    }

    public Task<HookResult> OnAfterSkill(ChatHookContext ctx, CancellationToken ct = default)
    {
        if (ctx.MatchedSkillName is { } name &&
            _activities.TryRemove(name, out var activity))
        {
            activity?.SetTag("skill.confidence", ctx.Response?.Confidence);
            activity?.Dispose();
        }

        return Task.FromResult(HookResult.Continue);
    }
}
