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
        // Natural-language phrasings real users type. Added 2026-06-16: the
        // short canonical examples above scored below the router threshold for
        // verbose questions, so "is the pitch class set 0,1,4,6 z-related to
        // another set class" fell through to the LLM path and timed out at 15s
        // instead of reaching this deterministic engine. These are additional
        // embedding anchors (the semantic-routing lever) — not keyword rules.
        "Is the pitch class set 0,1,4,6 Z-related to another set class?",
        "Does the set 0,1,4,6 have a Z-partner, and what is it?",
        "Which set class has the same interval-class vector as 0,1,4,6?",
        "Tell me the Forte label and Z-relation of the pitch-class set 0,1,3,7",
        // NOTE: deliberately NOT adding "what is Forte number 4-Z29" — that is a
        // reverse Forte-label→set LOOKUP the engine doesn't support yet; routing
        // it here would extract {2,9} from "29" and answer the wrong question.
        // Tracked as a missing feature (reverse ForteCatalog lookup).
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
