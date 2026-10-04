namespace GA.Business.Core.Tests.Chords;

using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Services.Chords;
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
    // The full generation ran its three scale groups twice. A natural minor mode's template differs from the major
    // mode's with the same name only by the mode's parent scale (A B C D E F G instead of C D E F G A B)
    [Test]
    public void GenerateAllPossibleChords_YieldsEachTemplateOnce()
    {
        var templates = ChordTemplateFactory.GenerateAllPossibleChords()
            .Select(t => (t.GetParentScale()?.ParentScale.ToString(), t.GetParentScale()?.Name, t.GetScaleDegree(), t.StackingType, t.Extension, t.Name, t.PitchClassSet.Id))
            .ToList();

        Assert.That(templates.Count, Is.EqualTo(templates.Distinct().Count()));
    }

    [Test]
    public void GenerateAllPossibleChords_ReturnsNonEmptyCollection()
    {
        var templates = ChordTemplateFactory.GenerateAllPossibleChords();
        Assert.That(templates.Any(), Is.True, "Chord generator should not return an empty sequence.");
    }
    private static bool MatchesPitchClasses(ChordTemplate template, params int[] classes) => classes.All(pc => template.PitchClassSet.Contains(PitchClass.FromValue(pc)));
}