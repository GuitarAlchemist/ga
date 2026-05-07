namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Extensions;
using GA.Business.ML.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Verification harness for <see cref="TransposeSkill"/> — the first canary
/// for the DSL-eval pattern (Phase 2 of
/// <c>docs/plans/2026-05-06-skills-orchestration-architecture.md</c>).
/// </summary>
/// <remarks>
/// The skill no longer emits the SKILL.md body verbatim; it delegates
/// <c>ExecuteAsync</c> to a <see cref="SkillMdDrivenSkill"/> wrapping the
/// canonical <c>skills/transpose/SKILL.md</c> so the LLM-in-the-loop pass
/// (Anthropic + <c>ga_dsl_eval</c>) actually fires. These tests cover:
/// routing-metadata invariants (<c>Name</c>, <c>Description</c>,
/// <c>ExamplePrompts</c>), the architectural promise that the SKILL.md
/// references <c>ga_dsl_eval</c> and not a fictional keyhole tool, and the
/// wrapper's evidence/AgentId/result-passthrough contract via a fake
/// <see cref="IChatClient"/> that doesn't touch the network.
/// </remarks>
[TestFixture]
public class TransposeSkillTests
{
    private const string SkillMdFolder = "transpose";

    private static IChatClientFactory FactoryFor(IChatClient client)
    {
        var mock = new Mock<IChatClientFactory>();
        mock.Setup(f => f.Create(It.IsAny<string>())).Returns(client);
        return mock.Object;
    }

    private static IChatClient FakeClient(string responseText)
    {
        var mock = new Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        return mock.Object;
    }

    private static IMcpToolsProvider EmptyTools()
    {
        var mock = new Mock<IMcpToolsProvider>();
        mock.Setup(p => p.GetToolsAsync(It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<AIFunction>>(Array.Empty<AIFunction>()));
        return mock.Object;
    }

    private static TransposeSkill MakeSkill(string responseText = "Gmaj7")
    {
        var factory = FactoryFor(FakeClient(responseText));
        return new TransposeSkill(EmptyTools(), factory, NullLoggerFactory.Instance);
    }

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
    public async Task ExecuteAsync_DelegatesToSkillMdDrivenSkill_AndPassesResultThrough()
    {
        // Path B: the wrapper builds a SkillMdDrivenSkill backed by the
        // injected IChatClient and forwards its Result. Verifies the LLM
        // text round-trips and the wrapper does NOT replace it with the
        // SKILL.md body verbatim (the previous markdown-emitter behaviour).
        const string fakeAnswer = "Gmaj7";
        var skill = MakeSkill(responseText: fakeAnswer);

        var response = await skill.ExecuteAsync("transpose Cmaj7 up a perfect fifth");

        Assert.Multiple(() =>
        {
            Assert.That(response.AgentId, Is.EqualTo(AgentIds.Theory),
                "wrapper still owns AgentId so downstream UIs/tests stay stable");
            Assert.That(response.Result, Is.EqualTo(fakeAnswer),
                "Path B: response.Result is the LLM's text, not SKILL.md body");
            Assert.That(response.Evidence, Has.Some.Contains($"skills/{SkillMdFolder}"));
            Assert.That(response.Evidence, Has.Some.Contains("ga_dsl_eval"),
                "evidence cites the dispatch path so the chatbot UI surfaces it");
        });
    }

    [Test]
    public async Task ExecuteAsync_DoesNotEmitSkillMdBodyVerbatim()
    {
        // Regression guard against accidentally reverting to the
        // markdown-emitter pattern. With Path B the response should NEVER
        // equal the raw SKILL.md body — that's the exact failure mode the
        // 2026-05-06 Phase 2 finding flagged.
        var skill = MakeSkill(responseText: "Gmaj7");

        var skillsDir = SkillMdPlugin.ResolveSkillsPath();
        var path = Path.Combine(skillsDir, SkillMdFolder, "SKILL.md");
        var skillMd = SkillMdParser.TryParse(path);
        Assert.That(skillMd, Is.Not.Null);

        var response = await skill.ExecuteAsync("test");

        Assert.That(response.Result, Is.Not.EqualTo(skillMd!.Body),
            "Path B: ExecuteAsync MUST go through the LLM, not echo the body");
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
        // Architectural invariant of the Phase 2 canary: SKILL.md routes
        // through ga_dsl_eval to the domain.transposeChord closure, NOT a
        // fictional ga_transpose_chord keyhole tool. Independent of how
        // ExecuteAsync delegates — a SKILL.md edit shouldn't drift the
        // tool surface.
        var skillsDir = SkillMdPlugin.ResolveSkillsPath();
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
