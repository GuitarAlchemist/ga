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
    /// Deserialises the frontmatter under BOTH naming conventions and
    /// merges the results, preferring PascalCase per field for back-compat.
    /// Without this dual-pass strategy, a file that mixes casings (e.g.
    /// <c>Name:</c> in PascalCase but <c>description:</c>/<c>triggers:</c>
    /// in camelCase — a realistic migration mistake) would parse under
    /// Pascal only, then silently DROP the lowercase fields as unmatched
    /// properties. The skill would ship with no triggers, get filtered as
    /// untriggered, and disappear with no warning.
    /// </summary>
    /// <remarks>
    /// Per-field merge rules (Pascal wins on every field; Camel fills the gaps):
    /// <list type="bullet">
    ///   <item><c>Name</c> — PascalCase value if present, otherwise camelCase.</item>
    ///   <item><c>Description</c> — PascalCase value if present, otherwise camelCase.</item>
    ///   <item><c>Triggers</c> — PascalCase list if non-empty, otherwise camelCase list.</item>
    /// </list>
    /// Effect: pure-PascalCase and pure-camelCase files round-trip identically;
    /// mixed-casing files keep all data the author wrote regardless of which
    /// convention each field used. Pinned by
    /// <c>TryParseContent_MixedCasing_*</c> tests.
    /// </remarks>
    private static SkillMdFrontmatter? TryDeserializeWithEitherConvention(string yamlContent)
    {
        var pascal = TrySafe(PascalDeserializer, yamlContent);
        var camel  = TrySafe(CamelDeserializer,  yamlContent);

        if (pascal is null && camel is null) return null;

        var name = FirstNonEmpty(pascal?.Name, camel?.Name);
        if (string.IsNullOrWhiteSpace(name))
            return null;

        return new SkillMdFrontmatter
        {
            Name        = name,
            Description = FirstNonEmpty(pascal?.Description, camel?.Description),
            Triggers    = FirstNonEmptyList(pascal?.Triggers, camel?.Triggers),
        };
    }

    private static SkillMdFrontmatter? TrySafe(IDeserializer d, string yaml)
    {
        try { return d.Deserialize<SkillMdFrontmatter>(yaml); }
        catch { return null; }
    }

    private static string? FirstNonEmpty(string? a, string? b) =>
        !string.IsNullOrWhiteSpace(a) ? a : b;

    private static List<string>? FirstNonEmptyList(List<string>? a, List<string>? b) =>
        a is { Count: > 0 } ? a : b;

    // ── YAML DTO ─────────────────────────────────────────────────────────────

    private sealed class SkillMdFrontmatter
    {
        public string?       Name        { get; set; }
        public string?       Description { get; set; }
        public List<string>? Triggers    { get; set; }  // List<T> for YamlDotNet compatibility
    }
}
