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

    // Two deserializers so authors can pick either casing convention:
    //  - PascalCase (`Name:`, `Description:`, `Triggers:`) — original GA style.
    //  - camelCase  (`name:`, `description:`, `triggers:`) — Anthropic Claude
    //    Code SKILL.md convention. A skill authored once in lowercase frontmatter
    //    is now valid in both ecosystems, enabling fast iteration: drop a
    //    Claude Code skill into `skills/` and GA picks it up; drop a GA skill
    //    into `~/.claude/plugins/.../skills/` and Claude Code picks it up.
    //
    // PascalCase is tried first (preserves prior behaviour for the 59 existing
    // SKILL.md files); camelCase is the fallback when PascalCase produces no
    // Name (i.e. the YAML used lowercase keys). Both deserializers
    // IgnoreUnmatchedProperties so each ecosystem's extra fields
    // (GA: `Triggers`/`license`/`compatibility`; Claude: `user-invocable`/
    // `allowed-tools`) coexist without parser errors.
    private static readonly IDeserializer PascalDeserializer = new DeserializerBuilder()
        .WithNamingConvention(PascalCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly IDeserializer CamelDeserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
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

        var frontmatter = TryDeserializeWithEitherConvention(yamlContent);
        if (frontmatter is null || string.IsNullOrWhiteSpace(frontmatter.Name))
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

    /// <summary>
    /// Tries to deserialise the frontmatter as PascalCase (legacy GA convention),
    /// falling back to camelCase (Claude Code convention). A frontmatter with no
    /// Name field after both attempts is treated as malformed by the caller.
    /// </summary>
    /// <remarks>
    /// Either convention is acceptable; mixing within a single file is not
    /// supported (the chosen deserializer wins, the other case's keys are
    /// silently dropped under <c>IgnoreUnmatchedProperties</c>). Authors who
    /// want a SKILL.md valid in both ecosystems should pick one convention and
    /// stick with it for the file — the recommended convention going forward
    /// is camelCase to match Anthropic's published spec.
    /// </remarks>
    private static SkillMdFrontmatter? TryDeserializeWithEitherConvention(string yamlContent)
    {
        foreach (var deserializer in new[] { PascalDeserializer, CamelDeserializer })
        {
            try
            {
                var result = deserializer.Deserialize<SkillMdFrontmatter>(yamlContent);
                if (result is not null && !string.IsNullOrWhiteSpace(result.Name))
                    return result;
            }
            catch
            {
                // Try next convention. If both fail we return null and the caller
                // surfaces a generic parse failure.
            }
        }
        return null;
    }

    // ── YAML DTO ─────────────────────────────────────────────────────────────

    private sealed class SkillMdFrontmatter
    {
        public string?       Name        { get; set; }
        public string?       Description { get; set; }
        public List<string>? Triggers    { get; set; }  // List<T> for YamlDotNet compatibility
    }
}
