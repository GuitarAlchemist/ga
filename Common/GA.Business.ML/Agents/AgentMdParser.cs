namespace GA.Business.ML.Agents;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Parses declarative agent <c>.md</c> files into <see cref="AgentMd"/> records.
/// Follows the same YAML frontmatter + markdown body pattern as <see cref="Skills.SkillMdParser"/>,
/// with extended fields for agent-specific metadata (capabilities, routing keywords, delegation).
/// </summary>
public static class AgentMdParser
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Attempts to parse an agent <c>.md</c> file at <paramref name="filePath"/>.
    /// Returns <see langword="null"/> when the file has no frontmatter, no <c>id</c> field,
    /// or cannot be read.
    /// </summary>
    public static AgentMd? TryParse(string filePath)
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
    /// Parses agent .md content from a string — used for unit testing without file I/O.
    /// </summary>
    public static AgentMd? TryParseContent(string content, string filePath = "<memory>")
    {
        content = content.TrimStart('\r', '\n');

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
        var body = string.Join('\n', lines[(closingIndex + 1)..]).TrimStart();

        AgentMdFrontmatter frontmatter;
        try
        {
            frontmatter = Deserializer.Deserialize<AgentMdFrontmatter>(yamlContent)
                          ?? new AgentMdFrontmatter();
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(frontmatter.Id))
            return null;

        return new AgentMd
        {
            Id = frontmatter.Id.Trim(),
            Name = frontmatter.Name?.Trim() ?? frontmatter.Id.Trim(),
            Role = frontmatter.Role?.Trim() ?? frontmatter.Id.Trim(),
            Description = frontmatter.Description?.Trim() ?? string.Empty,
            Capabilities = frontmatter.Capabilities ?? [],
            RoutingKeywords = frontmatter.RoutingKeywords?
                .Select(k => k.ToLowerInvariant()).ToList() ?? [],
            UseCritique = frontmatter.UseCritique,
            DelegatesTo = frontmatter.DelegatesTo?.Trim(),
            Body = body,
            FilePath = filePath,
        };
    }

    /// <summary>
    /// Discovers and parses all agent <c>.md</c> files in a directory.
    /// </summary>
    public static IReadOnlyList<AgentMd> DiscoverAll(string directory)
    {
        if (!Directory.Exists(directory))
            return [];

        return Directory.GetFiles(directory, "*.md")
            .Select(TryParse)
            .Where(a => a is not null)
            // Exclude non-agent .md files (those without the agent frontmatter fields)
            .Where(a => !string.IsNullOrEmpty(a!.Role))
            .ToList()!;
    }

    // ── YAML DTO ─────────────────────────────────────────────────────────────

    private sealed class AgentMdFrontmatter
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Role { get; set; }
        public string? Description { get; set; }
        public List<string>? Capabilities { get; set; }
        public List<string>? Routing_keywords { get; set; }
        public bool Use_critique { get; set; }
        public string? Delegates_to { get; set; }

        // Convenience properties for underscore naming convention
        public List<string>? RoutingKeywords => Routing_keywords;
        public bool UseCritique => Use_critique;
        public string? DelegatesTo => Delegates_to;
    }
}
