namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using Core.Fretboard.Biomechanics;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Notes.Primitives;

[TestFixture]
public class SlideLegatoTests
{
    private static Position.Played CreatePlayed(int str, int fret)
    {
        var stringObj = Str.FromValue(str);
        var fretObj = Fret.FromValue(fret);
        var location = new PositionLocation(stringObj, fretObj);
        return new Position.Played(location, MidiNote.FromValue(60 + fret));
    }

    [Test]
    public void AnalyzeTransition_NoCommonStrings_ReturnsNone()
    {
        // Arrange
        var from = new List<Position.Played>
        {
            CreatePlayed(1, 3),
            CreatePlayed(2, 3)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(5, 5),
            CreatePlayed(6, 5)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.None));
        Assert.That(result.RequiresSlide, Is.False);
        Assert.That(result.RequiresHammerOn, Is.False);
        Assert.That(result.RequiresPullOff, Is.False);
    }

    [Test]
    public void AnalyzeTransition_SameFrets_ReturnsNone()
    {
        // Arrange
        var from = new List<Position.Played>
        {
            CreatePlayed(1, 5),
            CreatePlayed(2, 5)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(1, 5),
            CreatePlayed(2, 5)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.None));
    }

    [Test]
    public void AnalyzeTransition_LargeDistance_RecommendsSlide()
    {
        // Arrange - Slide from fret 3 to fret 7 (4 frets)
        var from = new List<Position.Played>
        {
            CreatePlayed(3, 3)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(3, 7)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.Slide));
        Assert.That(result.RequiresSlide, Is.True);
        Assert.That(result.RequiresHammerOn, Is.False);
        Assert.That(result.RequiresPullOff, Is.False);
        Assert.That(result.AffectedStrings, Contains.Item(3));
        Assert.That(result.FretDistance, Is.EqualTo(4));
        Assert.That(result.Confidence, Is.GreaterThan(0.7));
        Assert.That(result.Reason, Does.Contain("Slide"));
        Assert.That(result.Reason, Does.Contain("string 3"));
    }

    [Test]
    public void AnalyzeTransition_SmallDistanceUp_RecommendsHammerOn()
    {
        // Arrange - Hammer-on from fret 5 to fret 7 (2 frets)
        var from = new List<Position.Played>
        {
            CreatePlayed(2, 5)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(2, 7)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.HammerOn));
        Assert.That(result.RequiresSlide, Is.False);
        Assert.That(result.RequiresHammerOn, Is.True);
        Assert.That(result.RequiresPullOff, Is.False);
        Assert.That(result.AffectedStrings, Contains.Item(2));
        Assert.That(result.FretDistance, Is.EqualTo(2));
        Assert.That(result.Confidence, Is.GreaterThan(0.7));
        Assert.That(result.Reason, Does.Contain("Hammer-on"));
    }

    [Test]
    public void AnalyzeTransition_SmallDistanceDown_RecommendsPullOff()
    {
        // Arrange - Pull-off from fret 7 to fret 5 (2 frets)
        var from = new List<Position.Played>
        {
            CreatePlayed(1, 7)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(1, 5)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.PullOff));
        Assert.That(result.RequiresSlide, Is.False);
        Assert.That(result.RequiresHammerOn, Is.False);
        Assert.That(result.RequiresPullOff, Is.True);
        Assert.That(result.AffectedStrings, Contains.Item(1));
        Assert.That(result.FretDistance, Is.EqualTo(2));
        Assert.That(result.Confidence, Is.GreaterThan(0.7));
        Assert.That(result.Reason, Does.Contain("Pull-off"));
    }

    [Test]
    public void AnalyzeTransition_MultipleStringsSlide_HighConfidence()
    {
        // Arrange - Slide on 2 strings from fret 3 to fret 7
        var from = new List<Position.Played>
        {
            CreatePlayed(2, 3),
            CreatePlayed(3, 3)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(2, 7),
            CreatePlayed(3, 7)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.Slide));
        Assert.That(result.AffectedStrings, Has.Count.EqualTo(2));
        Assert.That(result.AffectedStrings, Contains.Item(2));
        Assert.That(result.AffectedStrings, Contains.Item(3));
        Assert.That(result.FretDistance, Is.EqualTo(4));
        Assert.That(result.Confidence, Is.GreaterThanOrEqualTo(0.8));
    }

    [Test]
    public void AnalyzeTransition_CombinedTechniques_ReturnsCombined()
    {
        // Arrange - Slide on one string, hammer-on on another
        var from = new List<Position.Played>
        {
            CreatePlayed(1, 3), // Will slide to 7 (4 frets)
            CreatePlayed(2, 5) // Will hammer-on to 7 (2 frets)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(1, 7),
            CreatePlayed(2, 7)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.Combined));
        Assert.That(result.RequiresSlide, Is.True);
        Assert.That(result.RequiresHammerOn, Is.True);
        Assert.That(result.AffectedStrings, Has.Count.EqualTo(2));
        Assert.That(result.Reason, Does.Contain("Combined"));
    }

    [Test]
    public void AnalyzeTransition_EmptyFromPositions_ReturnsNone()
    {
        // Arrange
        var from = new List<Position.Played>();
        var to = new List<Position.Played>
        {
            CreatePlayed(1, 5)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.None));
    }

    [Test]
    public void AnalyzeTransition_EmptyToPositions_ReturnsNone()
    {
        // Arrange
        var from = new List<Position.Played>
        {
            CreatePlayed(1, 5)
        };
        var to = new List<Position.Played>();

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.None));
    }

    [Test]
    public void AnalyzeTransition_OneFretDistance_RecommendsHammerOn()
    {
        // Arrange - 1 fret hammer-on
        var from = new List<Position.Played>
        {
            CreatePlayed(3, 5)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(3, 6)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.HammerOn));
        Assert.That(result.FretDistance, Is.EqualTo(1));
    }

    [Test]
    public void AnalyzeTransition_ThreeFretDistance_RecommendsSlide()
    {
        // Arrange - 3 fret slide (boundary case)
        var from = new List<Position.Played>
        {
            CreatePlayed(4, 5)
        };

        var to = new List<Position.Played>
        {
            CreatePlayed(4, 8)
        };

        // Act
        var result = SlideLegatoDetector.AnalyzeTransition(from, to);

        // Assert
        Assert.That(result.Technique, Is.EqualTo(SlideLegatoTechnique.Slide));
        Assert.That(result.FretDistance, Is.EqualTo(3));
    }
}
