namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Capo questions as people ask them (issue #888): the fret is often named as "capo on fret 3",
///     and the sounding key is named the way a key signature spells it.
/// </summary>
[TestFixture]
public class CapoSkillTests
{
    private static readonly CapoSkill Capo = new(NullLogger<CapoSkill>.Instance);

    [TestCase("With a capo on fret 3, what key does a G shape sound in?", "sounds as **Bb**")]
    [TestCase("capo at fret 5, what does a C shape sound like", "sounds as **F**")]
    [TestCase("I play an E shape with the capo on the 2nd fret", "sounds as **F#**")]
    [TestCase("What does a G shape sound like with capo 2", "sounds as **A**")]
    public async Task ShapeWithCapo_NamesTheSoundingKey(string prompt, string expected)
    {
        var response = await Capo.ExecuteAsync(prompt);

        Assert.That(response.Result, Does.Contain(expected));
    }

    [TestCase("A shape with capo 1", "sounds as **Bb**")]
    [TestCase("C shape with capo 1", "sounds as **Db**")]
    [TestCase("C shape with capo 3", "sounds as **Eb**")]
    [TestCase("E shape with capo 4", "sounds as **Ab**")]
    [TestCase("D shape with capo 4", "sounds as **F#**")]
    [TestCase("Em shape with capo 4", "sounds as **G#m**")]
    [TestCase("Am shape with capo 1", "sounds as **Bbm**")]
    public async Task SoundingKey_WithoutAPreference_UsesTheKeySignatureSpelling(string prompt, string expected)
    {
        var response = await Capo.ExecuteAsync(prompt);

        Assert.That(response.Result, Does.Contain(expected));
    }

    [Test]
    public async Task SongKeyWithCapoOnFret_NamesTheShape()
    {
        var response = await Capo.ExecuteAsync("The song is in A, capo on fret 2, what shape do I play?");

        Assert.That(response.Result, Does.Contain("play a **G shape**"));
    }

    private static readonly IReadOnlyList<ChatHistoryTurn> CapoTwoGShape =
    [
        new("user", "With a capo on fret 2, what key does a G shape sound in?"),
        new("assistant", "A **G shape** with a capo on fret **2** sounds as **A** (A major)."),
    ];

    // An elliptical follow-up names only what changed; the other argument comes from the
    // previous capo question.
    [TestCase("And a C shape?", "sounds as **D**")]
    [TestCase("and the Em shape?", "sounds as **F#m**")]
    [TestCase("What about capo 4?", "sounds as **B**")]
    public async Task EllipticalFollowUp_TakesTheMissingArgumentFromThePreviousQuestion(string prompt, string expected)
    {
        var response = await Capo.ExecuteAsync(prompt, CapoTwoGShape);

        Assert.That(response.Declined, Is.False);
        Assert.That(response.Result, Does.Contain(expected));
    }

    [TestCase("And a C shape?")]
    [TestCase("What about capo 4?")]
    public async Task EllipticalFollowUp_WithoutHistory_Declines(string prompt)
    {
        var response = await Capo.ExecuteAsync(prompt, []);

        Assert.That(response.Declined, Is.True);
    }

    [Test]
    public async Task UnrelatedFollowUp_AfterACapoQuestion_Declines()
    {
        var response = await Capo.ExecuteAsync("How do I develop a good finger vibrato?", CapoTwoGShape);

        Assert.That(response.Declined, Is.True);
    }
}
