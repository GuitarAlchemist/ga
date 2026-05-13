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
            return skillMd?.Body is { } body
                ? StripModelDirectivePreamble(body)
                : fallback;
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    /// <summary>
    /// Catalog SKILL.md files were authored with a leading "model-directive
    /// preamble" — a paragraph between the H1 and the first H2 that tells an
    /// LLM how to use the body ("Reproduce the catalog below verbatim when a
    /// user asks…", "Match their goal to one of the templates below and
    /// reproduce verbatim…", "Pure pedagogy — doesn't need a tool call",
    /// "Use when a learner asks…"). For hybrid skills that pass the body
    /// to an LLM as context, that preamble is helpful priming. For PURE
    /// catalog skills — which this loader serves — there IS NO LLM CALL.
    /// The body is returned verbatim to the end user, so the preamble leaks
    /// as "looks like a fake / skill-prompt leak" UX (live found 2026-05-12).
    /// We strip preamble paragraphs that contain known directive markers,
    /// leaving the H1 heading and resuming at the first non-directive line
    /// (typically the first H2).
    /// </summary>
    internal static string StripModelDirectivePreamble(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;

        var lines  = body.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>(lines.Length);

        // State: have we passed the H1 and are scanning the preamble zone?
        var inPreambleZone = false;
        var preambleDone   = false;

        foreach (var line in lines)
        {
            if (preambleDone)
            {
                result.Add(line);
                continue;
            }

            // Heading levels: an H2 (## …) ends the preamble zone. An H1 (# …)
            // is the title and starts the zone.
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("## ") || trimmed.StartsWith("### "))
            {
                preambleDone = true;
                result.Add(line);
                continue;
            }

            if (trimmed.StartsWith("# "))
            {
                inPreambleZone = true;
                result.Add(line);
                continue;
            }

            if (inPreambleZone && IsDirectiveLine(line))
            {
                // Drop this line — and collapse the following blank line if
                // present so we don't leave a double-gap after the H1.
                continue;
            }

            result.Add(line);
        }

        return string.Join("\n", result).TrimEnd('\n');
    }

    private static readonly string[] DirectiveMarkers =
    [
        "reproduce the catalog below",
        "reproduce verbatim",
        "pure pedagogy",
        "doesn't need a tool call",
        "use when a user asks",
        "use when a learner asks",
        "use when a visitor asks",
        "catalog skill — when",
        "match their goal to one of the templates",
    ];

    private static bool IsDirectiveLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        var lower = line.ToLowerInvariant();
        foreach (var marker in DirectiveMarkers)
            if (lower.Contains(marker)) return true;
        return false;
    }
}
