namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verification harness for the four catalog SKILL.md skills graduated in
/// PR #124 and wired in PR #126: <c>circle-of-fifths</c>,
/// <c>practice-routine</c>, <c>genre-essentials</c>, <c>what-can-you-do</c>.
/// </summary>
/// <remarks>
/// <para>
/// These skills are intentionally <i>SKILL.md-only</i> at the body level —
/// each C# class is a thin wrapper that supplies the routing metadata
/// (Description, ExamplePrompts) the <c>SemanticIntentRouter</c> needs and
/// emits the SKILL.md body verbatim on <c>ExecuteAsync</c>. Tests verify
/// that contract end to end without requiring Ollama (the embedding-driven
/// routing layer is exercised separately).
/// </para>
/// <para>
/// Stub-resistance: the strongest test in this fixture is
/// <see cref="ExecuteAsync_BodyMatchesSkillMdVerbatim"/>, which compares
/// the wrapper's output to the actual file content byte-for-byte (after
/// frontmatter strip) — a stubbed wrapper that returns a hardcoded string
/// would fail.
/// </para>
/// </remarks>
[TestFixture]
public class CatalogSkillTests
{
    /// <summary>
    /// One row per catalog skill: the wrapper type + the SKILL.md folder
    /// name. Anything else needed (Name, Description) is read off the
    /// instance — tests check those properties are non-trivial without
    /// hardcoding their exact values, so the test stays robust to copy
    /// edits in the SKILL.md.
    /// </summary>
    public sealed record CatalogSkillCase(Type SkillType, string SkillMdFolder, string ExpectedFrontmatterName);

    private static readonly CatalogSkillCase[] Cases =
    [
        new(typeof(CircleOfFifthsSkill),  "circle-of-fifths",  "circle-of-fifths"),
        new(typeof(PracticeRoutineSkill), "practice-routine",  "practice-routine"),
        new(typeof(GenreEssentialsSkill), "genre-essentials",  "genre-essentials"),
        new(typeof(WhatCanYouDoSkill),    "what-can-you-do",   "what-can-you-do"),
    ];

    private static IEnumerable<TestCaseData> AllCases =>
        Cases.Select(c => new TestCaseData(c).SetName($"Catalog_{c.SkillMdFolder}"));

    private static IOrchestratorSkill MakeSkill(Type skillType)
    {
        // Each wrapper takes a single ILogger<TSkill> dependency. NullLogger
        // is the standard test stand-in; we don't assert on log output here.
        var loggerType  = typeof(NullLogger<>).MakeGenericType(skillType);
        var loggerInst  = loggerType.GetField("Instance",
                              System.Reflection.BindingFlags.Public |
                              System.Reflection.BindingFlags.Static)!.GetValue(null);
        return (IOrchestratorSkill)Activator.CreateInstance(skillType, loggerInst)!;
    }

    [TestCaseSource(nameof(AllCases))]
    public void HasSubstantiveDescription(CatalogSkillCase c)
    {
        var skill = MakeSkill(c.SkillType);

        Assert.That(skill.Description, Is.Not.Null.And.Not.Empty,
            $"{c.SkillType.Name}.Description must be non-empty — it's the " +
            $"primary input to SemanticIntentRouter's similarity match");
        Assert.That(skill.Description.Length, Is.GreaterThan(50),
            $"{c.SkillType.Name}.Description is too short ({skill.Description.Length} chars). " +
            $"A one-word stub passes Is.Not.Empty but starves the router; require >50 chars.");
    }

