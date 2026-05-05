namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Skills;

/// <summary>
/// Smoke test for the 17 drafts in <c>skills-dev/</c> seeded by the
/// skill-stewards 2026-05-05 batch. Confirms every draft parses, carries
/// a Name + Description, and retains at least 3 surviving triggers
/// after <see cref="SkillMdParser.MinTriggerLength"/> filtering.
/// </summary>
/// <remarks>
/// This test will keep firing on the canonical skills as drafts graduate
/// — the same assertions apply to <c>skills/</c>. Designed as cheap
/// CI-time guard against frontmatter regressions in the corpus.
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

        Assert.Multiple(() =>
        {
            Assert.That(skills, Is.Not.Empty,
                "skills-dev/ should contain drafts after the skill-stewards batch landed");
            Assert.That(skills.All(s => !string.IsNullOrWhiteSpace(s.Name)), Is.True,
                "Every draft must have a non-empty Name");
            Assert.That(skills.All(s => !string.IsNullOrWhiteSpace(s.Description)), Is.True,
                "Every draft must have a non-empty Description (intent-router input)");
            Assert.That(skills.All(s => s.Triggers.Count >= 3), Is.True,
                "Every draft must have at least 3 surviving triggers — fewer would " +
                "shadow nothing and thus never dispatch");
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
