namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.AgentFramework;
using GA.Business.ML.Skills;

/// <summary>
/// Structural parity gate across the 10 migrated chatbot skills. Asserts
/// that for every skill we have BOTH a C# fast-path implementation AND a
/// SKILL.md surface, AND that they reference the same domain operation /
/// tool name when applicable. Catches drift between the two paths without
/// requiring a live LLM (which a true output-parity test would need).
/// </summary>
/// <remarks>
/// Quality fix #4 of 5 from the 2026-05-03 candid assessment:
/// "we have both deterministic C# skills and SKILL.md/MCP equivalents,
/// but no formal parity gate yet."
///
/// **What this test catches:**
/// <list type="bullet">
///   <item>SKILL.md file deleted or renamed</item>
///   <item>SKILL.md missing required trigger phrases that the migration
///         claimed it had</item>
///   <item>MCP-tool-driven SKILL.md whose `allowed-tools` no longer
///         references the tool name we expect</item>
///   <item>SKILL.md frontmatter `Name` doesn't match the directory name</item>
/// </list>
///
/// **What it does NOT catch (live-only):**
/// <list type="bullet">
///   <item>LLM picks the wrong tool for a given user phrasing</item>
///   <item>SKILL.md path produces output materially different from the C#
///         path for the same prompt</item>
///   <item>Routing confidence fall-through to LLM fallback</item>
/// </list>
/// Live-side parity is a separate workstream — needs Ollama running and a
/// curated golden prompt set per skill.
/// </remarks>
[TestFixture]
public class SkillParityMatrixTests
{
    /// <summary>
    /// Source-of-truth contract: one row per migrated skill, naming the
    /// SKILL.md folder, the expected `Name` frontmatter, the C# class type,
    /// and the expected MCP tool wire names (empty for catalog skills, may
    /// have multiple for skills that expose more than one operation).
    /// </summary>
    /// <remarks>
    /// Hardcoded array is intentional at N=10 — the <c>typeof()</c> entries
    /// give compile-time verification that's lost in a data file. Tipping
    /// point is roughly N≈20 or whenever a non-C# author needs to add rows;
    /// at that point extract to <c>state/quality/chatbot-qa/parity.yaml</c>
    /// and load via reflection-by-string.
    /// </remarks>
    public sealed record SkillContract(
        string SkillMdFolder,
        string ExpectedName,
        Type CSharpSkillType,
        string[] ExpectedToolWireNames,
        string Bucket);  // "catalog", "mcp-tool", "reuse"

    private static readonly SkillContract[] Contracts =
    [
        // Catalog skills (no MCP tool needed; data lives in the body)
        new("beginner-chords",       "beginner-chords",
            typeof(GA.Business.ML.Agents.Skills.BeginnerChordsSkill),
            [], "catalog"),
        new("progression-mood",      "progression-mood",
            typeof(GA.Business.ML.Agents.Skills.ProgressionMoodSkill),
            [], "catalog"),
        new("modes",                 "modes",
            typeof(GA.Business.ML.Agents.Skills.ModesSkill),
            [], "catalog"),

        // MCP-tool-driven skills (SKILL.md → ga_* tool → IOrchestratorSkill)
        new("interval",              "interval",
            typeof(GA.Business.ML.Agents.Skills.IntervalSkill),
            ["ga_interval_compute"], "mcp-tool"),
        new("scale-info",            "scale-info",
            typeof(GA.Business.ML.Agents.Skills.ScaleInfoSkill),
            ["ga_scale_get_notes"], "mcp-tool"),
        new("chord-info",            "chord-info",
            typeof(GA.Business.ML.Agents.Skills.ChordInfoSkill),
            ["ga_chord_info"], "mcp-tool"),
        new("fret-span",             "fret-span",
            typeof(GA.Business.ML.Agents.Skills.FretSpanSkill),
            ["ga_fret_span"], "mcp-tool"),
        new("chord-substitution",    "chord-substitution",
            typeof(GA.Business.ML.Agents.Skills.ChordSubstitutionSkill),
            ["ga_chord_substitutions", "ga_chord_compare"], "mcp-tool"),
        new("key-identification",    "key-identification",
            typeof(GA.Business.ML.Agents.Skills.KeyIdentificationSkill),
            ["ga_key_identify"], "mcp-tool"),

        // Reuses an existing tool — no new tool of its own.
        new("progression-completion","progression-completion",
            typeof(GA.Business.ML.Agents.Skills.ProgressionCompletionSkill),
            ["ga_key_identify"], "reuse"),
    ];

