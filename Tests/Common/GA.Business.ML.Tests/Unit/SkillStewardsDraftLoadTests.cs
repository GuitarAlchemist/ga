namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Skills;

/// <summary>
/// Smoke test for active drafts in <c>skills-dev/</c>. Confirms every
/// active draft parses, carries a Name + Description, and retains at
/// least 3 surviving triggers after <see cref="SkillMdParser.MinTriggerLength"/>
/// filtering.
/// </summary>
/// <remarks>
/// <para>
/// Drafts under <c>skills-dev/_pending-tools/</c> are intentionally
/// excluded — they're stored as <c>DRAFT.md</c> (not
/// <c>SKILL.md</c>) so SkillMdLoader's glob doesn't see them. They
/// live as design specs for MCP tools that don't yet exist; loading
/// them as live skills would crash the orchestrator on first
/// dispatch. See <c>skills-dev/_pending-tools/README.md</c>.
/// </para>
/// <para>
/// This test will keep firing on the canonical skills as drafts
/// graduate — the same assertions apply to <c>skills/</c>. Designed
/// as cheap CI-time guard against frontmatter regressions in the
/// corpus.
/// </para>
/// </remarks>
[TestFixture]
public class SkillStewardsDraftLoadTests
{
    [Test]
    public void AllSkillsDevDrafts_ParseAndLoadCleanly()
    {
        var skillsDevDir = LocateSkillsDevDir();
        if (!Directory.Exists(skillsDevDir))
        {
            Assert.Ignore($"skills-dev/ not present at {skillsDevDir}; nothing to load.");
            return;
        }

        var warnings = new List<string>();
        var skills = SkillMdLoader.LoadFromDirectory(skillsDevDir, warnings.Add);

        TestContext.WriteLine($"Loaded {skills.Count} drafts from {skillsDevDir}");
        foreach (var skill in skills.OrderBy(s => s.Name, StringComparer.Ordinal))
        {
            TestContext.WriteLine($"  - {skill.Name}: {skill.Triggers.Count} triggers, " +
                                  $"description={skill.Description.Length} chars, " +
                                  $"body={skill.Body.Length} chars");
        }

        // skills-dev/ may legitimately be empty when all drafts have
        // graduated to skills/ (the desired terminal state of the
        // skill-stewards loop). The remaining assertions are
        // conditional on having drafts to validate.
        Assert.Multiple(() =>
        {
            if (skills.Count > 0)
            {
                Assert.That(skills.All(s => !string.IsNullOrWhiteSpace(s.Name)), Is.True,
                    "Every draft must have a non-empty Name");
                Assert.That(skills.All(s => !string.IsNullOrWhiteSpace(s.Description)), Is.True,
                    "Every draft must have a non-empty Description (intent-router input)");
                Assert.That(skills.All(s => s.Triggers.Count >= 3), Is.True,
                    "Every draft must have at least 3 surviving triggers — fewer would " +
                    "shadow nothing and thus never dispatch");
            }
            Assert.That(warnings, Is.Empty,
                $"SkillMdLoader emitted warnings: {string.Join(" | ", warnings)}");
        });
    }

