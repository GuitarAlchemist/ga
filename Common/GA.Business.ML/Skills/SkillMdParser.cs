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

        return new SkillMd
        {
            Name        = frontmatter.Name.Trim(),
            Description = frontmatter.Description?.Trim() ?? string.Empty,
            // Pre-lowercased so CanHandle can compare without per-call allocation
            Triggers    = frontmatter.Triggers?.Select(t => t.ToLowerInvariant()).ToList() ?? [],
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
