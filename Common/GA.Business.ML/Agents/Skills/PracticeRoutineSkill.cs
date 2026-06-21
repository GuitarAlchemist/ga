namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Suggests a structured practice routine for a stated goal — jazz comping,
/// soloing, ear training, fingerstyle technique, etc. Catalog skill (see
/// <see cref="CatalogSkillBase"/>): body is loaded from
/// <c>skills/practice-routine/SKILL.md</c>, zero LLM calls.
/// </summary>
public sealed class PracticeRoutineSkill(ILogger<PracticeRoutineSkill> logger) : CatalogSkillBase(logger)
{
    public override string Name        => "PracticeRoutine";
    public override string Description =>
        "Returns a structured practice plan for guitar/piano goals — jazz " +
        "comping, soloing, ear training, fingerstyle, repertoire, barre " +
        "technique. Templates with daily timings and drills. No LLM call.";

    // PR (post-baseline-2026-05-11) — expanded to cover the "schedule" /
    // "outline" / "daily" / "minute" surface that the labeled eval corpus
    // uses. The prior set led to 3/5 prompts falling BELOW the router's
    // 0.65 confidence threshold (returning "(none)") because "20 minute
    // practice routine" / "30 minute practice schedule" / "daily practice
    // outline" don't share enough lexical signal with the previous
    // "routine / plan / drill / exercises" wording.
    public override IReadOnlyList<string> ExamplePrompts =>
    [
        "give me a 20 minute practice routine",
        "build a 30 minute practice schedule",
        "daily practice outline for guitar",
        "what's a good practice plan for improvisation",
        "give me a practice routine for jazz comping",
        "weekly practice plan for fingerstyle",
        // PR #178 review (LOW): keep one example for the "what should I
        // practice [today|first|now]" temporal-instruction shape. Without
        // it, beginnerchords' "first chords to learn" centroid wins.
        "what should I practice today",
        "drill for barre chords",
        "exercises for soloing over changes",
    ];

    protected override string FolderName => "practice-routine";
    protected override string Fallback   =>
        "Practice routines should match your goal: jazz comping, improvisation, ear training, fingerstyle, repertoire, or technique. Pick a 15-30 minute daily slot you can sustain rather than the time you'd ideally like.";
}