    [Test]
    public void AllSkillsDevDrafts_HaveCanonicalCamelCaseFrontmatter()
    {
        var skillsDevDir = LocateSkillsDevDir();
        if (!Directory.Exists(skillsDevDir))
        {
            Assert.Ignore($"skills-dev/ not present at {skillsDevDir}");
            return;
        }

        var pascalCaseFiles = new List<string>();
        foreach (var file in Directory.EnumerateFiles(skillsDevDir, "SKILL.md", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            // Look for top-level PascalCase keys in the frontmatter block.
            // Simple heuristic: any line at indent 0 starting with uppercase letter then ":".
            var lines = content.Split('\n');
            var inFrontmatter = lines.Length > 0 && lines[0].TrimEnd() == "---";
            if (!inFrontmatter) continue;

            for (var i = 1; i < lines.Length; i++)
            {
                if (lines[i].TrimEnd() == "---") break;
                var line = lines[i];
                if (line.Length == 0 || char.IsWhiteSpace(line[0])) continue;
                if (line.Length > 0 && char.IsUpper(line[0]) && line.Contains(':'))
                {
                    pascalCaseFiles.Add($"{file}: '{line.TrimEnd()}'");
                    break;
                }
            }
        }

        Assert.That(pascalCaseFiles, Is.Empty,
            "Drafts must use camelCase frontmatter (Anthropic spec + GA canonical convention). " +
            $"Found PascalCase keys in:\n  {string.Join("\n  ", pascalCaseFiles)}");
    }

    /// <summary>
    /// Graduation gate: every `allowed-tools` entry across `skills/` and
    /// active `skills-dev/` drafts MUST resolve to a real
    /// [McpServerTool(Name=...)] registration in
    /// Common/GA.Business.ML/Agents/Mcp/*.cs. Without this gate, drafts
    /// referencing fictional tools (the failure mode that triggered this
    /// gate's creation, PR #118 review) can graduate and crash the
    /// orchestrator on first dispatch.
    /// </summary>
    [Test]
    public void AllowedToolsAcrossSkills_ResolveToRegisteredMcpTools()
    {
        var repoRoot = LocateRepoRoot();
        if (repoRoot is null)
        {
            Assert.Ignore("Repo root not found from test bin location.");
            return;
        }

        // Discover the registry by grepping McpServerTool(Name="...")
        // across the canonical Mcp directory. Single source of truth.
        var mcpDir = Path.Combine(repoRoot, "Common", "GA.Business.ML", "Agents", "Mcp");
        var registry = new HashSet<string>(StringComparer.Ordinal);
        if (Directory.Exists(mcpDir))
        {
            foreach (var cs in Directory.EnumerateFiles(mcpDir, "*.cs"))
            {
                foreach (var line in File.ReadLines(cs))
                {
                    var idx = line.IndexOf("McpServerTool(Name = \"", StringComparison.Ordinal);
                    if (idx < 0) continue;
                    var start = idx + "McpServerTool(Name = \"".Length;
                    var end = line.IndexOf('"', start);
                    if (end > start) registry.Add(line[start..end]);
                }
            }
        }
        TestContext.WriteLine($"Registered MCP tools ({registry.Count}): {string.Join(", ", registry.OrderBy(x => x))}");

        // Walk every SKILL.md (canonical + active drafts), parse
        // allowed-tools, assert each is registered.
        var dirsToScan = new[]
        {
            Path.Combine(repoRoot, "skills"),
            Path.Combine(repoRoot, "skills-dev"),
        };
        var orphans = new List<string>();
        foreach (var dir in dirsToScan.Where(Directory.Exists))
        {
            // SearchOption.AllDirectories is fine here — _pending-tools/
            // contents are DRAFT.md, not SKILL.md, so they're skipped.
            foreach (var skillFile in Directory.EnumerateFiles(dir, "SKILL.md", SearchOption.AllDirectories))
            {
                // Skip Claude-Code-only meta-skills whose allowed-tools
                // resolve via a different MCP server (Demerzel / QA CLI),
                // not the GA chatbot's MCP registry. Today this is just
                // qa-architect; if more such skills land, replace this
                // exception list with a frontmatter flag like
                // `metadata.chatbot_dispatch: false`.
                if (Path.GetFileName(Path.GetDirectoryName(skillFile)) == "qa-architect")
                    continue;

                var content = File.ReadAllText(skillFile);
                var allowedTools = ExtractAllowedTools(content);
                foreach (var tool in allowedTools)
                {
                    if (!registry.Contains(tool))
                        orphans.Add($"{Path.GetRelativePath(repoRoot, skillFile)}: '{tool}'");
                }
            }
        }

        Assert.That(orphans, Is.Empty,
            "Every allowed-tools entry must resolve to a [McpServerTool(Name=...)] " +
            "registration. If a draft needs a tool that doesn't exist, move it to " +
            "skills-dev/_pending-tools/<name>/DRAFT.md until the tool ships.\n" +
            $"Orphans:\n  {string.Join("\n  ", orphans)}");
    }

    private static IEnumerable<string> ExtractAllowedTools(string skillContent)
    {
        // Find `allowed-tools:` block in YAML frontmatter, return list items.
        var lines = skillContent.Split('\n');
        var inFrontmatter = lines.Length > 0 && lines[0].TrimEnd() == "---";
        if (!inFrontmatter) yield break;
        var inAllowedTools = false;
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i];
            if (line.TrimEnd() == "---") yield break;
            if (line.StartsWith("allowed-tools:"))
            {
                inAllowedTools = true;
                continue;
            }
            if (inAllowedTools)
            {
                if (line.Length > 0 && !char.IsWhiteSpace(line[0]))
                {
                    inAllowedTools = false;
                    continue;
                }
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("- "))
                {
                    var v = trimmed[2..].Trim().Trim('"');
                    if (v.Length > 0) yield return v;
                }
            }
        }
    }

    private static string? LocateRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(current, "AllProjects.slnx")))
                return current;
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || parent == current) break;
            current = parent;
        }
        return null;
    }

    /// <summary>
    /// Walks up from the test assembly to find the repo root, then locates
    /// <c>skills-dev/</c>. The test isn't an integration test against a
    /// live filesystem — it's reading committed fixture files in the repo.
    /// </summary>
    private static string LocateSkillsDevDir()
    {
        var current = AppContext.BaseDirectory;
        // Walk up looking for `AllProjects.slnx` (the repo root marker).
        for (var i = 0; i < 10; i++)
        {
            if (File.Exists(Path.Combine(current, "AllProjects.slnx")))
                return Path.Combine(current, "skills-dev");
            var parent = Directory.GetParent(current)?.FullName;
            if (parent is null || parent == current) break;
            current = parent;
        }
        // Fallback: best-effort path from test bin output.
        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "skills-dev");
    }
}
