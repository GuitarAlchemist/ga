namespace GA.Business.ML.Tests.Unit;

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
}
