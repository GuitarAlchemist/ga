namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Suggests a structured practice routine for a stated goal — jazz comping,
/// soloing, ear training, fingerstyle technique, etc. Pure catalog skill,
/// zero LLM calls. Body is loaded from
/// <c>skills/practice-routine/SKILL.md</c> so the markdown stays the single
/// source of truth.
/// </summary>
public sealed class PracticeRoutineSkill(ILogger<PracticeRoutineSkill> logger) : IOrchestratorSkill
{
    private const string SkillFolderName = "practice-routine";

    private static readonly Lazy<string> _bodyCache = new(
        () => CatalogSkillMdLoader.LoadBodyOrFallback(
            SkillFolderName,
            "Practice routines should match your goal: jazz comping, improvisation, ear training, fingerstyle, repertoire, or technique. Pick a 15-30 minute daily slot you can sustain rather than the time you'd ideally like."),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Name        => "PracticeRoutine";
    public string Description =>
        "Returns a structured practice plan for guitar/piano goals — jazz " +
        "comping, soloing, ear training, fingerstyle, repertoire, barre " +
        "technique. Templates with daily timings and drills. No LLM call.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Give me a practice routine for jazz comping",
        "What's a good practice plan for improvisation?",
        "How do I practice ear training?",
        "What should I practice for fingerstyle?",
        "Drill for barre chords",
        "Exercises for soloing over changes",
    ];

    public bool CanHandle(string message) => false; // semantic-routing only

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var body = _bodyCache.Value;
        logger.LogDebug("PracticeRoutineSkill: returned {Length} chars", body.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = body,
            Confidence = 1.0f,
            Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md"],
        });
    }
}