    [TestCaseSource(nameof(AllCases))]
    public void HasAtLeast3ExamplePrompts(CatalogSkillCase c)
    {
        var skill = MakeSkill(c.SkillType);

        // 3 is the floor used elsewhere in the corpus (see
        // SkillMdParser.MinTriggerLength → SkillStewardsDraftLoadTests).
        // Fewer prompts means the router's discovery surface is too narrow
        // to route on diverse phrasings.
        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(3),
            $"{c.SkillType.Name} must declare >=3 example prompts for routing diversity");
    }

    [TestCaseSource(nameof(AllCases))]
    public void CanHandle_AlwaysFalse(CatalogSkillCase c)
    {
        // Catalog skills route exclusively via SemanticIntentRouter
        // (ExamplePrompts → embeddings → similarity). The legacy
        // CanHandle path stays disabled so a substring match doesn't
        // shadow a more specific skill — e.g., "what" appearing in a
        // chord-info question shouldn't pull in WhatCanYouDoSkill.
        var skill = MakeSkill(c.SkillType);

        Assert.That(skill.CanHandle("any random message"),       Is.False);
        Assert.That(skill.CanHandle("what is the circle of fifths"), Is.False);
        Assert.That(skill.CanHandle(string.Empty),                Is.False);
    }

    [TestCaseSource(nameof(AllCases))]
    public async Task ExecuteAsync_ReturnsTheoryAgentResponse(CatalogSkillCase c)
    {
        var skill = MakeSkill(c.SkillType);

        var response = await skill.ExecuteAsync("test query");

        Assert.Multiple(() =>
        {
            Assert.That(response.AgentId, Is.EqualTo(AgentIds.Theory),
                $"catalog skills emit Theory-bucket responses (per migration plan)");
            Assert.That(response.Confidence, Is.EqualTo(1.0f),
                $"deterministic catalog answer — confidence is always 1.0");
            Assert.That(response.Result, Is.Not.Null.And.Not.Empty,
                $"{c.SkillType.Name} returned an empty body");
            Assert.That(response.Result.Length, Is.GreaterThan(200),
                $"catalog body too short ({response.Result.Length} chars) — " +
                $"either the SKILL.md is empty or the loader returned the fallback. " +
                $"All four real SKILL.md bodies are >2KB.");
            Assert.That(response.Evidence, Has.Some.Contains($"skills/{c.SkillMdFolder}"),
                $"evidence should cite the source SKILL.md path");
        });
    }

    [TestCaseSource(nameof(AllCases))]
    public async Task ExecuteAsync_BodyMatchesSkillMdVerbatim(CatalogSkillCase c)
    {
        // The strongest test: the wrapper must return the SKILL.md body
        // exactly. A stubbed implementation that returns a hardcoded
        // string would fail this immediately. Drift between SKILL.md and
        // the C# answer is also caught here (the bug pattern from the
        // PR #121 audit, where SKILL.md fixes hadn't propagated to the
        // duplicated C# fast-path content).
        var skill = MakeSkill(c.SkillType);

        var skillsDir = GA.Business.ML.Agents.Plugins.SkillMdPlugin.ResolveSkillsPath();
        var path = Path.Combine(skillsDir, c.SkillMdFolder, "SKILL.md");
        Assert.That(File.Exists(path), Is.True,
            $"prerequisite: skills/{c.SkillMdFolder}/SKILL.md must exist on disk; " +
            $"if this fails, the test environment is broken, not the code under test");

        var skillMd = SkillMdParser.TryParse(path);
        Assert.That(skillMd, Is.Not.Null,
            $"prerequisite: SKILL.md at {path} must parse cleanly");

        var response = await skill.ExecuteAsync("test query");

        Assert.That(response.Result, Is.EqualTo(skillMd!.Body),
            $"{c.SkillType.Name}.ExecuteAsync must emit the SKILL.md body verbatim — " +
            $"any drift means the C# class is duplicating content that should live " +
            $"only in the .md, OR the loader returned the fallback");
    }

    [TestCaseSource(nameof(AllCases))]
    public void Name_MatchesSkillMdFrontmatterName(CatalogSkillCase c)
    {
        // The C# Name property doesn't have to match the SKILL.md
        // frontmatter Name — they serve different audiences (C# uses
        // PascalCase 'CircleOfFifths' for routing tags; the .md uses
        // kebab-case 'circle-of-fifths' for filesystem layout). What
        // MUST match is the SKILL.md folder ↔ frontmatter name (the
        // chatbot's intent registry keys off the latter). Verify that
        // the folder name we expect resolves to a SKILL.md whose
        // frontmatter actually carries the same name — proves nothing
        // got renamed-and-forgotten.
        var skillsDir = GA.Business.ML.Agents.Plugins.SkillMdPlugin.ResolveSkillsPath();
        var path      = Path.Combine(skillsDir, c.SkillMdFolder, "SKILL.md");
        var skillMd   = SkillMdParser.TryParse(path);

        Assert.That(skillMd, Is.Not.Null);
        Assert.That(skillMd!.Name, Is.EqualTo(c.ExpectedFrontmatterName),
            $"SKILL.md frontmatter name '{skillMd.Name}' must equal " +
            $"the folder name '{c.ExpectedFrontmatterName}' for the parity " +
            $"matrix and intent registry to agree");
    }
}
