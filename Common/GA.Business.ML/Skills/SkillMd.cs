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
    /// The markdown body of the SKILL.md file — injected verbatim as the Claude system prompt.
    /// </summary>
    public required string Body { get; init; }

    /// <summary>Absolute path of the source file, for logging and diagnostics.</summary>
    public required string FilePath { get; init; }
}
