namespace GA.Business.ML.Agents.Skills;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Skills;

/// <summary>
/// Reads a catalog SKILL.md body by folder name so C# wrapper classes can keep
/// the SKILL.md as the single source of truth for catalog content while still
/// supplying the routing metadata (Name, Description, ExamplePrompts) that
/// <c>SemanticIntentRouter</c> requires.
/// </summary>
/// <remarks>
/// The architectural rule for catalog skills is: SKILL.md = body + routing
/// triggers; C# class = routing metadata + answer emission. Without a C#
/// class the skill is invisible to the orchestrator's intent registry — see
/// the smoke-test failure log "embedded 11 new intent(s)" that surfaced this
/// gap when the four catalog skills (circle-of-fifths, practice-routine,
/// genre-essentials, what-can-you-do) were graduated SKILL.md-only on
/// 2026-05-05.
/// </remarks>
internal static class CatalogSkillMdLoader
{
    public static string LoadBodyOrFallback(string skillFolderName, string fallback)
    {
        try
        {
            var skillsDir = SkillMdPlugin.ResolveSkillsPath();
            var path = Path.Combine(skillsDir, skillFolderName, "SKILL.md");
            var skillMd = SkillMdParser.TryParse(path);
            return skillMd?.Body ?? fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
