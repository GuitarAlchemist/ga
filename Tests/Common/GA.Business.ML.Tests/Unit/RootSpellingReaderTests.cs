namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     Root spelling → pitch class in the skills that used to carry their own root table, now read through
///     <see cref="GA.Business.ML.Agents.ChordVocabulary.TryGetPitchClass"/>.
/// </summary>
/// <remarks>
///     The shared table also reads the enharmonic edge spellings B#, Cb, E# and Fb. That is right where the skill
///     only does pitch-class arithmetic (capo, tuning deltas). <see cref="RelativeKeySkill"/> quotes key signatures,
///     so it keeps refusing those four spellings as before.
/// </remarks>
[TestFixture]
public class RootSpellingReaderTests
{
    private static readonly CapoSkill Capo = new(NullLogger<CapoSkill>.Instance);
    private static readonly AlternateTuningsSkill Tunings = new(NullLogger<AlternateTuningsSkill>.Instance);
    private static readonly RelativeKeySkill RelativeKey = new(NullLogger<RelativeKeySkill>.Instance);

    [TestCase("G shape with capo 2", "sounds as **A**")]
    [TestCase("Cb shape with capo 1", "sounds as **C**")]
    [TestCase("Fb shape with capo 2", "sounds as **Gb**")]
    [TestCase("E# shape with capo 1", "sounds as **F#**")]
    [TestCase("song in Cb with capo 1", "play a **Bb shape**")]
    public async Task CapoSkill_ReadsRootSpellings(string prompt, string expected)
    {
        var response = await Capo.ExecuteAsync(prompt);

        Assert.That(response.Result, Does.Contain(expected));
    }

    [Test]
    public async Task AlternateTuningsSkill_ReadsFbAsE()
    {
        var response = await Tunings.ExecuteAsync("Fb A D G B E");

        Assert.That(response.Result, Does.Contain("| 6 | Fb | same |"));
    }

    [TestCase("parallel minor of F# major", "**F# minor**")]
    [TestCase("parallel minor of Gb major", "**Gb minor**")]
    [TestCase("parallel major of Bb minor", "**Bb major**")]
    public async Task RelativeKeySkill_AnswersParallelKeys(string prompt, string expected)
    {
        var response = await RelativeKey.ExecuteAsync(prompt);

        Assert.That(response.Result, Does.Contain(expected));
    }

    [TestCase("parallel minor of Fb major")]
    [TestCase("parallel minor of Cb major")]
    [TestCase("parallel minor of E# major")]
    [TestCase("parallel major of B# minor")]
    public async Task RelativeKeySkill_KeepsRefusingEdgeSpellingsForParallelKeys(string prompt)
    {
        var response = await RelativeKey.ExecuteAsync(prompt);

        Assert.That(response.Result, Does.StartWith("I couldn't identify"));
    }
}
