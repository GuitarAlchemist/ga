namespace GA.Business.Core.Orchestration.Intents;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Adapts an existing <see cref="IOrchestratorSkill"/> to the unified
/// <see cref="IIntent"/> surface so it can participate in
/// <see cref="SemanticIntentRouter"/> dispatch alongside non-skill intents
/// (algebra, tab handling).
/// </summary>
/// <remarks>
/// The adapter forwards <see cref="IIntent.Description"/> and
/// <see cref="IIntent.ExamplePrompts"/> from the wrapped skill (skills must
/// expose <c>ExamplePrompts</c> to opt in to semantic routing). Execution is
/// delegated to the skill's existing <c>ExecuteAsync</c>; the wrapped
/// <see cref="AgentResponse"/> is mapped back to <see cref="IntentResult"/>.
/// </remarks>
public sealed class OrchestratorSkillIntent(IOrchestratorSkill skill) : IIntent
{
    public string Id => $"skill.{skill.Name.ToLowerInvariant().Replace(' ', '-')}";

    public string Description => skill.Description;

    public IReadOnlyList<string> ExamplePrompts => skill.ExamplePrompts;

    public async Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        var response = await skill.ExecuteAsync(query, cancellationToken);

        // SkillMdDrivenWrapperBase emits a sentinel evidence tag of the form
        // "grounding.source: ga.dsl@<closureName>" when the LLM successfully
        // invoked ga_dsl_eval against the canonical closure. Lift it into a
        // real GroundingSource/QueryType pair so the chat wire payload
        // matches the algebra intent's contract — closing the visibility
        // gap that the 2026-05-07 smoke set surfaced. Roadmap P0 #1.
        const string Prefix = "grounding.source: ga.dsl@";
        var groundingTag = response.Evidence?.FirstOrDefault(e => e.StartsWith(Prefix, StringComparison.Ordinal));
        var (groundingSource, groundingQueryType) = groundingTag is null
            ? (null, null)
            : ((string?)"ga.dsl", (string?)groundingTag[Prefix.Length..]);

        return new IntentResult(
            Answer: response.Result,
            Confidence: response.Confidence,
            Evidence: response.Evidence?.ToList(),
            RoutingMethodOverride: "orchestrator-skill-semantic",
            GroundingSource: groundingSource,
            GroundingRevision: groundingSource is null ? null : "registered",
            GroundingQueryType: groundingQueryType);
    }
}
