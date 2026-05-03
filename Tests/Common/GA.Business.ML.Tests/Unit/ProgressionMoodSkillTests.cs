namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

[TestFixture]
public class ProgressionMoodSkillTests
{
    private static ProgressionMoodSkill MakeSkill() => new(NullLogger<ProgressionMoodSkill>.Instance);

    [Test]
    public void Metadata_ExposesExamplePrompts()
    {
        var skill = MakeSkill();
        Assert.That(skill.ExamplePrompts, Has.Count.GreaterThanOrEqualTo(4));
        Assert.That(skill.Description, Does.Contain("darken").IgnoreCase
            .Or.Contain("brighten").IgnoreCase
            .Or.Contain("modal interchange").IgnoreCase);
    }

    [Test]
    public void CanHandle_AlwaysFalse_DispatchedSemantically()
    {
        var skill = MakeSkill();
        Assert.That(skill.CanHandle("how do I make this progression sound darker"), Is.False);
    }

    [TestCase("How do I make this progression sound darker?")]
    [TestCase("Make this progression sound moodier")]
    [TestCase("How can I make my chords sound sadder?")]
    public async Task ExecuteAsync_DarkenQueries_ReturnDarkenTechniques(string prompt)
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync(prompt);

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("parallel minor").IgnoreCase);
        Assert.That(response.Result, Does.Contain("Phrygian").Or.Contain("Aeolian"));
        Assert.That(response.Evidence, Has.Some.Contains("parallel minor"));
    }

    [TestCase("Make my song sound brighter")]
    [TestCase("How to make a progression more uplifting")]
    public async Task ExecuteAsync_BrightenQueries_ReturnBrightenTechniques(string prompt)
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync(prompt);

        Assert.That(response.Confidence, Is.EqualTo(1.0f));
        Assert.That(response.Result, Does.Contain("parallel major").IgnoreCase);
        Assert.That(response.Result, Does.Contain("Lydian").Or.Contain("Mixolydian"));
    }

    [Test]
    public async Task ExecuteAsync_DarkenAnswerListsAtLeastFiveTechniques()
    {
        var skill = MakeSkill();
        var response = await skill.ExecuteAsync("how do I darken my progression");

        // The answer numbers techniques 1. 2. 3. 4. 5. — verify all five render.
        for (var i = 1; i <= 5; i++)
        {
            Assert.That(response.Result, Does.Contain($"{i}."),
                $"darken answer should number technique {i}");
        }
    }
}
