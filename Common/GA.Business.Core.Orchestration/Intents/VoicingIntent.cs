namespace GA.Business.Core.Orchestration.Intents;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;

/// <summary>
/// Routes voicing-retrieval queries through <see cref="VoicingAgent"/> as a
/// first-class <see cref="IIntent"/>, so the embedding-similarity router can
/// dispatch them alongside algebra and Path B SKILL.md intents instead of
/// only the explicit regex guard in
/// <see cref="GA.Business.Core.Orchestration.Services.ProductionOrchestrator.TrySelectDeterministicAgent"/>.
/// </summary>
/// <remarks>
/// Coexists with the regex guard, doesn't replace it: the guard catches
/// unambiguous keyword-bearing prompts ("Drop 2 voicings of Cmaj7") fast
/// without an embedding round-trip, while this intent catches semantic
/// variants that miss the keyword set ("show me chord shapes for Am7",
/// "what fingerings exist for F#m"). Codex CLI 2026-05-07 follow-up to the
/// reorder fix in commit a9220957.
///
/// Confidence is sourced from <see cref="AgentResponse.Confidence"/> when
/// available; SemanticIntentRouter combines it with its own embedding score
/// at dispatch time, so a low-confidence intent here doesn't override an
/// authoritative semantic match elsewhere.
/// </remarks>
public sealed class VoicingIntent(VoicingAgent voicingAgent) : IIntent
{
    public string Id => "voicing";

    public string Description =>
        "Retrieves playable guitar voicings (chord shapes / fingerings) from the OPTIC-K corpus. " +
        "Use for queries asking for fingerings, chord diagrams, drop-2 / drop-3 / shell / rootless / " +
        "quartal voicings, or per-instrument shapes (guitar / bass / ukulele). Backed by the 112-dim " +
        "musical embedding (NOT text similarity) so it matches musical structure regardless of " +
        "phrasing.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Show me Drop 2 voicings of Cmaj7",
        "What fingerings exist for Am7?",
        "Give me chord shapes for F#m7",
        "Rootless Dm7 voicings",
        "Drop 3 voicings of G7",
        "Shell voicings for Bm7b5",
        "Quartal voicings in A minor",
        "Easy beginner chord shapes for D major",
    ];

    public async Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default)
    {
        var response = await voicingAgent.ProcessAsync(new AgentRequest { Query = query }, cancellationToken);

        return new IntentResult(
            Answer: response.Result,
            Confidence: response.Confidence,
            Evidence: response.Evidence?.ToList(),
            RoutingMethodOverride: "semantic-intent-voicing");
    }
}
