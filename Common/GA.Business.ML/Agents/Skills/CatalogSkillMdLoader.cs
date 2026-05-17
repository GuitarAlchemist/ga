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
    /// Catalog SKILL.md files were authored with TWO categories of model-
    /// facing meta-content that should not reach end users:
    /// <list type="number">
    /// <item><b>Preamble directive lines</b> — a paragraph between the H1
    ///   and the first H2 that tells an LLM how to use the body
    ///   ("Reproduce the catalog below verbatim…", "Match their goal to
    ///   one of the templates below…", "Pure pedagogy — doesn't need a
    ///   tool call", "Use when a learner asks…"). Stripped per-line via
    ///   <see cref="DirectiveMarkers"/> while in the preamble zone.</item>
    /// <item><b>Meta-sections</b> — entire H2 sections whose heading
    ///   matches <see cref="MetaSectionHeadings"/> (e.g.
    ///   <c>## How to dispatch</c>, <c>## Routing</c>,
    ///   <c>## Authoring notes</c>). These contain dispatch logic for the
    ///   LLM/router, not user-visible answer content. Stripped from the
    ///   heading through (but not including) the next H2.</item>
    /// </list>
    /// For hybrid skills that pass the body to an LLM as context, that
    /// meta-content is helpful priming. For PURE catalog skills — which
    /// this loader serves — there IS NO LLM CALL. The body is returned
    /// verbatim to the end user, so any meta-content leaks as
    /// "looks like a fake / skill-prompt leak" UX. Live found 2026-05-12
    /// (preamble), again 2026-05-14 (meta-section).
    /// </summary>
    internal static string StripModelDirectivePreamble(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return body;

        var lines  = body.Replace("\r\n", "\n").Split('\n');
        var result = new List<string>(lines.Length);

        // State machine:
        //   inPreambleZone  — we're between H1 and the first H2; directive
        //                     LINES get dropped here (single-paragraph leaks).
        //   inMetaSection   — we're inside an H2 whose heading is on the
        //                     meta-section whitelist; ALL lines are dropped
        //                     until the next H2 (or end of file).
        var inPreambleZone = false;
        var preambleDone   = false;
        var inMetaSection  = false;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            // H1 — title; starts the preamble zone.
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                inPreambleZone = true;
                preambleDone   = false;
                inMetaSection  = false;
                result.Add(line);
                continue;
            }

            // H2 — ends preamble zone, and decides whether the new section
            // is meta (drop) or content (keep).
            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                preambleDone   = true;
                inPreambleZone = false;
                inMetaSection  = IsMetaSectionHeading(trimmed);
                if (!inMetaSection)
                {
                    result.Add(line);
                }
                continue;
            }

            // Deeper headings (### …) are content-level; if we're inside a
            // meta section, they belong to it and stay dropped. Otherwise
            // they pass through.
            if (inMetaSection)
            {
                continue;
            }

            if (!preambleDone && inPreambleZone && IsDirectiveLine(line))
            {
                continue;
            }

            result.Add(line);
        }

        // Collapse runs of 3+ consecutive blank lines (left over from
        // stripped sections) down to a single blank line.
        var joined = string.Join("\n", result).TrimEnd('\n');
        joined = System.Text.RegularExpressions.Regex.Replace(
            joined,
            @"\n{3,}",
            "\n\n");
        return joined;
    }

    /// <summary>
    /// H2 headings whose sections should be stripped entirely (heading +
    /// body until the next H2). All comparisons are case-insensitive and
    /// match the heading text after the <c>##</c> marker.
    /// </summary>
    /// <remarks>
    /// Bare "routing" was deliberately omitted on 2026-05-14 (code-review
    /// finding) — it collides with legitimate music vocabulary like
    /// "voice routing", "signal routing", or "routing the 7th to the 3rd"
    /// in voice-leading content. Use the more-specific "routing notes" or
    /// "routing rules" when authoring meta-sections about routing logic.
    /// </remarks>
    private static readonly string[] MetaSectionHeadings =
    [
        "how to dispatch",
        "dispatch",
        "dispatch notes",
        "routing notes",
        "routing rules",
        "when to use",
        "authoring notes",
        "implementation notes",
    ];

    private static bool IsMetaSectionHeading(string trimmedLine)
    {
        // trimmedLine starts with "## ". Strip the marker and any trailing
        // anchors/whitespace, then case-insensitive compare against the
        // whitelist.
        var headingText = trimmedLine[3..].Trim().ToLowerInvariant();
        // Strip trailing markdown anchors like " {#id}" if any.
        var spaceIdx = headingText.IndexOf(" {", StringComparison.Ordinal);
        if (spaceIdx > 0) headingText = headingText[..spaceIdx].TrimEnd();
        foreach (var meta in MetaSectionHeadings)
        {
            if (headingText == meta) return true;
        }
        return false;
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
