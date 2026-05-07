namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Extensions;
using GA.Business.ML.Skills;
using Microsoft.Extensions.Logging;

/// <summary>
/// Base class for the Path B (LLM-in-the-loop) skill template. The C#
/// wrapper owns routing metadata (<see cref="Name"/>,
/// <see cref="Description"/>, <see cref="ExamplePrompts"/>) so the
/// <c>SemanticIntentRouter</c> can dispatch via the <c>IIntent</c>
/// registry, then delegates <see cref="ExecuteAsync"/> to a lazily-
/// constructed <see cref="SkillMdDrivenSkill"/> built from
/// <c>skills/{SkillFolderName}/SKILL.md</c>.
/// </summary>
/// <remarks>
/// Extracted from TransposeSkill / DiatonicChordsSkill which had ~30 lines
/// of identical glue (Lazy plumbing, path resolution, SkillMdParser
/// failure handling, exception passthrough). PR #151 review (architecture
/// arch-F2) flagged the duplication as a maintenance tax that grows
/// linearly with each new Path B canary. Adding a new canary now requires
/// 4 properties + a closure name, ~25 lines.
/// </remarks>
public abstract class SkillMdDrivenWrapperBase : IOrchestratorSkill
{
    private readonly Lazy<SkillMdDrivenSkill> _inner;
    private readonly ILogger _logger;

    /// <summary>
    /// Folder name under <c>skills/</c> containing the SKILL.md backing
    /// this wrapper. The base resolves the full path via
    /// <see cref="SkillMdPlugin.ResolveSkillsPath"/>.
    /// </summary>
    protected abstract string SkillFolderName { get; }

    /// <summary>
    /// AgentId stamped on the response. Conventionally one of the
    /// <c>AgentIds.*</c> constants (e.g. <see cref="AgentIds.Theory"/>).
    /// </summary>
    protected abstract string ResponseAgentId { get; }

    /// <summary>
    /// Closure name that the SKILL.md body teaches the LLM to dispatch
    /// via <c>ga_dsl_eval</c> — used in <see cref="EvidenceTags"/> below.
    /// </summary>
    protected abstract string ClosureName { get; }

    /// <summary>
    /// One-line graceful-degradation message returned when the inner
    /// SkillMdDrivenSkill or the Lazy itself throws. Should be specific
    /// enough that a user knows which capability misfired.
    /// </summary>
    protected abstract string DegradedResponseText { get; }

    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract IReadOnlyList<string> ExamplePrompts { get; }

    protected SkillMdDrivenWrapperBase(
        IMcpToolsProvider toolsProvider,
        IChatClientFactory chatClientFactory,
        ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger(GetType());
        var innerLogger = loggerFactory.CreateLogger<SkillMdDrivenSkill>();

        _inner = new Lazy<SkillMdDrivenSkill>(() =>
        {
            var path = Path.Combine(SkillMdPlugin.ResolveSkillsPath(), SkillFolderName, "SKILL.md");
            var skillMd = SkillMdParser.TryParse(path)
                ?? throw new InvalidOperationException(
                    $"{GetType().Name}: skills/{SkillFolderName}/SKILL.md is missing or unparseable at {path}");
            return new SkillMdDrivenSkill(skillMd, toolsProvider, chatClientFactory, innerLogger);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Tool-driven skills only route via the <see cref="SemanticIntentRouter"/>;
    /// the legacy <c>CanHandle</c> regex shadow is intentionally disabled.
    /// </remarks>
    public bool CanHandle(string message) => false;

    /// <summary>Evidence array stamped on every response, including failures.</summary>
    private string[] EvidenceTags =>
        [$"Source: skills/{SkillFolderName}/SKILL.md", $"Closure: {ClosureName} (via ga_dsl_eval)"];

    /// <inheritdoc />
    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("{Skill}: delegating to SkillMdDrivenSkill (LLM + ga_dsl_eval)", GetType().Name);

        try
        {
            var inner = await _inner.Value.ExecuteAsync(message, cancellationToken);
            return new AgentResponse
            {
                AgentId    = ResponseAgentId,
                Result     = inner.Result,
                Confidence = inner.Confidence,
                Evidence   = EvidenceTags,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Lazy<T,ExecutionAndPublication> caches the construction
            // exception forever; without this catch a missing /
            // unparseable SKILL.md would 500 every subsequent request.
            // Match SkillMdDrivenSkill's swallow pattern so a deployment
            // artefact issue degrades gracefully.
            _logger.LogError(ex, "{Skill}: inner SkillMdDrivenSkill failed", GetType().Name);
            return new AgentResponse
            {
                AgentId    = ResponseAgentId,
                Result     = DegradedResponseText,
                Confidence = 0f,
                Evidence   = EvidenceTags,
            };
        }
    }
}
