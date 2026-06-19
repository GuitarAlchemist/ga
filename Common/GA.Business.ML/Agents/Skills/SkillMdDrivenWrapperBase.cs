namespace GA.Business.ML.Agents.Skills;

using System.Diagnostics;
using GA.Business.ML.Agents;
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
    /// via <c>ga_dsl_eval</c> — used in <see cref="StaticEvidenceTags"/> below.
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
    /// Tool-driven skills only route via the <see cref="Intents.SemanticIntentRouter"/>;
    /// the legacy <c>CanHandle</c> regex shadow is intentionally disabled.
    /// </remarks>
    public bool CanHandle(string message) => false;

    /// <summary>
    /// Static evidence tags stamped on every response. The dynamic
    /// <c>tools.invoked:*</c> entries from <see cref="SkillMdDrivenSkill"/>
    /// are appended at execution time so callers can audit whether the
    /// LLM actually called <c>ga_dsl_eval</c> against <see cref="ClosureName"/>
    /// or just produced a plausible-looking answer from training data.
    /// </summary>
    private string[] StaticEvidenceTags =>
        [$"Source: skills/{SkillFolderName}/SKILL.md", $"Closure: {ClosureName} (via ga_dsl_eval)"];

    /// <inheritdoc />
    public async Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("{Skill}: delegating to SkillMdDrivenSkill (LLM + ga_dsl_eval)", GetType().Name);

        try
        {
            var inner = await _inner.Value.ExecuteAsync(message, cancellationToken).ConfigureAwait(false);

            // The inner skill records every tool call as a "tools.invoked: <name>"
            // entry in Evidence. Path B should always go through ga_dsl_eval —
            // if it didn't, the answer is LLM-only and we want both the trace
            // and the confidence to reflect that. Roadmap P0 #1.
            var innerEvidence = inner.Evidence ?? [];
            var calledDslEval = innerEvidence.Any(e => e.Contains("ga_dsl_eval", StringComparison.Ordinal));

            var combinedEvidence = StaticEvidenceTags.Concat(innerEvidence).ToList();
            if (calledDslEval)
            {
                // Sentinel tag picked up by OrchestratorSkillIntent so Path B
                // responses surface a real grounding block on the chat wire,
                // matching the algebra intent's contract. The closure name is
                // the strongest queryType signal available — it identifies
                // which deterministic computation produced the answer.
                combinedEvidence.Add($"grounding.source: ga.dsl@{ClosureName}");
            }
            else
            {
                combinedEvidence.Add("warning: ga_dsl_eval was NOT invoked — answer is LLM-only, not deterministic");
                Activity.Current?.SetTag(ChatbotActivitySource.TagToolName, "skill-md");
                Activity.Current?.SetTag(ChatbotActivitySource.TagSkillName, GetType().Name);
                Activity.Current?.SetTag(ChatbotActivitySource.TagClosureName, ClosureName);
                Activity.Current?.SetTag(
                    ChatbotActivitySource.TagToolFailureReason,
                    ChatbotActivitySource.FailureReasons.GaDslEvalNotInvoked);
                _logger.LogWarning(
                    "{Skill}: LLM produced an answer without invoking ga_dsl_eval. " +
                    "Closure {Closure} should have been called. Evidence: {Evidence}",
                    GetType().Name, ClosureName, string.Join(" | ", innerEvidence));
            }

            // Confidence floor when ga_dsl_eval skipped: the answer text may
            // still be correct (LLMs can transpose Cmaj7 in their head) but
            // by Path B's design contract it isn't deterministic, so callers
            // arbitrating confidence shouldn't treat it as if it were.
            var effectiveConfidence = calledDslEval
                ? inner.Confidence
                : Math.Min(inner.Confidence, 0.5f);

            return new AgentResponse
            {
                AgentId    = ResponseAgentId,
                Result     = inner.Result,
                Confidence = effectiveConfidence,
                Evidence   = combinedEvidence,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Lazy<T,ExecutionAndPublication> caches the construction
            // exception forever; without this catch a missing /
            // unparseable SKILL.md would 500 every subsequent request.
            // Match SkillMdDrivenSkill's swallow pattern so a deployment
            // artefact issue degrades gracefully.
            Activity.Current?.SetTag(ChatbotActivitySource.TagToolName, "skill-md");
            Activity.Current?.SetTag(ChatbotActivitySource.TagSkillName, GetType().Name);
            Activity.Current?.SetTag(ChatbotActivitySource.TagClosureName, ClosureName);
            Activity.Current?.SetTag(ChatbotActivitySource.TagExceptionType, ex.GetType().Name);
            Activity.Current?.SetTag(
                ChatbotActivitySource.TagToolFailureReason,
                ChatbotActivitySource.FailureReasons.SkillMdException);
            _logger.LogError(ex, "{Skill}: inner SkillMdDrivenSkill failed", GetType().Name);
            return new AgentResponse
            {
                AgentId    = ResponseAgentId,
                Result     = DegradedResponseText,
                Confidence = 0f,
                Evidence   = StaticEvidenceTags,
            };
        }
    }
}
