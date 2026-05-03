namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Plugins;

/// <summary>
/// Tests for <see cref="SkillMdPlugin.ResolveSkillsPath"/> — verifies the
/// canonical-first lookup order documented in the 2026-05-03 migration
/// recommendation. Uses internal access via InternalsVisibleTo.
/// </summary>
[TestFixture]
public class SkillMdPluginResolverTests
{
    private string _tmpRoot = string.Empty;

    [SetUp]
    public void Setup()
    {
        _tmpRoot = Path.Combine(Path.GetTempPath(), "ga-resolver-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tmpRoot);
        // .git marker so FindRepoRoot anchors here rather than the real repo
        File.WriteAllText(Path.Combine(_tmpRoot, ".git"), "gitdir: fake\n");
    }

    [TearDown]
    public void Teardown()
    {
        Environment.SetEnvironmentVariable("SKILLMD_SKILLS_PATH", null);
        if (Directory.Exists(_tmpRoot))
        {
            try { Directory.Delete(_tmpRoot, recursive: true); }
            catch { /* best-effort under Windows file locks */ }
        }
    }

    [Test]
    public void ResolveSkillsPath_PrefersCanonicalSkillsDir()
    {
        // Both directories exist — canonical must win.
        var canonical = Path.Combine(_tmpRoot, "skills");
        var legacy    = Path.Combine(_tmpRoot, ".agent", "skills");
        Directory.CreateDirectory(canonical);
        Directory.CreateDirectory(legacy);

        var resolved = SkillMdPlugin.ResolveSkillsPath(_tmpRoot);

        Assert.That(resolved, Is.EqualTo(canonical),
            "When both 'skills/' and '.agent/skills/' exist, the canonical path wins per the migration recommendation.");
    }

    [Test]
    public void ResolveSkillsPath_FallsBackToLegacyWhenCanonicalMissing()
    {
        // Only .agent/skills exists — back-compat path so unported skills
        // don't disappear at flip-time.
        var legacy = Path.Combine(_tmpRoot, ".agent", "skills");
        Directory.CreateDirectory(legacy);

        var resolved = SkillMdPlugin.ResolveSkillsPath(_tmpRoot);

        Assert.That(resolved, Is.EqualTo(legacy),
            "When only legacy '.agent/skills/' exists, it should still be picked up so unported skills load.");
    }

    [Test]
    public void ResolveSkillsPath_ReturnsCanonicalWhenNeitherExists()
    {
        // Neither exists — return the canonical path so the Register() warning
        // points users at the right place to author new skills.
        var resolved = SkillMdPlugin.ResolveSkillsPath(_tmpRoot);

        Assert.That(resolved, Is.EqualTo(Path.Combine(_tmpRoot, "skills")),
            "When neither directory exists, the resolver should return the canonical path so the missing-dir warning points at the right authoring location.");
    }

    [Test]
    public void ResolveSkillsPath_EnvVarOverrideWinsWhenInsideRepoRoot()
    {
        var canonical = Path.Combine(_tmpRoot, "skills");
        var override_ = Path.Combine(_tmpRoot, "custom-skills");
        Directory.CreateDirectory(canonical);
        Directory.CreateDirectory(override_);

        Environment.SetEnvironmentVariable("SKILLMD_SKILLS_PATH", override_);

        var resolved = SkillMdPlugin.ResolveSkillsPath(_tmpRoot);

        Assert.That(resolved, Is.EqualTo(override_),
            "Env var override that resolves inside the repo root must win over canonical/legacy probing.");
    }

    [Test]
    public void ResolveSkillsPath_EnvVarOverrideRejectedWhenOutsideRepoRoot()
    {
        // Path-traversal defense: an env var pointing outside the repo root
        // must be ignored to prevent injection of arbitrary system prompts.
        var canonical = Path.Combine(_tmpRoot, "skills");
        Directory.CreateDirectory(canonical);

        var outside = Path.Combine(Path.GetTempPath(), "ga-outside-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        try
        {
            Environment.SetEnvironmentVariable("SKILLMD_SKILLS_PATH", outside);

            var resolved = SkillMdPlugin.ResolveSkillsPath(_tmpRoot);

            Assert.That(resolved, Is.EqualTo(canonical),
                "Env var pointing outside the repo root must be ignored, falling back to canonical path resolution.");
        }
        finally
        {
            try { Directory.Delete(outside, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Test]
    public void ResolveSkillsPath_NoGitMarker_FallsBackToCwd()
    {
        // Strip the .git marker to simulate "not in a repo" — should fall back
        // to the CWD-relative canonical path.
        File.Delete(Path.Combine(_tmpRoot, ".git"));

        var resolved = SkillMdPlugin.ResolveSkillsPath(_tmpRoot);

        Assert.That(resolved, Does.EndWith(Path.Combine(string.Empty, "skills"))
            .Or.EndWith(Path.DirectorySeparatorChar + "skills"),
            "Without a .git anchor the resolver falls back to <cwd>/skills.");
    }
}
