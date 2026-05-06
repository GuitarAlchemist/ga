namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Finds the notes shared between two chords and describes their interval
/// role in each. Pure SKILL.md-driven — the body teaches the LLM to call
/// <c>ga_dsl_eval</c> with the <c>domain.commonTones</c> closure. Second
/// canary for the DSL-eval pattern (Phase 2b of
/// <c>docs/plans/2026-05-06-skills-orchestration-architecture.md</c>;
/// transpose was the first, PR #146).
/// </summary>
/// <remarks>
/// The wrapper produces no answer of its own; it emits the SKILL.md body
/// verbatim. The actual common-tones computation happens when the LLM
/// follows the body's instructions and invokes <c>ga_dsl_eval</c>. Same
/// thin-wrapper pattern as <see cref="TransposeSkill"/>.
/// </remarks>
public sealed class CommonTonesSkill(ILogger<CommonTonesSkill> logger) : IOrchestratorSkill
{
    private const string SkillFolderName = "common-tones";

    private static readonly Lazy<string> _bodyCache = new(
        () => CatalogSkillMdLoader.LoadBodyOrFallback(
            SkillFolderName,
            "Find common tones between two chords by calling ga_dsl_eval with closureName 'domain.commonTones' and args { chord1, chord2 }. The closure returns a formatted string listing each shared note's interval role in both chords (root / 3rd / 5th / 7th / extension)."),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Name        => "CommonTones";
    public string Description =>
        "Finds notes shared between two chords and describes their interval role " +
        "in each (root / 3rd / 5th / 7th / extension). Used for pivot-chord choice, " +
        "smooth voice leading, and modulation prep. Routes through ga_dsl_eval to " +
        "the domain.commonTones closure; the closure handles the role-mapping that " +
        "LLMs fumble on extended/altered chords.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "What notes do Cmaj7 and Am7 share?",
        "Common tones between G and D7",
        "What's shared between Cmaj9 and Em7?",
        "Pivot tones from F to Bb7",
        "Notes in common between Dm7 and G7",
        "What do C major and A minor share?",
    ];

    public bool CanHandle(string message) => false; // semantic-routing only

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var body = _bodyCache.Value;
        logger.LogDebug("CommonTonesSkill: returned {Length} chars (LLM will dispatch ga_dsl_eval)", body.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = body,
            Confidence = 1.0f,
            Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md", "Closure: domain.commonTones (via ga_dsl_eval)"],
        });
    }
}