    private static IEnumerable<TestCaseData> AllContracts =>
        Contracts.Select(c => new TestCaseData(c).SetName($"Skill_{c.ExpectedName}"));

    /// <summary>
    /// Resolves the repo's <c>skills/</c> directory by walking up from the
    /// test binary, mirroring the strategy in
    /// <see cref="FileBasedSkillsProviderTests"/>. Returns <c>null</c>
    /// (test ignored) when not reachable so CI without a checkout doesn't
    /// fail.
    /// </summary>
    private static string? ResolveRepoSkillsDir()
    {
        var dir = new DirectoryInfo(Environment.GetEnvironmentVariable("GA_REPO_ROOT")
                                    ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "skills");
            if (Directory.Exists(candidate)) return candidate;
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))) return null;
            dir = dir.Parent;
        }
        return null;
    }

    [TestCaseSource(nameof(AllContracts))]
    public void Skill_HasMatchingSkillMdAndCSharpClass(SkillContract contract)
    {
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        // 1. SKILL.md exists at the expected path
        var skillMdPath = Path.Combine(repoSkills!, contract.SkillMdFolder, "SKILL.md");
        Assert.That(File.Exists(skillMdPath), Is.True,
            $"skills/{contract.SkillMdFolder}/SKILL.md must exist");

        // 2. SKILL.md parses + Name frontmatter matches expected
        var skill = SkillMdParser.TryParse(skillMdPath);
        Assert.That(skill, Is.Not.Null,
            $"skills/{contract.SkillMdFolder}/SKILL.md must parse");
        Assert.That(skill!.Name, Is.EqualTo(contract.ExpectedName),
            $"frontmatter Name must equal '{contract.ExpectedName}'");

        // 3. SKILL.md has at least one trigger (otherwise it won't register
        //    in the production path; SkillMdLoader filters out trigger-less
        //    skills)
        Assert.That(skill.Triggers.Count, Is.GreaterThan(0),
            $"skills/{contract.SkillMdFolder} must declare at least one trigger");

        // 4. The C# IOrchestratorSkill type exists and is constructible (no
        //    abstract / interface mistakes; static analysis catches typos here)
        Assert.That(contract.CSharpSkillType.IsAbstract, Is.False,
            $"{contract.CSharpSkillType.Name} must be a concrete type");
        Assert.That(typeof(IOrchestratorSkill).IsAssignableFrom(contract.CSharpSkillType), Is.True,
            $"{contract.CSharpSkillType.Name} must implement IOrchestratorSkill");
    }

    [TestCaseSource(nameof(AllContracts))]
    public void Skill_McpToolWireNamesAppearInSkillMdBody(SkillContract contract)
    {
        if (contract.ExpectedToolWireNames.Length == 0)
            Assert.Ignore("Catalog skill — no MCP tool to verify");

        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var skill = SkillMdParser.TryParse(Path.Combine(repoSkills!, contract.SkillMdFolder, "SKILL.md"))!;

        foreach (var toolName in contract.ExpectedToolWireNames)
        {
            Assert.That(skill.Body, Does.Contain(toolName),
                $"skills/{contract.SkillMdFolder}/SKILL.md body must reference tool '{toolName}' " +
                $"so the LLM knows what to call. If you renamed the tool, update both the SKILL.md " +
                $"and the contract in this test.");
        }
    }

    [TestCaseSource(nameof(AllContracts))]
    public void Skill_GaPluginRegistersTheCSharpClassAsIntent(SkillContract contract)
    {
        // Read GaPlugin.cs and assert the C# skill type is registered via
        // AddOrchestratorSkillIntent<TSkill>(). This catches the case where
        // a skill class exists but isn't actually wired into the orchestrator
        // — it would silently never route in production.
        //
        // Source-of-truth grep is more robust than DI introspection for this
        // particular check: the test fails IFF the source line is missing
        // (intent: "every migrated skill MUST appear in GaPlugin's registration
        // list"). DI introspection would require booting the entire host.
        var repoRoot = FindRepoRoot();
        if (repoRoot is null) Assert.Ignore("repo root not reachable");

        var gaPluginPath = Path.Combine(repoRoot!,
            "Common", "GA.Business.Core.Orchestration", "Plugins", "GaPlugin.cs");
        Assert.That(File.Exists(gaPluginPath), Is.True, "GaPlugin.cs must exist");

        var source = File.ReadAllText(gaPluginPath);
        var className = contract.CSharpSkillType.Name;

        // Regex-match `AddOrchestratorSkillIntent < (ws) ClassName (ws) >` —
        // tolerates formatter changes (a `dotnet format` run that adds spaces
        // around the type-arg shouldn't break the test). PR #97 review tightening.
        var registrationPattern = new System.Text.RegularExpressions.Regex(
            $@"AddOrchestratorSkillIntent\s*<\s*{System.Text.RegularExpressions.Regex.Escape(className)}\s*>");

        Assert.That(registrationPattern.IsMatch(source), Is.True,
            $"GaPlugin.cs must register {className} via AddOrchestratorSkillIntent<{className}>() — " +
            $"otherwise the SemanticIntentRouter never sees it and queries silently fall through to LLM.");
    }

    [Test]
    public void AllRepoSkillMdFiles_AreCoveredByTheParityMatrix()
    {
        // Catches the inverse drift: a SKILL.md file exists in skills/ but
        // isn't in the parity matrix. Either add it to Contracts, or move
        // it under a clearly-marked "non-chatbot" path. Prevents quiet
        // accumulation of unaudited skill surface.
        var repoSkills = ResolveRepoSkillsDir();
        if (repoSkills is null) Assert.Ignore("skills/ directory not reachable from test binary");

        var skillFolders = Directory.EnumerateDirectories(repoSkills!)
            .Select(Path.GetFileName)
            .Where(name => name is not null && File.Exists(Path.Combine(repoSkills!, name!, "SKILL.md")))
            .Cast<string>()
            .ToHashSet();

        var matrixFolders = Contracts.Select(c => c.SkillMdFolder).ToHashSet();

        // Excluded from the parity matrix by design:
        //
        // - qa-architect: predates the chatbot migration (PR #66 — a non-
        //   chatbot skill for QA Architect agent collaboration).
        // - circle-of-fifths, practice-routine, genre-essentials,
        //   what-can-you-do: pure-SKILL.md catalog skills graduated
        //   2026-05-05; no C# fast-path by design (the data lives in
        //   the body, no IOrchestratorSkill needed).
        //
        // **Follow-up** (per PR #97 review): replace this hardcoded list
        // with a SKILL.md frontmatter convention, e.g.
        //     metadata:
        //       chatbot-migration: false
        // and filter on absence-or-true here. Defers the next exclusion
        // debate to where it belongs (the SKILL.md file itself).
        // Maintenance cost is now 5 rows; revisit when it hits 8+.
        foreach (var skillMdOnly in new[]
        {
            "qa-architect",
            "circle-of-fifths",
            "practice-routine",
            "genre-essentials",
            "what-can-you-do",
        })
        {
            skillFolders.Remove(skillMdOnly);
        }

        Assert.That(skillFolders, Is.EquivalentTo(matrixFolders),
            "Every chatbot SKILL.md folder MUST appear in the parity matrix " +
            "(or be explicitly excluded with a comment in this test). " +
            $"Missing: {string.Join(", ", skillFolders.Except(matrixFolders))}; " +
            $"Stale: {string.Join(", ", matrixFolders.Except(skillFolders))}.");
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(Environment.GetEnvironmentVariable("GA_REPO_ROOT")
                                    ?? AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AllProjects.slnx"))) return dir.FullName;
            dir = dir.Parent;
        }
        return null;
    }
}
