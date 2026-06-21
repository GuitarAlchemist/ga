namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.AgentFramework;
using GA.Business.ML.Skills;

/// <summary>
/// Pins the live-reload contract for <see cref="FileBasedSkillsProvider"/>:
/// after a <c>SKILL.md</c> file is added, edited, or deleted under the
/// watched directory, calling <see cref="FileBasedSkillsProvider.Reload"/>
/// must reflect the on-disk truth in <see cref="FileBasedSkillsProvider.Skills"/>.
/// The FileSystemWatcher hook in production calls Reload on file events;
/// these tests exercise Reload directly so they're deterministic and
/// don't depend on watcher event timing.
/// </summary>
/// <remarks>
/// All tests construct the provider with <c>watchForChanges: false</c> and
/// drive Reload manually. One end-of-suite test (<see
/// cref="WatcherFires_ReloadAfterFileEdit"/>) opts back into the watcher
/// to validate the wiring at least once; it has a generous timeout to
/// absorb FileSystemWatcher event latency on slow CI runners.
/// </remarks>
[TestFixture]
public class FileBasedSkillsProviderReloadTests
{
    private string _tempRoot = null!;

    [SetUp]
    public void Setup()
    {
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "ga-skill-reload-tests-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempRoot);
    }

    [TearDown]
    public void Teardown()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
                Directory.Delete(_tempRoot, recursive: true);
        }
        catch
        {
            // Test cleanup; ignore lingering file handles on Windows.
        }
    }

    // ── Reload() semantics ────────────────────────────────────────────────────

    [Test]
    public void Reload_AfterFileEdit_ReflectsNewTriggers()
    {
        WriteSkill("alpha", "alpha-trigger-v1");
        using var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: false);
        Assert.That(provider.Skills, Has.Count.EqualTo(1));
        Assert.That(provider.Skills[0].Triggers, Has.Some.EqualTo("alpha-trigger-v1"));

        // Edit: same file, different trigger keyword.
        WriteSkill("alpha", "alpha-trigger-v2");
        provider.Reload();

        Assert.That(provider.Skills, Has.Count.EqualTo(1));
        Assert.That(provider.Skills[0].Triggers, Has.Some.EqualTo("alpha-trigger-v2"),
            "Reload must pick up edits to existing files");
        Assert.That(provider.Skills[0].Triggers, Has.None.EqualTo("alpha-trigger-v1"),
            "Old triggers must be evicted, not appended");
    }

    [Test]
    public void Reload_AfterFileAdded_PicksUpNewSkill()
    {
        WriteSkill("alpha", "alpha-trigger");
        using var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: false);
        Assert.That(provider.Skills, Has.Count.EqualTo(1));

        WriteSkill("beta", "beta-trigger");
        provider.Reload();

        Assert.That(provider.Skills, Has.Count.EqualTo(2),
            "Reload must include newly-added skill files");
        Assert.That(provider.Skills.Select(s => s.Name), Does.Contain("beta"));
    }

    [Test]
    public void Reload_AfterFileDeleted_RemovesSkill()
    {
        WriteSkill("alpha", "alpha-trigger");
        WriteSkill("beta", "beta-trigger");
        using var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: false);
        Assert.That(provider.Skills, Has.Count.EqualTo(2));

        var betaPath = Path.Combine(_tempRoot, "beta", "SKILL.md");
        File.Delete(betaPath);
        provider.Reload();

        Assert.That(provider.Skills, Has.Count.EqualTo(1),
            "Reload must drop deleted skills from the loaded set");
        Assert.That(provider.Skills.Select(s => s.Name), Does.Not.Contain("beta"));
    }

    [Test]
    public void Reload_IncrementsReloadCount()
    {
        WriteSkill("alpha", "alpha-trigger");
        using var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: false);
        var before = provider.ReloadCount;

        provider.Reload();
        provider.Reload();
        provider.Reload();

        Assert.That(provider.ReloadCount, Is.EqualTo(before + 3),
            "ReloadCount lets callers (and tests) confirm reload actually ran");
    }

    [Test]
    public void Reload_OnEmptyDirectory_DoesNotThrow_AndYieldsEmptySkillSet()
    {
        using var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: false);
        Assert.That(provider.Skills, Is.Empty);

        provider.Reload();

        Assert.That(provider.Skills, Is.Empty,
            "An empty directory must reload to an empty skill set without error");
    }

    [Test]
    public void Reload_AcceptsBothCasingConventions_AfterParityFix()
    {
        // Combined check: the parity work in PR #113 lets either Pascal or
        // camelCase frontmatter parse. Reload-after-rewriting in the
        // OPPOSITE casing must keep the skill loaded — proves an author
        // can refactor between conventions in-place without losing the skill.
        WritePascalCaseSkill("dual", "dual-trigger");
        using var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: false);
        Assert.That(provider.Skills, Has.Count.EqualTo(1));
        Assert.That(provider.Skills[0].Name, Is.EqualTo("dual"));

        // Rewrite the same file in camelCase.
        WriteSkill("dual", "dual-trigger-v2");
        provider.Reload();

        Assert.That(provider.Skills, Has.Count.EqualTo(1));
        Assert.That(provider.Skills[0].Name, Is.EqualTo("dual"),
            "Rewriting Pascal → camel must keep the skill loaded after Reload");
        Assert.That(provider.Skills[0].Triggers, Has.Some.EqualTo("dual-trigger-v2"));
    }

    // ── FileSystemWatcher integration (best-effort) ───────────────────────────

    [Test]
    [Timeout(10_000)]
    public async Task WatcherFires_ReloadAfterFileEdit()
    {
        // One end-to-end check that the watcher actually fires Reload on
        // edit. Polled with a long timeout because FileSystemWatcher event
        // latency varies by platform and CI runner. If this is flaky in CI,
        // the deterministic Reload tests above are the real safety net.
        WriteSkill("alpha", "alpha-trigger-v1");
        using var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: true);
        var initialReloadCount = provider.ReloadCount;
        Assert.That(provider.Skills[0].Triggers, Has.Some.EqualTo("alpha-trigger-v1"));

        WriteSkill("alpha", "alpha-trigger-v2");

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && provider.ReloadCount == initialReloadCount)
        {
            await Task.Delay(50);
        }

        Assert.That(provider.ReloadCount, Is.GreaterThan(initialReloadCount),
            "FileSystemWatcher should have fired Reload at least once within 5s of file edit");
        Assert.That(provider.Skills[0].Triggers, Has.Some.EqualTo("alpha-trigger-v2"),
            "After watcher-driven reload, edited triggers must be visible");
    }

    [Test]
    public void Dispose_StopsWatching_NoMoreReloadsAfterDispose()
    {
        WriteSkill("alpha", "alpha-trigger-v1");
        var provider = new FileBasedSkillsProvider(_tempRoot, watchForChanges: true);
        var afterStartup = provider.ReloadCount;

        provider.Dispose();

        // Edit after dispose; watcher is detached, no reload should fire.
        WriteSkill("alpha", "alpha-trigger-v2");
        Thread.Sleep(500);  // give any in-flight watcher event a chance to settle

        Assert.That(provider.ReloadCount, Is.EqualTo(afterStartup),
            "Disposed provider must not reload further");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void WriteSkill(string name, string trigger)
        => WriteSkillUnderRoot(_tempRoot, name, trigger, body: $"Body for {name}.");

    private static void WriteSkillUnderRoot(string root, string name, string trigger, string body)
    {
        var dir = Path.Combine(root, name);
        Directory.CreateDirectory(dir);
        // camelCase (canonical post-PR-#113).
        var content = $$"""
            ---
            name: {{name}}
            description: "Test skill for reload verification"
            triggers:
              - "{{trigger}}"
            ---
            # {{name}}

            {{body}}
            """;
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
    }

    // ── Multi-directory overlay (skill-stewards iteration loop) ───────────────
    //
    // The skill-stewards team uses two directories: `skills/` (canonical)
    // and `skills-dev/` (drafts). FileBasedSkillsProvider loads from BOTH
    // and uses last-write-wins on Name collision so a draft shadows its
    // canonical counterpart while the author iterates. Graduating a skill
    // is just `git mv skills-dev/<name> skills/<name>` — the override
    // naturally lifts.

    [Test]
    public void MultiDir_DraftSkillShadowsCanonicalOnNameCollision()
    {
        var canonical = Path.Combine(_tempRoot, "canonical");
        var drafts    = Path.Combine(_tempRoot, "drafts");
        Directory.CreateDirectory(canonical);
        Directory.CreateDirectory(drafts);

        WriteSkillUnderRoot(canonical, "shared-name", "canonical-trigger", "canonical body");
        WriteSkillUnderRoot(drafts,    "shared-name", "draft-trigger",     "DRAFT BODY");

        using var provider = new FileBasedSkillsProvider(
            new[] { canonical, drafts },
            watchForChanges: false);

        Assert.That(provider.Skills, Has.Count.EqualTo(1),
            "Same Name in both dirs collapses to one entry — last write wins");
        Assert.That(provider.Skills[0].Triggers, Has.Some.EqualTo("draft-trigger"),
            "Draft (later in priority order) must shadow canonical");
        Assert.That(provider.Skills[0].Body, Does.Contain("DRAFT BODY"));
    }

    [Test]
    public void MultiDir_NonOverlappingSkillsFromBothDirs_BothLoaded()
    {
        var canonical = Path.Combine(_tempRoot, "canonical");
        var drafts    = Path.Combine(_tempRoot, "drafts");
        Directory.CreateDirectory(canonical);
        Directory.CreateDirectory(drafts);

        WriteSkillUnderRoot(canonical, "alpha", "alpha-trigger", "alpha body");
        WriteSkillUnderRoot(drafts,    "beta",  "beta-trigger",  "beta body");

        using var provider = new FileBasedSkillsProvider(
            new[] { canonical, drafts },
            watchForChanges: false);

        Assert.That(provider.Skills, Has.Count.EqualTo(2));
        Assert.That(provider.Skills.Select(s => s.Name), Is.EquivalentTo(new[] { "alpha", "beta" }));
    }

    [Test]
    public void MultiDir_OneDirMissing_OtherStillLoads()
    {
        var canonical = Path.Combine(_tempRoot, "canonical");
        var drafts    = Path.Combine(_tempRoot, "drafts-does-not-exist");
        Directory.CreateDirectory(canonical);
        // intentionally do NOT create drafts

        WriteSkillUnderRoot(canonical, "alpha", "alpha-trigger", "alpha body");

        using var provider = new FileBasedSkillsProvider(
            new[] { canonical, drafts },
            watchForChanges: false);

        Assert.That(provider.Skills, Has.Count.EqualTo(1),
            "A missing draft directory must not break loading from canonical");
    }

    [Test]
    public void MultiDir_GraduationFlow_DraftRemovedAfterGitMv_CanonicalSurfaces()
    {
        // Simulates the graduation flow: author iterates on a draft, then
        // `git mv` it to canonical. Draft directory loses the file; canonical
        // gains it. After Reload, the canonical version surfaces. No
        // duplicate skill entries; no orphan triggers.
        var canonical = Path.Combine(_tempRoot, "canonical");
        var drafts    = Path.Combine(_tempRoot, "drafts");
        Directory.CreateDirectory(canonical);
        Directory.CreateDirectory(drafts);

        WriteSkillUnderRoot(drafts, "graduating", "draft-trigger", "draft body");

        using var provider = new FileBasedSkillsProvider(
            new[] { canonical, drafts },
            watchForChanges: false);

        Assert.That(provider.Skills, Has.Count.EqualTo(1));
        Assert.That(provider.Skills[0].Body, Does.Contain("draft body"));

        // Graduate: move file from drafts/ to canonical/.
        var draftSkill = Path.Combine(drafts, "graduating");
        var graduated  = Path.Combine(canonical, "graduating");
        Directory.Move(draftSkill, graduated);
        provider.Reload();

        Assert.That(provider.Skills, Has.Count.EqualTo(1),
            "Post-graduation: still exactly one skill (no duplicate)");
        Assert.That(provider.Skills[0].Body, Does.Contain("draft body"),
            "Same content — graduation just relocates the file");
    }

    [Test]
    public void MultiDir_Constructor_RejectsAllNullOrWhitespace() => Assert.Throws<ArgumentException>(
            () => new FileBasedSkillsProvider(new[] { "", "   ", null! }, watchForChanges: false));

    private void WritePascalCaseSkill(string name, string trigger)
    {
        var dir = Path.Combine(_tempRoot, name);
        Directory.CreateDirectory(dir);
        // PascalCase (parser fallback for externally-sourced files).
        var content = $$"""
            ---
            Name: {{name}}
            Description: "Test skill in PascalCase"
            Triggers:
              - "{{trigger}}"
            ---
            # {{name}}

            Body for {{name}}.
            """;
        File.WriteAllText(Path.Combine(dir, "SKILL.md"), content);
    }
}
