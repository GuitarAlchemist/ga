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

        return Directory
            .EnumerateFiles(rootPath, "SKILL.md", SearchOption.AllDirectories)
            .Select(SkillMdParser.TryParse)
            .OfType<SkillMd>()
            .Where(s => s.Triggers.Count > 0)
            .ToList();
    }
}
