namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;

/// <summary>
/// Tests for <see cref="CatalogSkillMdLoader"/> — the helper that lets C#
/// catalog-skill wrappers (CircleOfFifthsSkill, etc.) load their answer body
/// from the matching SKILL.md file at runtime, keeping the markdown as the
/// single source of truth.
/// </summary>
[TestFixture]
public class CatalogSkillMdLoaderTests
{
    [Test]
    public void LoadBodyOrFallback_ReturnsBody_WhenSkillMdExists()
    {
        // The 4 graduated catalog skills (PR #124) each have a SKILL.md
        // present in the repo's skills/ directory. Loading any of them
        // should return a body substantially longer than the fallback
        // string, proving the loader actually reads the file.
        const string folderName = "circle-of-fifths";
        const string fallback = "FALLBACK_SHORT";

        var body = CatalogSkillMdLoader.LoadBodyOrFallback(folderName, fallback);

        Assert.That(body, Is.Not.EqualTo(fallback),
            $"loader should have returned the SKILL.md body, not the fallback — " +
            $"either the file is missing or the loader is broken");
        Assert.That(body.Length, Is.GreaterThan(500),
            $"circle-of-fifths SKILL.md body should be substantial (>500 chars); " +
            $"got {body.Length}. Length sanity-check guards against returning " +
            $"frontmatter-only or partial content.");
    }

    [Test]
    public void LoadBodyOrFallback_ReturnsFallback_WhenSkillMdMissing()
    {
        // A folder that doesn't exist forces the fallback path. The loader
        // must NOT throw — production code calls this from a static Lazy<T>
        // initializer, so an exception would surface as a TypeInitializationException
        // on first request, not at startup. The fallback ensures the
        // chatbot still says something useful even if the SKILL.md is
        // missing in deployment.
        const string folderName = "this-folder-definitely-does-not-exist-2026-05-05";
        const string fallback = "Sentinel fallback answer for a missing skill.";

        var body = CatalogSkillMdLoader.LoadBodyOrFallback(folderName, fallback);

        Assert.That(body, Is.EqualTo(fallback),
            "missing SKILL.md must yield the fallback string verbatim");
    }

    // Heuristic — any H2 the chatbot loader emits whose heading text matches
    // one of these phrases is almost certainly an authoring meta-section that
    // leaked past the stripper. Add a new entry here when a future leak
    // pattern is discovered; do NOT extend the production whitelist
    // (CatalogSkillMdLoader.MetaSectionHeadings) without thinking through
    // the false-positive case, because that whitelist is consulted at runtime.
    private static readonly string[] LeakSignatureHeadings =
    [
        "## How to dispatch",
        "## Dispatch",
        "## Dispatch notes",
        "## Routing notes",
        "## Routing rules",
        "## When to use",
        "## Authoring notes",
        "## Implementation notes",
    ];

    [TestCaseSource(nameof(EnumerateCatalogSkillFolders))]
    public void LoadedBody_HasNoLeakedMetaSectionHeadings(string skillFolderName)
    {
        // Regression guard for the 2026-05-13 leak: SKILL.md author put a
        // "## How to dispatch" section into practice-routine, the
        // StripModelDirectivePreamble state machine only stripped the
        // preamble zone between H1 and the first H2, and the dispatch
        // template reached end users in a chatbot response. The fix
        // (CatalogSkillMdLoader inMetaSection state machine + this whitelist)
        // is silent unless something asserts it stays plugged. So: scan
        // every catalog SKILL.md, run it through the loader, and require
        // the stripped output to contain NONE of the leak-signature headings.
        //
        // If THIS test fails, the choice is binary:
        //   (a) the SKILL.md authored a real user-visible section with a
        //       name that happens to look like a meta-section → rename it
        //       in the SKILL.md (not the test).
        //   (b) a new flavour of meta-section is leaking → extend
        //       CatalogSkillMdLoader.MetaSectionHeadings AND add a matching
        //       entry to LeakSignatureHeadings above.
        var body = CatalogSkillMdLoader.LoadBodyOrFallback(skillFolderName, fallback: "FALLBACK");
        if (body == "FALLBACK")
        {
            // No SKILL.md on disk for this folder — TestCaseSource enumeration
            // walked a folder that exists on Windows but is gitignored or staged.
            // Skip rather than fail: the LoadBodyOrFallback_ReturnsFallback_*
            // test already covers the missing-file path.
            Assert.Ignore($"{skillFolderName}/SKILL.md not present in this checkout");
            return;
        }

        foreach (var leakSig in LeakSignatureHeadings)
        {
            Assert.That(body, Does.Not.Contain(leakSig),
                $"skills/{skillFolderName}/SKILL.md: heading '{leakSig}' " +
                $"leaked past CatalogSkillMdLoader.StripModelDirectivePreamble. " +
                $"Either rename the section in the SKILL.md or extend " +
                $"CatalogSkillMdLoader.MetaSectionHeadings to cover it.");
        }
    }

    private static IEnumerable<string> EnumerateCatalogSkillFolders()
    {
        // Walk the repository's skills/ directory and yield every immediate
        // child that owns a SKILL.md. Discovery is done at test-discovery
        // time, so adding a new SKILL.md doesn't require editing this list.
        var skillsDir = SkillMdPlugin.ResolveSkillsPath();
        if (!Directory.Exists(skillsDir)) yield break;

        foreach (var dir in Directory.EnumerateDirectories(skillsDir))
        {
            if (File.Exists(Path.Combine(dir, "SKILL.md")))
                yield return Path.GetFileName(dir);
        }
    }
}
