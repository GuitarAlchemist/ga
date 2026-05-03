namespace GA.Business.ML.Skills;

/// <summary>
/// Scans a root directory tree for SKILL.md files and returns all parsed <see cref="SkillMd"/>
/// instances that have at least one trigger keyword (and are therefore eligible as chatbot skills).
/// Files without frontmatter or without <c>triggers</c> are silently skipped.
/// </summary>
public static class SkillMdLoader
{
    public static IReadOnlyList<SkillMd> LoadFromDirectory(string rootPath) =>
        LoadFromDirectory(rootPath, _ => { });

    /// <summary>
    /// Variant that emits author-facing diagnostics via <paramref name="logWarning"/>.
    /// Use this overload from production wiring (FileBasedSkillsProvider, SkillMdPlugin)
    /// so a SKILL.md file whose triggers all got filtered surfaces a clear message
    /// instead of silently disappearing from registration.
    /// </summary>
    public static IReadOnlyList<SkillMd> LoadFromDirectory(string rootPath, Action<string> logWarning)
    {
        if (!Directory.Exists(rootPath))
            return [];

        // WHY OrderBy: Directory.EnumerateFiles is OS-dependent — Windows returns
        // alphabetic order, Linux (CI) returns filesystem (inode) order. Tests
        // and downstream code expect deterministic load order so the chatbot
        // sees a stable skill context. Ordinal comparer keeps it culture-free.
        var parsed = Directory
            .EnumerateFiles(rootPath, "SKILL.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(SkillMdParser.TryParse)
            .OfType<SkillMd>()
            .ToList();

        // Author feedback: a skill that parsed but has zero surviving triggers is
        // almost certainly a typo'd or stub trigger list — the parser silently
        // dropped all of them under MinTriggerLength. Loud at the loader so the
        // author sees it, quiet at the parser so the file-format contract stays
        // pure (per PR #79 review recommendation).
        foreach (var skill in parsed.Where(s => s.Triggers.Count == 0))
        {
            logWarning(
                $"[SkillMdLoader] '{skill.FilePath}' parsed but has no surviving triggers — " +
                $"the skill will NOT be registered. Check that each trigger is at least " +
                $"{SkillMdParser.MinTriggerLength} characters long.");
        }

        return parsed.Where(s => s.Triggers.Count > 0).ToList();
    }
}
