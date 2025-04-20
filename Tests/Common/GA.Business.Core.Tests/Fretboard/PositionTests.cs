﻿﻿﻿namespace GA.Business.Core.Tests.Fretboard;

using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Positions;
using static GA.Business.Core.Fretboard.Primitives.Position;

[TestFixture]
public class PositionTests
{
    [Test]
    public void Muted_Constructor_CreatesValidPosition()
    {
        // Arrange
        var str = Str.Min;

        // Act
        var muted = new Muted(str);

        // Assert
        Assert.That(muted.Str, Is.EqualTo(str));
        Assert.That(muted.Location.Fret, Is.EqualTo(Fret.Muted));
        Assert.That(muted.Location.Str, Is.EqualTo(str));
    }

    [Test]
    public void Muted_ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var str = Str.Min;
        var muted = new Muted(str);

        // Act
        var result = muted.ToString();

        // Assert
        Assert.That(result, Is.EqualTo($"X{str}"));
    }

    [Test]
    public void Muted_Comparison_WorksCorrectly()
    {
        // Arrange
        var muted1 = new Muted(Str.Min);
        var muted2 = new Muted(Str.Min + 1);

        // Act & Assert
        Assert.That(muted1, Is.LessThan(muted2));
        Assert.That(muted2, Is.GreaterThan(muted1));
        Assert.That(muted1, Is.Not.EqualTo(muted2));
    }

    [Test]
    public void Played_Constructor_CreatesValidPosition()
    {
        // Arrange
        var location = new PositionLocation(Str.Min, Fret.Open);
        var midiNote = 40; // Example MIDI note

        // Act
        var played = new Played(location, midiNote);

        // Assert
        Assert.That(played.Location, Is.EqualTo(location));
        // Don't compare directly with the integer since MidiNote might be a custom type
        Assert.That(played.MidiNote.ToString(), Is.EqualTo(midiNote.ToString()));
    }

    [Test]
    public void Played_ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var location = new PositionLocation(Str.Min, Fret.Open);
        var midiNote = 40; // Example MIDI note
        var played = new Played(location, midiNote);

        // Act
        var result = played.ToString();

        // Assert
        Assert.That(result, Is.EqualTo($"{location} {midiNote}"));
    }

    [Test]
    public void Played_Comparison_WorksCorrectly()
    {
        // Arrange
        var played1 = new Played(new PositionLocation(Str.Min, Fret.Open), 40);
        var played2 = new Played(new PositionLocation(Str.Min, Fret.FromValue(1)), 41);
        var played3 = new Played(new PositionLocation(Str.Min + 1, Fret.Open), 45);

        // Act & Assert
        Assert.That(played1, Is.LessThan(played2));
        Assert.That(played1, Is.LessThan(played3));
        Assert.That(played2, Is.LessThan(played3));
        Assert.That(played3, Is.GreaterThan(played1));
        Assert.That(played1, Is.Not.EqualTo(played2));
    }

    [Test]
    public void Played_Equality_WorksCorrectly()
    {
        // Arrange
        var location = new PositionLocation(Str.Min, Fret.Open);
        var midiNote = 40;
        var played1 = new Played(location, midiNote);
        var played2 = new Played(location, midiNote);
        var differentLocation = new PositionLocation(Str.Min + 1, Fret.Open);
        var played3 = new Played(differentLocation, midiNote); // Different location

        // Act & Assert
        Assert.That(played1, Is.EqualTo(played2));
        Assert.That(played1, Is.Not.EqualTo(played3));
        Assert.That(played1.GetHashCode(), Is.EqualTo(played2.GetHashCode()));
    }
}
