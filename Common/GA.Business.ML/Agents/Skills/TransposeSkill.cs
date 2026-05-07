namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using GA.Business.ML.Skills;
using Microsoft.Extensions.Logging;

/// <summary>
/// Transposes a chord by a named musical interval. The C# wrapper owns the
/// routing metadata (<c>Name</c>, <c>Description</c>, <c>ExamplePrompts</c>)
/// so the <c>SemanticIntentRouter</c> can dispatch via the
/// <c>IIntent</c> registry, then delegates <c>ExecuteAsync</c> to a lazily-
/// constructed <see cref="SkillMdDrivenSkill"/> that runs the LLM-in-the-loop
/// pass with <c>skills/transpose/SKILL.md</c> as the system prompt and the
/// MCP tool surface (including <c>ga_dsl_eval</c>) attached as
/// function-calling tools.
/// </summary>
/// <remarks>
/// This is path "B" from the 2026-05-06 Phase 2 finding: keep
/// <c>IIntent</c> registration on the C# wrapper (so routing metadata is
/// curated by hand), but route the actual answer generation through
/// Anthropic + <c>ga_dsl_eval</c> instead of emitting the SKILL.md body
/// verbatim. Without this, the LLM-in-the-loop path is wired but never
/// invoked — see
/// <c>docs/plans/2026-05-06-skills-orchestration-architecture.md</c>
/// §"Phase 2 finding" for the rationale.
/// </remarks>
public sealed class TransposeSkill : IOrchestratorSkill
{
    private const string SkillFolderName = "transpose";

    private readonly Lazy<SkillMdDrivenSkill> _inner;
    private readonly ILogger<TransposeSkill> _logger;

    public TransposeSkill(
        IMcpToolsProvider toolsProvider,
        IChatClientFactory chatClientFactory,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<TransposeSkill>();
        var innerLogger = loggerFactory.CreateLogger<SkillMdDrivenSkill>();

        _inner = new Lazy<SkillMdDrivenSkill>(() =>
        {
            var path = Path.Combine(SkillMdPlugin.ResolveSkillsPath(), SkillFolderName, "SKILL.md");
            var skillMd = SkillMdParser.TryParse(path)
                ?? throw new InvalidOperationException(
                    $"TransposeSkill: skills/{SkillFolderName}/SKILL.md is missing or unparseable at {path}");
            return new SkillMdDrivenSkill(skillMd, toolsProvider, chatClientFactory, innerLogger);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

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

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("TransposeSkill: delegating to SkillMdDrivenSkill (LLM + ga_dsl_eval)");
        var inner = await _inner.Value.ExecuteAsync(message, cancellationToken);

        // The wrapper still owns the AgentId/Evidence shape that downstream
        // tests + UIs depend on; only the Result text comes from the LLM call.
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = inner.Result,
            Confidence = inner.Confidence,
            Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md", "Closure: domain.transposeChord (via ga_dsl_eval)"],
        };
    }
}
