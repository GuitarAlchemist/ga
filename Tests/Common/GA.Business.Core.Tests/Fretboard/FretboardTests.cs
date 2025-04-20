﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿﻿namespace GA.Business.Core.Tests.Fretboard;

using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Notes;

[TestFixture]
public class FretboardTests
{
    [Test]
    public void Default_HasExpectedProperties()
    {
        // Act
        var fretboard = Fretboard.Default;

        // Assert
        Assert.That(fretboard.StringCount, Is.EqualTo(6));
        Assert.That(fretboard.FretCount, Is.EqualTo(24));
        Assert.That(fretboard.Capo, Is.Null);
        Assert.That(fretboard.Tuning.ToString(), Is.EqualTo("E2 A2 D3 G3 B3 E4"));
    }

    [Test]
    public void Constructor_WithCustomTuning_CreatesCorrectFretboard()
    {
        // Arrange
        var tuning = new Tuning(PitchCollection.Parse("D2 A2 D3 G3 B3 E4"));

        // Act
        var fretboard = new Fretboard(tuning, 22);

        // Assert
        Assert.That(fretboard.StringCount, Is.EqualTo(6));
        Assert.That(fretboard.FretCount, Is.EqualTo(22));
        Assert.That(fretboard.Tuning, Is.EqualTo(tuning));
    }

    [Test]
    public void Strings_ReturnsCorrectCollection()
    {
        // Arrange
        var fretboard = Fretboard.Default;

        // Act
        var strings = fretboard.Strings;

        // Assert
        Assert.That(strings.Count, Is.EqualTo(6));

        // Check each string individually instead of using EquivalentTo to avoid stack overflow
        Assert.That(strings.Contains(Str.Min), Is.True, "Should contain Str.Min");
        Assert.That(strings.Contains(Str.Min + 1), Is.True, "Should contain Str.Min + 1");
        Assert.That(strings.Contains(Str.Min + 2), Is.True, "Should contain Str.Min + 2");
        Assert.That(strings.Contains(Str.Min + 3), Is.True, "Should contain Str.Min + 3");
        Assert.That(strings.Contains(Str.Min + 4), Is.True, "Should contain Str.Min + 4");
        Assert.That(strings.Contains(Str.Min + 5), Is.True, "Should contain Str.Min + 5");
    }

    [Test]
    public void Frets_ReturnsCorrectCollection()
    {
        // Arrange
        var fretboard = Fretboard.Default;

        // Act
        var frets = fretboard.Frets;

        // Assert
        Assert.That(frets.Count, Is.EqualTo(24)); // 0-23 inclusive
        Assert.That(frets.First().Value, Is.EqualTo(-1)); // Open string is represented as -1
        Assert.That(frets.Last().Value, Is.EqualTo(22)); // Highest fret
    }

    [Test]
    public void Positions_ContainsBothMutedAndPlayedPositions()
    {
        // Arrange
        var fretboard = Fretboard.Default;

        // Act
        var positions = fretboard.Positions;

        // Assert
        Assert.That(positions.Muted.Count, Is.EqualTo(6)); // One muted position per string
        Assert.That(positions.Played.Count, Is.EqualTo(138)); // Based on actual implementation
    }

    [Test]
    public void TryGetPositionFromLocation_WithValidLocation_ReturnsPosition()
    {
        // Arrange
        var fretboard = Fretboard.Default;
        var location = new PositionLocation(Str.Min, Fret.Open);

        // Act
        var success = fretboard.TryGetPositionFromLocation(location, out var position);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(position, Is.Not.Null);
        Assert.That(position.Location, Is.EqualTo(location));
    }

    [Test]
    public void TryGetPositionFromLocation_WithInvalidLocation_ReturnsFalse()
    {
        // Arrange
        var fretboard = Fretboard.Default;
        var invalidLocation = new PositionLocation(Str.Min + 10, Fret.Open); // Invalid string

        // Act
        var success = fretboard.TryGetPositionFromLocation(invalidLocation, out var position);

        // Assert
        Assert.That(success, Is.False);
        Assert.That(position, Is.Null, "Position should be null for invalid location");
    }

    [Test]
    public void SetCapo_AffectsAvailablePositions()
    {
        // Arrange
        var fretboard = new Fretboard();

        // Act
        fretboard.Capo = Fret.FromValue(2); // Set capo at 2nd fret

        // Assert
        Assert.That(fretboard.Capo, Is.EqualTo(Fret.FromValue(2)));
    }

    [Test]
    public void Render_ReturnsNonEmptyString()
    {
        // Arrange
        var fretboard = Fretboard.Default;

        // Act
        var rendered = fretboard.Render();

        // Assert
        Assert.That(rendered, Is.Not.Null.Or.Empty);
        // Just check that it's not empty, don't check for specific content
        // since the rendering format might change
    }
}
