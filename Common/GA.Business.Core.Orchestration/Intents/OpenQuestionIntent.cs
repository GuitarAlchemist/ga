namespace GA.Business.Core.Orchestration.Intents;

using GA.Business.ML.Agents.Intents;

/// <summary>
/// Routing target for open questions that no deterministic skill answers: why music sounds the
/// way it does, or which famous songs use a mode. It always declines, so the orchestrator hands
/// the turn, with its history, to the LLM agent path.
/// </summary>
/// <remarks>
/// Without these anchors the semantic router gave such questions to the nearest skill, which
/// answered a different question: "Why does a minor chord sound sad compared to a major chord?"
/// scored 0.824 on <c>skill.progressionmood</c> via "How can I make my chords sound sadder?" and
/// got techniques to darken a progression; "Give me an example of a famous song in Dorian
/// mode." scored 0.777 on <c>skill.modes</c> and got the Dorian formula, without a song
/// (2026-11-18). The examples compete with those skills' anchors by meaning, not by keyword;
/// a question that scores higher on a skill's own example still goes to the skill.
/// </remarks>
public sealed class OpenQuestionIntent : IIntent
{
    /// <summary>Intent id shown in routing traces.</summary>
    public const string IntentId = "agent.open-question";

    public string Id => IntentId;

    public string Description =>
        "Open questions answered from musical knowledge rather than computed: why a chord, key or " +
        "mode sounds the way it does, and which famous songs use a mode.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        // Why something sounds the way it does (a skill only changes a progression's mood).
        "Why does a minor chord sound sad compared to a major chord?",
        "Why do major chords sound happy?",
        "Why does music in a minor key sound melancholic?",
        "Why does the Locrian mode sound so unstable?",
        // Famous songs in a mode (the modes skill holds formulas, not songs).
        "Give me an example of a famous song in Dorian mode.",
        "What famous songs use the Mixolydian mode?",
        "Name a well-known song written in Phrygian.",
        "Which popular songs are in the Aeolian mode?",
        "Can you name a tune that uses the Lydian mode?",
    ];

    public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
        Task.FromResult(new IntentResult(
            "Open question: the conversational agents answer it.",
            Confidence: 0f,
            RoutingMethodOverride: "semantic-intent-open-question",
            Declined: true));
}
