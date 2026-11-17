namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Skills;
using GA.Domain.Services.Atonal.Grothendieck;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
///     A chord quality named with an article ("a major triad") is a chord type, not the note A
///     (issue #888). The interval-class vector does not depend on the root, so the skill answers on C.
/// </summary>
[TestFixture]
public class IntervalClassVectorSkillTests
{
    private static readonly IntervalClassVectorSkill Skill =
        new(new GrothendieckService(), NullLogger<IntervalClassVectorSkill>.Instance);

    [TestCase("What is the interval class vector of a major triad?", "a major triad", "{0, 4, 7}", "<0 0 1 1 1 0>")]
    [TestCase("ICV of an augmented triad", "an augmented triad", "{0, 4, 8}", "<0 0 0 3 0 0>")]
    [TestCase("interval-class vector of a minor triad", "a minor triad", "{0, 3, 7}", "<0 0 1 1 1 0>")]
    [TestCase("what is the ICV of a dominant seventh chord", "a dominant seventh chord", "{0, 4, 7, 10}", "<0 1 2 1 1 1>")]
    [TestCase("ICV of a diminished seventh chord", "a diminished seventh chord", "{0, 3, 6, 9}", "<0 0 4 0 0 2>")]
    [TestCase("ICV of a suspended chord", "a suspended chord", "{0, 5, 7}", "<0 1 0 0 2 0>")]
    [TestCase("ICV of a suspended seventh chord", "a suspended seventh chord", "{0, 5, 7, 10}", "<0 2 1 0 3 0>")]
    [TestCase("ICV of a half-diminished seventh chord", "a half-diminished seventh chord", "{0, 3, 6, 10}", "<0 1 2 1 1 1>")]
    public async Task ChordQualityWithArticle_IsReadAsAChordType(string prompt, string label, string pcs, string icv)
    {
        var response = await Skill.ExecuteAsync(prompt);

        Assert.Multiple(() =>
        {
            Assert.That(response.Result, Does.StartWith($"**ICV of {label}**: `{icv}`"));
            Assert.That(response.Result, Does.Contain($"Pitch-class set: `{pcs}`"));
        });
    }

    [Test]
    public async Task LoneArticle_IsNotReadAsTheNoteA()
    {
        var response = await Skill.ExecuteAsync("what is the interval class vector of a chord I just made up");

        Assert.That(response.Result, Does.Not.Contain("ICV of a**"));
    }

    [TestCase("interval class vector of A", "**ICV of A**")]
    [TestCase("ICV of Am", "**ICV of Am**")]
    [TestCase("what is the interval-class vector of Cmaj7", "**ICV of Cmaj7**")]
    public async Task ChordSymbol_StillReadsItsRoot(string prompt, string expected)
    {
        var response = await Skill.ExecuteAsync(prompt);

        Assert.That(response.Result, Does.StartWith(expected));
    }
}
