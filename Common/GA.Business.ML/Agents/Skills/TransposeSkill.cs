namespace GA.Business.ML.Agents.Skills;

/// <summary>
/// Transposes a chord by a named musical interval. Pure SKILL.md-driven —
/// the body teaches the LLM to call <c>ga_dsl_eval</c> with the
/// <c>domain.transposeChord</c> closure. First canary for the DSL-eval
/// pattern (Phase 2 of <c>docs/plans/2026-05-06-skills-orchestration-architecture.md</c>).
/// </summary>
/// <remarks>
/// This wrapper produces no answer of its own; it emits the SKILL.md body
/// verbatim. The actual transposition happens when the LLM follows the body's
/// instructions and invokes <c>ga_dsl_eval</c>. This is the "thin wrapper"
/// pattern from PR #126 (catalog skills) applied to a tool-driven skill —
/// proves the C# orchestrator can register a SKILL.md whose substance lives
/// entirely in the markdown + DSL closure.
/// </remarks>
public sealed class TransposeSkill(ILogger<TransposeSkill> logger) : IOrchestratorSkill
{
    private const string SkillFolderName = "transpose";

    private static readonly Lazy<string> _bodyCache = new(
        () => CatalogSkillMdLoader.LoadBodyOrFallback(
            SkillFolderName,
            "Transpose a chord by calling ga_dsl_eval with closure name 'domain.transposeChord' and args { symbol, semitones }. Use the interval-to-semitones table to convert phrasings like 'up a perfect fourth' (5 semitones) or 'down a minor third' (-3 semitones)."),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string Name        => "Transpose";
    public string Description =>
        "Transposes a chord by a named musical interval (e.g. Cmaj7 up a perfect " +
        "fourth = Fmaj7). Routes through ga_dsl_eval to the domain.transposeChord " +
        "closure; teaches the LLM the interval → semitones mapping in the body so " +
        "no enharmonic guessing happens at the model layer.";

    public IReadOnlyList<string> ExamplePrompts =>
    [
        "Transpose Cmaj7 up a perfect fourth",
        "Move this F chord down a minor third",
        "What's Dm7 up a whole step?",
        "Transpose G7 to Eb",
        "Shift Am7 up a fifth",
        "Cmaj7 in the key of G — what chord is that?",
    ];

    public bool CanHandle(string message) => false; // semantic-routing only

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var body = _bodyCache.Value;
        logger.LogDebug("TransposeSkill: returned {Length} chars (LLM will dispatch ga_dsl_eval)", body.Length);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = body,
            Confidence = 1.0f,
            Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md", "Closure: domain.transposeChord (via ga_dsl_eval)"],
        });
    }
}
