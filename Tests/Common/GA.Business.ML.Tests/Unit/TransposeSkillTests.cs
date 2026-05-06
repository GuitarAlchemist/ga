namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Verification harness for <see cref="TransposeSkill"/> — the first canary
/// for the DSL-eval pattern (Phase 2 of
/// <c>docs/plans/2026-05-06-skills-orchestration-architecture.md</c>).
/// Mirrors the catalog-skill harness from PR #135 because the wrapper
/// pattern is the same: thin C# class emits SKILL.md body verbatim; the
/// LLM does the actual work via <c>ga_dsl_eval</c>.
/// </summary>
[TestFixture]
public class TransposeSkillTests
{
    private const string SkillMdFolder = "transpose";

    private static TransposeSkill MakeSkill() => new(NullLogger<TransposeSkill>.Instance);

    [Test]
    public void HasSubstantiveDescription()
    {
        var skill = MakeSkill();

        Assert.That(skill.Description, Is.Not.Null.And.Not.Empty);
        Assert.That(skill.Description.Length, Is.GreaterThan(50),
            "Description fuels SemanticIntentRouter similarity match — needs more than a one-liner");
    }

    [Test]
    public void HasAtLeast3ExamplePrompts()
    {
        var skill = MakeSkill();

        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(3),
            "transpose must declare >=3 example prompts for routing diversity");
    }

    [Test]
    public void CanHandle_AlwaysFalse()
    {
        var skill = MakeSkill();

        Assert.That(skill.CanHandle("transpose Cmaj7 up a fourth"), Is.False,
            "tool-driven skill — semantic-routing only; no legacy-regex shadow");
        Assert.That(skill.CanHandle("any random message"), Is.False);
        Assert.That(skill.CanHandle(string.Empty), Is.False);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsTheoryAgentResponse()
    {
        var skill = MakeSkill();

        var response = await skill.ExecuteAsync("transpose F up a minor third");

        Assert.Multiple(() =>
        {
            Assert.That(response.AgentId, Is.EqualTo(AgentIds.Theory));
            Assert.That(response.Confidence, Is.EqualTo(1.0f));
            Assert.That(response.Result, Is.Not.Null.And.Not.Empty);
            Assert.That(response.Result.Length, Is.GreaterThan(500),
                "transpose SKILL.md is multi-section — body should be >500 chars");
            Assert.That(response.Evidence, Has.Some.Contains($"skills/{SkillMdFolder}"));
            Assert.That(response.Evidence, Has.Some.Contains("ga_dsl_eval"),
                "evidence must cite the dispatch path so the LLM has a hint how to answer");
        });
    }

    [Test]
    public async Task ExecuteAsync_BodyMatchesSkillMdVerbatim()
    {
        // Strongest test: response body equals the SKILL.md body byte-for-byte.
        // A hardcoded-string stub trivially fails (the body is multi-KB).
        // Drift between SKILL.md and the C# answer is also caught here.
        var skill = MakeSkill();

        var skillsDir = GA.Business.ML.Agents.Plugins.SkillMdPlugin.ResolveSkillsPath();
        var path = Path.Combine(skillsDir, SkillMdFolder, "SKILL.md");
        Assert.That(File.Exists(path), Is.True,
            $"prerequisite: skills/{SkillMdFolder}/SKILL.md must exist");

        var skillMd = SkillMdParser.TryParse(path);
        Assert.That(skillMd, Is.Not.Null);

        var response = await skill.ExecuteAsync("test");

        Assert.That(response.Result, Is.EqualTo(skillMd!.Body),
            $"TransposeSkill.ExecuteAsync must emit the SKILL.md body verbatim — " +
            $"any drift means the C# class is duplicating content that should " +
            $"live only in the .md");
    }

    [Test]
    public void Name_MatchesExpected()
    {
        var skill = MakeSkill();

        Assert.That(skill.Name, Is.EqualTo("Transpose"));
    }

    [Test]
    public void SkillMd_FrontmatterDeclaresGaDslEvalAllowedTool()
    {
        // Confirms the SKILL.md is wired through ga_dsl_eval (not a fictional
        // ga_transpose_chord keyhole tool from the original DRAFT.md). This
        // is the architectural invariant of the Phase 2 canary.
        var skillsDir = GA.Business.ML.Agents.Plugins.SkillMdPlugin.ResolveSkillsPath();
        var path = Path.Combine(skillsDir, SkillMdFolder, "SKILL.md");
        var content = File.ReadAllText(path);

        Assert.That(content, Does.Contain("ga_dsl_eval"),
            "transpose SKILL.md must reference the ga_dsl_eval tool surface");
        Assert.That(content, Does.Contain("domain.transposeChord"),
            "transpose SKILL.md must name the closure invocation target");
        Assert.That(content, Does.Not.Contain("ga_transpose_chord"),
            "old fictional ga_transpose_chord keyhole tool must NOT remain in the body");
    }
}
