namespace GA.Business.Core.Orchestration.Intents;

using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Routes set-class-algebra queries (Z-relations, prime forms, ICVs, Forte
/// labels, set classes) to <see cref="IIxAlgebraService"/>. Replaces the
/// legacy <c>KeywordAlgebraPromptClassifier</c> string-keyword path.
/// </summary>
/// <remarks>
/// The service itself still does the work — this intent just carries the
/// routing metadata so <see cref="SemanticIntentRouter"/> can dispatch via
/// embedding similarity instead of keyword regex.
/// </remarks>
public sealed class AlgebraIntent(IIxAlgebraService algebraService) : IIntent
{
    public string Id => "algebra";

    public string Description =>
        "Computes atonal-set-theory facts about pitch-class sets: prime form, " +
        "interval-class vector (ICV), Forte label, Z-relation between two sets, " +
        "set-class summary. Pure finite math, no LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Are 0146 and 0137 z-related?",
        "What is the prime form of [0,1,4,6]?",
        "Compute the ICV of {0,2,4,7}",
        "Forte number for 0146",
        "Are these Z-related: 0136 and 0146?",
        "Set class summary for [0,1,3,7]",
        "Interval-class vector of 014",
    ];

    public async Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        var answer = await algebraService.TryAnswerAsync(query, cancellationToken);
        if (answer is null)
        {
            return new IntentResult(
                Answer: "I couldn't extract a pitch-class set or recognise the algebra question. " +
                        "Try a query like 'are 0146 and 0137 z-related' or 'prime form of [0,1,4,6]'.",
                Confidence: 0.0f,
                RoutingMethodOverride: "ix-algebra");
        }

        return new IntentResult(
            Answer: answer.NaturalLanguageAnswer,
            Confidence: 1.0f,
            Evidence: answer.Facts.Select(kv => $"{kv.Key}: {kv.Value}").ToList(),
            RoutingMethodOverride: "ix-algebra",
            Grounding: new IntentGroundingEvidence(
                Source:    answer.Grounding.Source,
                Revision:  answer.Grounding.Revision,
                QueryType: answer.Grounding.QueryType,
                Facts:     answer.Grounding.Facts));
    }
}
