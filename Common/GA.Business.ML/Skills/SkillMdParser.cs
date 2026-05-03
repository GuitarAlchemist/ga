namespace GA.Business.ML.Skills;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Parses <c>SKILL.md</c> files into <see cref="SkillMd"/> records.
/// </summary>
/// <remarks>
/// A SKILL.md file has an optional YAML frontmatter block delimited by <c>---</c> lines,
/// followed by markdown body content used as the Claude system prompt.
/// </remarks>
public static class SkillMdParser
{
    /// <summary>
    /// Maximum allowed SKILL.md body length in characters. Files with bodies
    /// past this limit are rejected so a malicious or accidental giant payload
    /// (e.g. a million-line prompt-injection attack, or a copy-pasted large
    /// document) cannot ride into every LLM call that triggers the skill.
    /// 32 KB is comfortably larger than any legitimate authored skill — the
    /// largest in the repo today (qa-architect) is under 5 KB.
    /// </summary>
    public const int MaxBodyCharacters = 32 * 1024;

    /// <summary>
    /// Minimum trigger keyword length. Triggers shorter than this are dropped
    /// silently — defends against over-broad triggers like <c>"a"</c> /
    /// <c>"an"</c> that would fire on every English message and shadow
    /// more-specific skills. 3 chars is the floor: it admits domain-meaningful
    /// 3-letter words like <c>"key"</c>, <c>"mode"</c>, <c>"tab"</c>,
    /// <c>"jam"</c> while still rejecting one- and two-letter catch-alls
    /// (which would essentially always match English prose).
    /// </summary>
    public const int MinTriggerLength = 3;

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Attempts to parse the SKILL.md file at <paramref name="filePath"/>.
    /// Returns <see langword="null"/> when the file has no frontmatter, no <c>Name</c> field,
    /// or cannot be read.
    /// </summary>
    public static SkillMd? TryParse(string filePath)
    {
        string content;
        try
        {
            content = File.ReadAllText(filePath);
        }
        catch
        {
            return null;
        }

        return TryParseContent(content, filePath);
    }

    /// <summary>
    /// Parses SKILL.md content from a string — used for unit testing without file I/O.
    /// </summary>
    public static SkillMd? TryParseContent(string content, string filePath = "<memory>")
    {
        // Trim leading newlines — raw string literals and some editors add them.
        content = content.TrimStart('\r', '\n');

        // Frontmatter: content must start with "---\n" and contain a closing "---"
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return null;

        var lines = content.Split('\n');
        var closingIndex = -1;
        for (var i = 1; i < lines.Length; i++)
        {
            if (lines[i].TrimEnd() == "---")
            {
                closingIndex = i;
                break;
            }
        }

        if (closingIndex < 0)
            return null;

        var yamlContent = string.Join('\n', lines[1..closingIndex]);
        var body        = string.Join('\n', lines[(closingIndex + 1)..]).TrimStart();

        SkillMdFrontmatter frontmatter;
        try
        {
            frontmatter = Deserializer.Deserialize<SkillMdFrontmatter>(yamlContent)
                          ?? new SkillMdFrontmatter();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(frontmatter.Name))
            return null;

        // Reject SKILL.md files whose bodies exceed MaxBodyCharacters — the
        // body becomes a system prompt for every triggered LLM call, so a
        // multi-MB payload is both a memory hog and a prompt-injection
        // amplifier.
        if (body.Length > MaxBodyCharacters)
            return null;

        // Drop triggers shorter than MinTriggerLength rather than rejecting
        // the whole skill — most violations are typos / leftover stubs, not
        // hostile. Skills with NO surviving triggers are filtered downstream
        // by SkillMdLoader.
        var sanitizedTriggers = frontmatter.Triggers?
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length >= MinTriggerLength)
            .Select(t => t.ToLowerInvariant())
            .ToList()
            ?? [];

        return new SkillMd
        {
            Name        = frontmatter.Name.Trim(),
            Description = frontmatter.Description?.Trim() ?? string.Empty,
            Triggers    = sanitizedTriggers,
            Body        = body,
            FilePath    = filePath,
        };
    }

    // ── YAML DTO ─────────────────────────────────────────────────────────────

    private sealed class SkillMdFrontmatter
    {
        public string?       Name        { get; set; }
        public string?       Description { get; set; }
        public List<string>? Triggers    { get; set; }  // List<T> for YamlDotNet compatibility
    }
}
