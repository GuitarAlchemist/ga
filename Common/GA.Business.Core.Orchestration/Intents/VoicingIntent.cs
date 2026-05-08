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

    // ExamplePrompts intentionally avoid "easy / beginner / first / basic /
    // simple / common" tokens that BeginnerChordsSkill owns — the embedding
    // router doesn't have semantic-exclusion rules so prompt overlap turns
    // into a routing tiebreak.
    //
    // Two example classes by design:
    //   1. Technical-voicing terms (drop-N, rootless, shell, quartal,
    //      closed/open position) — these strengthen the embedding centroid
    //      for VoicingIntent even though most also match the regex guard's
    //      keyword set, which fires first for them.
    //   2. NO-keyword phrasings that the regex guard cannot catch ("finger
    //      Cmaj9", "chord diagram for F#m7", "play F#m7 on guitar") — these
    //      are the semantic-intent-voicing path's reason to exist. Without
    //      at least a few of these the router falls through to TabOptimize
    //      or BeginnerChords for fingering-style prose.
    //
    // Codex CLI QA 2026-05-07 surfaced the BeginnerChords overlap; the
    // post-QA smoke set surfaced that "finger Cmaj9" was falling to
    // tab.optimize once the conflicting example was removed.
    public IReadOnlyList<string> ExamplePrompts =>
    [
        // Technical-voicing terms (regex guard fires first for most of these
        // but they shape the embedding centroid for cosine matching)
        "Show me Drop 2 voicings of Cmaj7",
        "Drop 3 voicings of G7",
        "Rootless Dm7 voicings",
        "Shell voicings for Bm7b5",
        "Quartal voicings in A minor",
        "Alternate voicings of F#m7 up the neck",
        "Closed-position voicings of Cmaj9",
        // No-keyword phrasings — these are what semantic-intent-voicing exists for
        "How do I finger Cmaj9?",
        "Show me a chord diagram for F#m7",
        "What's the best way to play Bm7 on guitar?",
        "I need an alternative for the standard Am7",
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
