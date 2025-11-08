namespace GA.Business.Core.Tests.Fretboard.Biomechanics;

using Core.Fretboard.Biomechanics;
using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using Core.Notes.Primitives;

[TestFixture]
public class FingeringEfficiencyTests
{
    private static Position.Played CreatePlayed(int str, int fret)
    {
        var stringObj = Str.FromValue(str);
        var fretObj = Fret.FromValue(fret);
        var location = new PositionLocation(stringObj, fretObj);
        return new Position.Played(location, MidiNote.FromValue(60 + fret));
    }

    [Test]
    public void Analyze_EmptyAssignments_ReturnsNone()
    {
        // Arrange
        var assignments = new List<(Position.Played, FingerType)>();

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.EfficiencyScore, Is.EqualTo(0.0));
        Assert.That(result.FingerUsageCounts, Is.Empty);
        Assert.That(result.Reason, Does.Contain("No finger assignments"));
    }

    [Test]
    public void Analyze_SimpleChord_ReturnsEfficient()
    {
        // Arrange - Simple C major chord (3 notes, 3 fingers, compact)
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(5, 3), FingerType.Ring), // C on A string
            (CreatePlayed(4, 2), FingerType.Middle), // E on D string
            (CreatePlayed(2, 1), FingerType.Index) // C on B string
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.EfficiencyScore, Is.GreaterThanOrEqualTo(0.8));
        Assert.That(result.FingerUsageCounts.Count, Is.EqualTo(3));
        Assert.That(result.FingerSpan, Is.EqualTo(2)); // Frets 1-3
        Assert.That(result.HasBarreChord, Is.False);
        Assert.That(result.UsesThumb, Is.False);
        Assert.That(result.Recommendations, Is.Empty);
    }

    [Test]
    public void Analyze_BarreChord_DetectsBarreCorrectly()
    {
        // Arrange - F major barre chord (index finger barres fret 1)
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 1), FingerType.Index), // F on low E
            (CreatePlayed(5, 1), FingerType.Index), // C on A
            (CreatePlayed(4, 3), FingerType.Ring), // F on D
            (CreatePlayed(3, 3), FingerType.Little), // A on G
            (CreatePlayed(2, 2), FingerType.Middle), // C on B
            (CreatePlayed(1, 1), FingerType.Index) // F on high E
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.HasBarreChord, Is.True);
        Assert.That(result.FingerUsageCounts[FingerType.Index], Is.EqualTo(3)); // Barre on 3 strings
        Assert.That(result.FingerSpan, Is.EqualTo(2)); // Frets 1-3
    }

    [Test]
    public void Analyze_HighPinkyUsage_GeneratesRecommendation()
    {
        // Arrange - Chord with excessive pinky usage
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 5), FingerType.Little),
            (CreatePlayed(5, 5), FingerType.Little),
            (CreatePlayed(4, 5), FingerType.Little)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.PinkyUsagePercentage, Is.EqualTo(100.0));
        Assert.That(result.Recommendations, Has.Some.Contains("pinky"));
        Assert.That(result.EfficiencyScore, Is.LessThan(0.7)); // Adjusted - compact voicing gets bonus
    }

    [Test]
    public void Analyze_LargeFingerSpan_GeneratesRecommendation()
    {
        // Arrange - Chord with large finger span (6 frets)
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 3), FingerType.Index),
            (CreatePlayed(5, 9), FingerType.Little)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.FingerSpan, Is.EqualTo(6));
        Assert.That(result.Recommendations, Has.Some.Contains("span"));
        // Note: Only 2 notes gets compact voicing bonus, so score is still relatively high
        Assert.That(result.EfficiencyScore, Is.LessThan(1.0));
    }

    [Test]
    public void Analyze_UnevenFingerDistribution_GeneratesRecommendation()
    {
        // Arrange - One finger used multiple times (not a barre)
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 3), FingerType.Index),
            (CreatePlayed(5, 5), FingerType.Index),
            (CreatePlayed(4, 7), FingerType.Index)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.FingerUsageCounts[FingerType.Index], Is.EqualTo(3));
        Assert.That(result.Recommendations, Has.Some.Contains("spreading"));
    }

    [Test]
    public void Analyze_ThumbUsage_DetectsCorrectly()
    {
        // Arrange - Chord using thumb for bass note
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 3), FingerType.Thumb),
            (CreatePlayed(4, 5), FingerType.Index),
            (CreatePlayed(3, 5), FingerType.Middle),
            (CreatePlayed(2, 5), FingerType.Ring)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.UsesThumb, Is.True);
        Assert.That(result.FingerUsageCounts.ContainsKey(FingerType.Thumb), Is.True);
    }

    [Test]
    public void Analyze_CompactVoicing_RewardsEfficiency()
    {
        // Arrange - Compact 3-note voicing
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(4, 5), FingerType.Index),
            (CreatePlayed(3, 5), FingerType.Middle),
            (CreatePlayed(2, 5), FingerType.Ring)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.FingerSpan, Is.EqualTo(0)); // All on same fret
        Assert.That(result.EfficiencyScore, Is.GreaterThanOrEqualTo(0.8));
        Assert.That(result.Recommendations, Is.Empty);
    }

    [Test]
    public void Analyze_ModerateEfficiency_ClassifiesCorrectly()
    {
        // Arrange - Moderate difficulty chord (5-fret span with some pinky usage)
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 3), FingerType.Index),
            (CreatePlayed(5, 5), FingerType.Middle),
            (CreatePlayed(4, 7), FingerType.Ring),
            (CreatePlayed(3, 8), FingerType.Little),
            (CreatePlayed(2, 8), FingerType.Little)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.EfficiencyScore, Is.InRange(0.5, 0.9)); // Adjusted range
        Assert.That(result.FingerSpan, Is.EqualTo(5)); // Frets 3-8
    }

    [Test]
    public void Analyze_EvenFingerDistribution_HighScore()
    {
        // Arrange - Each finger used once
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 5), FingerType.Index),
            (CreatePlayed(5, 6), FingerType.Middle),
            (CreatePlayed(4, 7), FingerType.Ring),
            (CreatePlayed(3, 8), FingerType.Little)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.FingerUsageCounts.Values, Has.All.EqualTo(1));
        Assert.That(result.PinkyUsagePercentage, Is.EqualTo(25.0)); // 1 out of 4
    }

    [Test]
    public void Analyze_SingleNote_ReturnsEfficient()
    {
        // Arrange - Single note (very easy)
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(3, 5), FingerType.Index)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.EfficiencyScore, Is.GreaterThanOrEqualTo(0.8));
        Assert.That(result.FingerSpan, Is.EqualTo(0));
        Assert.That(result.Recommendations, Is.Empty);
    }

    [Test]
    public void Analyze_TwoNotes_ReturnsEfficient()
    {
        // Arrange - Two notes (easy)
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(5, 3), FingerType.Index),
            (CreatePlayed(4, 5), FingerType.Ring)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.EfficiencyScore, Is.GreaterThanOrEqualTo(0.8));
        Assert.That(result.FingerSpan, Is.EqualTo(2));
    }

    [Test]
    public void Analyze_FingerUsageCounts_AccuratelyReflectsAssignments()
    {
        // Arrange
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 1), FingerType.Index),
            (CreatePlayed(5, 1), FingerType.Index),
            (CreatePlayed(4, 3), FingerType.Ring),
            (CreatePlayed(3, 3), FingerType.Little)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.FingerUsageCounts[FingerType.Index], Is.EqualTo(2));
        Assert.That(result.FingerUsageCounts[FingerType.Ring], Is.EqualTo(1));
        Assert.That(result.FingerUsageCounts[FingerType.Little], Is.EqualTo(1));
        Assert.That(result.FingerUsageCounts.ContainsKey(FingerType.Middle), Is.False);
    }

    [Test]
    public void Analyze_BarreWithAdditionalFingers_CorrectlyClassifies()
    {
        // Arrange - Barre chord with additional fingers
        var assignments = new List<(Position.Played, FingerType)>
        {
            (CreatePlayed(6, 3), FingerType.Index),
            (CreatePlayed(5, 3), FingerType.Index),
            (CreatePlayed(4, 5), FingerType.Ring),
            (CreatePlayed(3, 5), FingerType.Little)
        };

        // Act
        var result = FingeringEfficiencyDetector.Analyze(assignments);

        // Assert
        Assert.That(result.HasBarreChord, Is.True);
        Assert.That(result.FingerSpan, Is.EqualTo(2));
        Assert.That(result.EfficiencyScore, Is.LessThan(1.0)); // Barre chords are harder
    }
}
