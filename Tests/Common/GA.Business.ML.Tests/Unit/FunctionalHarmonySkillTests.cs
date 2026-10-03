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
/// Verification harness for <see cref="FunctionalHarmonySkill"/> — the first
/// reference Path B skill: a <see cref="SkillMdDrivenWrapperBase"/> with no
/// closure, whose SKILL.md body is verified reference text that grounds the
/// LLM's answer.
/// </summary>
[TestFixture]
public class FunctionalHarmonySkillTests
{
    private const string SkillMdFolder = "functional-harmony";

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

    private static FunctionalHarmonySkill MakeSkill(string responseText = "A deceptive cadence is V to vi.") =>
        new(EmptyTools(), FactoryFor(FakeClient(responseText)), NullLoggerFactory.Instance);

    private static SkillMd LoadSkillMd()
    {
        var path = Path.Combine(SkillMdPlugin.ResolveSkillsPath(), SkillMdFolder, "SKILL.md");
        var skillMd = SkillMdParser.TryParse(path);
        Assert.That(skillMd, Is.Not.Null, $"skills/{SkillMdFolder}/SKILL.md must parse");
        return skillMd!;
    }

    [Test]
    public void RoutingMetadata_IsSubstantive()
    {
        var skill = MakeSkill();

        Assert.Multiple(() =>
        {
            Assert.That(skill.Name, Is.EqualTo("FunctionalHarmony"));
            Assert.That(skill.Description.Length, Is.GreaterThan(50),
                "Description fuels SemanticIntentRouter similarity match — needs more than a one-liner");
            Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(skill.CanHandle("what is a deceptive cadence"), Is.False,
                "semantic-routing only; no legacy-regex shadow");
        });
    }

    [Test]
    public async Task ExecuteAsync_ReferenceSkill_PassesAnswerThrough_WithoutClosureWarning()
    {
        // A reference skill has no closure, so not calling ga_dsl_eval is the
        // expected path: no "Closure:" tag, no "NOT invoked" warning. The
        // answer is still LLM-only, so confidence keeps the 0.5 cap.
        const string fakeAnswer = "A deceptive cadence is V(7) to vi: the expected tonic is replaced.";
        var skill = MakeSkill(fakeAnswer);

        var response = await skill.ExecuteAsync("what is a deceptive cadence");

        Assert.Multiple(() =>
        {
            Assert.That(response.AgentId, Is.EqualTo(AgentIds.Theory));
            Assert.That(response.Result, Is.EqualTo(fakeAnswer));
            Assert.That(response.Evidence, Has.Some.Contains($"skills/{SkillMdFolder}/SKILL.md"));
            Assert.That(response.Evidence, Has.None.Contains("ga_dsl_eval"),
                "reference skill: no closure tag and no ga_dsl_eval-not-invoked warning");
            Assert.That(response.Confidence, Is.LessThanOrEqualTo(0.5f));
        });
    }

    [Test]
    public void SkillMd_IsAReferenceSkillWithNoFretNumbers()
    {
        var skillMd = LoadSkillMd();

        Assert.Multiple(() =>
        {
            Assert.That(skillMd.Name, Is.EqualTo(SkillMdFolder));
            Assert.That(skillMd.Body, Does.Not.Contain("ga_dsl_eval"),
                "a reference skill names no closure for the LLM to dispatch");
            Assert.That(skillMd.Body, Does.Not.Match(@"\bfret\s+\d"),
                "concept answers name chords and notes, never fret positions");
        });
    }

    // The vetted facts the answering LLM is told never to contradict. Each
    // string is a spelling or progression derived by hand in the PR that
    // added the skill; a failure here means the reference text changed.
    [TestCase("G7 = G B D F (scale degrees 5, 7, 2, 4)")]
    [TestCase("B rises a half step to C, the root of I")]
    [TestCase("F falls a half step to E, the 3rd of I")]
    [TestCase("E7 = E G# B D, and G# rises to A")]
    [TestCase("Db7 = Db F Ab Cb has F (3rd) and Cb (7th)")]
    [TestCase("F, now the 3rd of Db7, falls a half step to E, the 3rd of C")]
    [TestCase("C → B, the 3rd of G")]
    [TestCase("| V (G) | V7/V = D7 | D F# A C |")]
    [TestCase("| vi (Am) | V7/vi = E7 | E G# B D |")]
    [TestCase("bII. In C minor (or C major) it is Db F Ab")]
    [TestCase("| Italian (It+6) | b6, 1, #4 | Ab C F# |")]
    [TestCase("| French (Fr+6) | b6, 1, 2, #4 | Ab C D F# |")]
    [TestCase("| German (Ger+6) | b6, 1, b3, #4 | Ab C Eb F# |")]
    [TestCase("| Half (HC) | Any chord → V")]
    [TestCase("| Plagal | IV → I")]
    [TestCase("| Deceptive | V(7) → vi")]
    [TestCase("the 7th F falls a half step to E, the 5th of Am")]
    public void SkillMd_StatesVettedFact(string fact)
    {
        Assert.That(LoadSkillMd().Body, Does.Contain(fact));
    }
}
