namespace GA.Business.ML.Tests.Unit;

using System.Diagnostics;
using GA.Business.ML.Agents.Plugins;
using Microsoft.Extensions.DependencyInjection;

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
    public void ResolveSkillsPath_RejectsPrefixCollisionEnvOverride()
    {
        // Defends against `<repo>/skills` matching the prefix of a sibling
        // directory like `<repo>-evil/skills`. Without the trailing separator
        // in the prefix check, ordinal-ignore-case StartsWith would accept it.
        var canonical = Path.Combine(_tmpRoot, "skills");
        Directory.CreateDirectory(canonical);

        // Create an "evil sibling" whose path starts with the same prefix as
        // _tmpRoot but is outside it.
        var siblingRoot = _tmpRoot + "-evil";
        Directory.CreateDirectory(siblingRoot);
        var siblingSkills = Path.Combine(siblingRoot, "skills");
        Directory.CreateDirectory(siblingSkills);
        try
        {
            Environment.SetEnvironmentVariable("SKILLMD_SKILLS_PATH", siblingSkills);

            var resolved = SkillMdPlugin.ResolveSkillsPath(_tmpRoot);

            Assert.That(resolved, Is.EqualTo(canonical),
                "Prefix-collision sibling path must be rejected — the prefix check has to require a directory boundary, not just a string prefix.");
        }
        finally
        {
            try { Directory.Delete(siblingRoot, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Test]
    public void Register_CanonicalEmptyButLegacyPopulated_WarnsAboutSilentSkillLoss()
    {
        // The migration footgun: someone runs `mkdir skills` while authoring
        // the canonical home, and the resolver immediately starts returning
        // the empty canonical path — silently shadowing the legacy directory
        // so production registers ZERO skills. Register() must shout in that
        // exact configuration so the migration doesn't silently brick prod.
        var canonical = Path.Combine(_tmpRoot, "skills");
        var legacyDir = Path.Combine(_tmpRoot, ".agent", "skills", "alpha");
        Directory.CreateDirectory(canonical);    // empty canonical
        Directory.CreateDirectory(legacyDir);
        File.WriteAllText(
            Path.Combine(legacyDir, "SKILL.md"),
            "---\nName: \"alpha\"\nDescription: \"Test\"\nTriggers:\n  - \"alpha\"\n---\n\nBody.");

        var traceListener = new TestTraceListener();
        Trace.Listeners.Add(traceListener);
        try
        {
            SkillMdPlugin.Register(new ServiceCollection(), canonical);
        }
        finally
        {
            Trace.Listeners.Remove(traceListener);
        }

        var output = traceListener.Output;
        Assert.That(output, Does.Contain("Canonical").And.Contain("empty"),
            "Register() must warn when canonical is empty and legacy still has skills, otherwise the migration silently bricks production.");
        Assert.That(output, Does.Contain("1 SKILL.md"),
            "warning must include the legacy skill count so the operator knows what's being shadowed");
    }

    /// <summary>
    /// Captures <see cref="System.Diagnostics.Trace.WriteLine(string?)"/> output for inspection.
    /// Debug.Listeners and Trace.Listeners share the same underlying collection, so in practice
    /// this listener captures both — but production code in
    /// <c>SkillMdPlugin</c> / <c>FileBasedSkillsProvider</c> deliberately uses
    /// <c>Trace.WriteLine</c> rather than <c>Debug.WriteLine</c>, because the latter is
    /// <c>[Conditional("DEBUG")]</c> and silently strips out of Release builds.
    /// </summary>
    private sealed class TestTraceListener : TraceListener
    {
        private readonly System.Text.StringBuilder _buf = new();
        public string Output => _buf.ToString();
        public override void Write(string? message)     => _buf.Append(message);
        public override void WriteLine(string? message) => _buf.AppendLine(message);
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
