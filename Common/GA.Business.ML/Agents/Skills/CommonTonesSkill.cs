namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using GA.Business.ML.Skills;
using Microsoft.Extensions.Logging;

/// <summary>
/// Finds the notes shared between two chords and describes their interval
/// role in each. The C# wrapper owns routing metadata (<c>Name</c>,
/// <c>Description</c>, <c>ExamplePrompts</c>) so the
/// <c>SemanticIntentRouter</c> can dispatch via the <c>IIntent</c>
/// registry, then delegates <c>ExecuteAsync</c> to a lazily-constructed
/// <see cref="SkillMdDrivenSkill"/> that runs the LLM-in-the-loop pass
/// with <c>skills/common-tones/SKILL.md</c> as the system prompt and the
/// MCP tool surface (including <c>ga_dsl_eval</c>) attached as
/// function-calling tools. Second canary for the DSL-eval pattern
/// (Phase 2b of
/// <c>docs/plans/2026-05-06-skills-orchestration-architecture.md</c>;
/// transpose was the first, PR #146 / #151).
/// </summary>
/// <remarks>
/// Mirrors path B from <see cref="TransposeSkill"/> (PR #151) so the
/// LLM-in-the-loop path actually fires; otherwise the canary measures the
/// markdown-emitter, which is the wrong thing per the 2026-05-06 Phase 2
/// finding.
/// </remarks>
public sealed class CommonTonesSkill : IOrchestratorSkill
{
    private const string SkillFolderName = "common-tones";

    private readonly Lazy<SkillMdDrivenSkill> _inner;
    private readonly ILogger<CommonTonesSkill> _logger;

    public CommonTonesSkill(
        IMcpToolsProvider toolsProvider,
        IChatClientFactory chatClientFactory,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<CommonTonesSkill>();
        var innerLogger = loggerFactory.CreateLogger<SkillMdDrivenSkill>();

        _inner = new Lazy<SkillMdDrivenSkill>(() =>
        {
            var path = Path.Combine(SkillMdPlugin.ResolveSkillsPath(), SkillFolderName, "SKILL.md");
            var skillMd = SkillMdParser.TryParse(path)
                ?? throw new InvalidOperationException(
                    $"CommonTonesSkill: skills/{SkillFolderName}/SKILL.md is missing or unparseable at {path}");
            return new SkillMdDrivenSkill(skillMd, toolsProvider, chatClientFactory, innerLogger);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

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

    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CommonTonesSkill: delegating to SkillMdDrivenSkill (LLM + ga_dsl_eval)");
        var inner = await _inner.Value.ExecuteAsync(message, cancellationToken);

        // Wrapper still owns AgentId/Evidence so downstream tests + UIs see
        // a stable shape; only the Result text comes from the LLM call.
        return new AgentResponse
        {
            AgentId    = AgentIds.Theory,
            Result     = inner.Result,
            Confidence = inner.Confidence,
            Evidence   = [$"Source: skills/{SkillFolderName}/SKILL.md", "Closure: domain.commonTones (via ga_dsl_eval)"],
        };
    }
}
