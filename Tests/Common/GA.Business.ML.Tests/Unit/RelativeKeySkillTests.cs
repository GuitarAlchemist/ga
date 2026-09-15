namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// The keyword predicates the offline fallback of <c>SemanticIntentRouter</c> consults
/// when embeddings are unavailable: a relative-key question must reach
/// <see cref="RelativeKeySkill"/>, not <see cref="ScaleInfoSkill"/> (which would list
/// the notes of C major instead of answering "A minor").
/// </summary>
[TestFixture]
public class RelativeKeySkillTests
{
    private static RelativeKeySkill MakeSkill() => new(NullLogger<RelativeKeySkill>.Instance);

    [TestCase("What is the relative minor of C major?")]
    [TestCase("Relative major of A minor")]
    [TestCase("What is the parallel minor of F major")]
    [TestCase("Parallel major of D minor")]
    [TestCase("How many sharps in D major")]
    [TestCase("Key signature of B minor")]
    public void CanHandle_KeyRelationQuestions_ReturnsTrue(string prompt) =>
        Assert.That(MakeSkill().CanHandle(prompt), Is.True, prompt);

    [TestCase("What notes are in C major?")]
    [TestCase("Why does a ii-V-I sound resolved?")]
    [TestCase("")]
    public void CanHandle_OtherQuestions_ReturnsFalse(string prompt) =>
        Assert.That(MakeSkill().CanHandle(prompt), Is.False, prompt);

    [Test]
    public void ScaleInfoSkill_YieldsRelativeKeyQuestion()
    {
        var scaleInfo = new ScaleInfoSkill(NullLogger<ScaleInfoSkill>.Instance);

        Assert.Multiple(() =>
        {
            Assert.That(scaleInfo.CanHandle("What is the relative minor of C major?"), Is.False);
            Assert.That(scaleInfo.CanHandle("What notes are in C major?"), Is.True,
                "ScaleInfo keeps its own questions");
        });
    }

    [Test]
    public async Task ExecuteAsync_RelativeMinorOfCMajor_IsAMinor()
    {
        var response = await MakeSkill().ExecuteAsync("What is the relative minor of C major?");

        Assert.That(response.Result, Does.Contain("**Am**"));
    }
}
