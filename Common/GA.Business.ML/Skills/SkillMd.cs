namespace GA.Business.ML.Skills;

/// <summary>
/// Parsed representation of a <c>SKILL.md</c> file — frontmatter metadata plus the
/// markdown body that serves as the system prompt for <c>SkillMdDrivenSkill</c>.
/// </summary>
public sealed record SkillMd
{
    /// <summary>Human-readable skill name (from <c>Name:</c> frontmatter field).</summary>
    public required string Name { get; init; }

    /// <summary>One-line capability description (from <c>Description:</c> frontmatter field).</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// Keyword patterns used by <c>CanHandle()</c> for trigger matching.
    /// Skills with an empty list are valid Claude Code guides but are NOT auto-loaded as chatbot skills.
    /// </summary>
    public IReadOnlyList<string> Triggers { get; init; } = [];

    /// <summary>
    /// Tool names this skill is permitted to use (from the <c>allowed-tools:</c>
    /// frontmatter field). For GA's deterministic skills this is the closure/MCP
    /// tool the skill MUST call (e.g. <c>[ga_dsl_eval]</c>), so <c>SkillMdDrivenSkill</c>
    /// scopes the visible tool set to these and forces invocation — preventing a
    /// weak model from narrating a plausible answer from training instead of
    /// running the deterministic engine. Empty for conversational skills (which
    /// keep the full tool set and a free <c>Auto</c> tool choice).
    /// </summary>
    public IReadOnlyList<string> AllowedTools { get; init; } = [];

    /// <summary>
    /// The markdown body of the SKILL.md file — injected verbatim as the Claude system prompt.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>Absolute path of the source file, for logging and diagnostics.</summary>
    public required string FilePath { get; init; }
}
