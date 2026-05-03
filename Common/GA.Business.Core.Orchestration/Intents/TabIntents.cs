namespace GA.Business.Core.Orchestration.Intents;

using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Routes "make this progression smoother / easier to play / optimize the
/// fingering" queries to <see cref="TabAnalysisOrchestrationService.OptimizePathAsync"/>.
/// Replaces the legacy <c>IsAskingForOptimization</c> keyword check
/// (which over-matched on the bare word "easy" — a routing bug).
/// </summary>
public sealed class TabOptimizeIntent(TabAnalysisOrchestrationService service) : IIntent
{
    public string Id => "tab.optimize";

    public string Description =>
        "Optimises the fingering path of a tab to minimise hand movement and " +
        "fret transitions. Requires a tab in the message (lines like " +
        "`e|---0---3---|` or a compact diagram like `x-3-2-0-1-0`).";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Make this progression smoother to play",
        "Optimize the fingering for this tab",
        "Find a better path through this tab",
        "Re-calculate this tab for less hand movement",
        "Smooth out the transitions in this tab",
    ];

    public async Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        var resp = await service.OptimizePathAsync(query, cancellationToken);
        return new IntentResult(
            Answer: resp.NaturalLanguageAnswer,
            Confidence: 1.0f,
            RoutingMethodOverride: "tab-optimize");
    }
}

/// <summary>
/// Routes tab-analysis queries (analyse this tab, what's the progression in
/// this tab) to <see cref="TabAnalysisOrchestrationService.AnalyzeTabAsync"/>.
/// </summary>
public sealed class TabAnalyzeIntent(TabAnalysisOrchestrationService service) : IIntent
{
    public string Id => "tab.analyze";

    public string Description =>
        "Analyses a tab block: parses chords, finds modulation targets, suggests " +
        "next chords. Requires a tab in the message.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Analyse this tab",
        "What's the chord progression in this tab?",
        "Identify the chords in this tab",
        "Walk me through this tab",
    ];

    public async Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        var resp = await service.AnalyzeTabAsync(query, cancellationToken);
        return new IntentResult(
            Answer: resp.NaturalLanguageAnswer,
            Confidence: 1.0f,
            RoutingMethodOverride: "tab-analyze");
    }
}
