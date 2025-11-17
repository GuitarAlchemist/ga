namespace GA.Business.Core.Tests.Chords;

using System.Linq;
using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using NUnit.Framework;

[TestFixture]
public class ChordTemplateFactoryTests
{
    [Test]
    public void GenerateAllPossibleChords_ProducesMajorMinorAndDiminishedTriads()
    {
        var templates = ChordTemplateFactory.GenerateAllPossibleChords().ToList();

        Assert.Multiple(() =>
        {
            Assert.That(templates, Is.Not.Empty, "Chord generation should yield at least one template.");
            Assert.That(
                templates.Any(t => MatchesPitchClasses(t, 0, 4, 7) && t.Quality == ChordQuality.Major),
                "Major triads should be produced.");
            Assert.That(
                templates.Any(t => MatchesPitchClasses(t, 0, 3, 7) && t.Quality == ChordQuality.Minor),
                "Minor triads should be produced.");
            Assert.That(
                templates.Any(t => MatchesPitchClasses(t, 0, 3, 6) && t.Quality == ChordQuality.Diminished),
                "Diminished triads should be produced.");
        });
    }

    [Test]
    public void GenerateAllPossibleChords_ReturnsNonEmptyCollection()
    {
        var templates = ChordTemplateFactory.GenerateAllPossibleChords();
        Assert.That(templates.Any(), Is.True, "Chord generator should not return an empty sequence.");
    }

    private static bool MatchesPitchClasses(ChordTemplate template, params int[] classes)
    {
        return classes.All(pc => template.PitchClassSet.Contains(PitchClass.FromValue(pc)));
    }
}
