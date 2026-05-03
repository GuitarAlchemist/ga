namespace GA.Business.ML.Agents.AgentFramework;

using System.Text;
using GA.Business.ML.Skills;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

/// <summary>
/// Microsoft Agent Framework <see cref="AIContextProvider"/> backed by a
/// directory of <c>SKILL.md</c> files. Forward-compatible substitute for the
/// upcoming <c>Microsoft.Agents.AI.AgentSkillsProvider</c> (referenced in
/// <c>docs/plans/2026-05-03-chatbot-agent-framework-migration-recommendation.md</c>
/// §"C# equivalent of Spring AI generic skills" but not yet shipped in the
/// 1.0.0-preview package GA references).
/// </summary>
/// <remarks>
/// <para><b>Behavior contract:</b></para>
/// <list type="bullet">
///   <item>On construction, scans the supplied directory for <c>SKILL.md</c>
///         files and loads them via <see cref="SkillMdLoader"/>. Files without
///         <c>triggers</c> frontmatter are silently skipped.</item>
///   <item>On each <see cref="InvokingAsync"/> call, inspects the most recent
///         user message: if any loaded skill's trigger keywords match, returns
///         an <see cref="AIContext"/> with the matching skill's body as
///         additional <c>Instructions</c>. Multiple matches are concatenated in
///         load order so the agent sees a deterministic context.</item>
///   <item>Returns <see cref="AIContext.Empty"/> when no triggers match — the
///         agent then proceeds without any skill-supplied context, exactly as
///         it would have without this provider.</item>
///   <item>Read-only: this provider never executes scripts, never writes to
///         disk, and never invokes external services.</item>
/// </list>
/// <para>Forward-compatibility note: when Microsoft ships
/// <c>Microsoft.Agents.AI.AgentSkillsProvider</c>, swap call sites to it and
/// retire this type. The skill directory layout is the same, so SKILL.md files
/// migrate without change. See Phase 2 of the migration recommendation.</para>
/// </remarks>
public sealed class FileBasedSkillsProvider : AIContextProvider
{
    private readonly IReadOnlyList<SkillMd> _skills;

    /// <summary>
    /// Construct a provider over <paramref name="skillsDirectory"/>. Missing
    /// directory yields an empty provider (agent runs without skill context).
    /// </summary>
    public FileBasedSkillsProvider(string skillsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(skillsDirectory);

        _skills = Directory.Exists(skillsDirectory)
            ? SkillMdLoader.LoadFromDirectory(skillsDirectory)
            : [];
    }

    /// <summary>Loaded skills, in directory enumeration order.</summary>
    public IReadOnlyList<SkillMd> Skills => _skills;

    /// <summary>
    /// Provide additional <see cref="AIContext"/> for the current invocation.
    /// The framework wraps this in <c>InvokingCoreAsync</c> which handles
    /// filtering (External-only messages by default), merging with input
    /// context, and source stamping — overriding this method is the
    /// recommended path per the Agent Framework docs.
    /// </summary>
    protected override ValueTask<AIContext?> ProvideAIContextAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (_skills.Count == 0)
            return ValueTask.FromResult<AIContext?>(new AIContext());

        // Inspect the latest user-authored message in the input context —
        // that's what the agent is about to respond to. Earlier messages
        // would cause stale skill matches in long sessions.
        var latestUserText = context.AIContext.Messages?
            .LastOrDefault(m => m.Role == ChatRole.User)?
            .Text;

        if (string.IsNullOrWhiteSpace(latestUserText))
            return ValueTask.FromResult<AIContext?>(new AIContext());

        var lower = latestUserText.ToLowerInvariant();
        var matches = _skills
            .Where(s => s.Triggers.Any(t => !string.IsNullOrWhiteSpace(t) &&
                                            lower.Contains(t.ToLowerInvariant())))
            .ToList();

        if (matches.Count == 0)
            return ValueTask.FromResult<AIContext?>(new AIContext());

        var instructions = new StringBuilder();
        for (var i = 0; i < matches.Count; i++)
        {
            if (i > 0) instructions.AppendLine().AppendLine();
            instructions.AppendLine($"### Skill: {matches[i].Name}");
            instructions.Append(matches[i].Body);
        }

        return ValueTask.FromResult<AIContext?>(new AIContext { Instructions = instructions.ToString() });
    }
}
