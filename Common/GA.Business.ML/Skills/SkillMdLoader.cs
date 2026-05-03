namespace GA.Business.ML.Skills;

/// <summary>
/// Scans a root directory tree for SKILL.md files and returns all parsed <see cref="SkillMd"/>
/// instances that have at least one trigger keyword (and are therefore eligible as chatbot skills).
/// Files without frontmatter or without <c>triggers</c> are silently skipped.
/// </summary>
public static class SkillMdLoader
{
    public static IReadOnlyList<SkillMd> LoadFromDirectory(string rootPath)
    {
        if (!Directory.Exists(rootPath))
            return [];

        // WHY OrderBy: Directory.EnumerateFiles is OS-dependent — Windows returns
        // alphabetic order, Linux (CI) returns filesystem (inode) order. Tests
        // and downstream code expect deterministic load order so the chatbot
        // sees a stable skill context. Ordinal comparer keeps it culture-free.
        return Directory
            .EnumerateFiles(rootPath, "SKILL.md", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(SkillMdParser.TryParse)
            .OfType<SkillMd>()
            .Where(s => s.Triggers.Count > 0)
            .ToList();
    }
}
