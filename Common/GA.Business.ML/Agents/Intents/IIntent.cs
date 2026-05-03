namespace GA.Business.ML.Agents.Intents;

/// <summary>
/// Single semantic surface for chatbot dispatch — replaces ad-hoc string-matching
/// classifiers (<c>KeywordAlgebraPromptClassifier</c>, <c>IsAskingForOptimization</c>,
/// per-skill <c>CanHandle</c> regexes). Each intent carries its routing metadata
/// (<see cref="Description"/> + <see cref="ExamplePrompts"/>) AND its execution path.
/// </summary>
/// <remarks>
/// <para>The router (<see cref="SemanticIntentRouter"/>) embeds every intent's
/// description and example prompts at startup, then per-query computes cosine
/// similarity and picks the top match above a confidence threshold. No LLM
/// call is on the routing path — this is the "function over agent" pattern
/// the migration recommendation doc requires.</para>
/// <para>Intent <see cref="Id"/> values follow the convention
/// <c>{category}.{name}</c> (e.g. <c>skill.modes</c>, <c>algebra</c>,
/// <c>tab.optimize</c>). They surface in <c>AgentRoutingMetadata.AgentId</c>
/// so traces tell you exactly which intent ran.</para>
/// </remarks>
public interface IIntent
{
    /// <summary>Stable identifier surfaced as the agent id in trace metadata.</summary>
    string Id { get; }

    /// <summary>One-paragraph description of what this intent answers. Embedded by
    /// <see cref="SemanticIntentRouter"/> alongside the example prompts.</summary>
    string Description { get; }

    /// <summary>Canonical phrasings that should route to this intent. Three to six
    /// representative examples is the sweet spot; the router takes the max
    /// cosine similarity across all of them.</summary>
    IReadOnlyList<string> ExamplePrompts { get; }

    /// <summary>Run the intent against the user query. Implementations should
    /// return a zero-confidence result rather than throwing for recoverable
    /// errors (e.g. unparseable input) so the chatbot pipeline can degrade
    /// gracefully.</summary>
    Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default);
}

/// <summary>Result returned from <see cref="IIntent.ExecuteAsync"/>.</summary>
/// <param name="Answer">Natural-language answer.</param>
/// <param name="Confidence">0.0 to 1.0. The router's match score is combined
/// with this via <c>min</c> when surfacing the final routing confidence.</param>
/// <param name="Evidence">Optional structured evidence items the chatbot may
/// expose in traces.</param>
/// <param name="RoutingMethodOverride">Optional override for the
/// <c>routing.method</c> trace tag. Defaults to <c>"semantic-intent"</c> at the
/// router level; intents that want their own brand (e.g. <c>"ix-algebra"</c>)
/// can override here.</param>
public sealed record IntentResult(
    string Answer,
    float Confidence = 1.0f,
    IReadOnlyList<string>? Evidence = null,
    string? RoutingMethodOverride = null);
