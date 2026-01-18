namespace GA.Business.Core.Tests.Fretboard;

using Core.Fretboard.Positions;
using Core.Fretboard.Primitives;
using static Core.Fretboard.Primitives.Position;

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
        TestContext.WriteLine($"Muted Position on String {str}: Location={muted.Location}");
        Assert.Multiple(() =>
        {
            Assert.That(muted.Str, Is.EqualTo(str));
            Assert.That(muted.Location.Fret, Is.EqualTo(Fret.Muted));
            Assert.That(muted.Location.Str, Is.EqualTo(str));
        });
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
        TestContext.WriteLine($"Muted ToString: {result}");
        Assert.That(result, Is.EqualTo($"X{str}"));
    }

    [Test]
    public void Muted_Comparison_WorksCorrectly()
    {
        // Arrange
        var muted1 = new Muted(Str.Min);
        var muted2 = new Muted(Str.Min + 1);

        // Act
        var isLess = muted1 < muted2;
        var isGreater = muted2 > muted1;

        // Assert
        TestContext.WriteLine($"Muted1: {muted1}, Muted2: {muted2}, 1 < 2: {isLess}, 2 > 1: {isGreater}");
        Assert.Multiple(() =>
        {
            Assert.That(isLess, Is.True);
            Assert.That(isGreater, Is.True);
            Assert.That(muted1, Is.Not.EqualTo(muted2));
        });
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
        TestContext.WriteLine($"Played Position: Location={played.Location}, MIDI={played.MidiNote}");
        Assert.Multiple(() =>
        {
            Assert.That(played.Location, Is.EqualTo(location));
            // Don't compare directly with the integer since MidiNote might be a custom type
            Assert.That(played.MidiNote.ToString(), Is.EqualTo(midiNote.ToString()));
        });
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
        TestContext.WriteLine($"Played ToString: {result}");
        Assert.That(result, Is.EqualTo($"{location} {midiNote}"));
    }

    [Test]
    public void Played_Comparison_WorksCorrectly()
    {
        // Arrange
        var played1 = new Played(new PositionLocation(Str.Min, Fret.Open), 40);
        var played2 = new Played(new PositionLocation(Str.Min, Fret.FromValue(1)), 41);
        var played3 = new Played(new PositionLocation(Str.Min + 1, Fret.Open), 45);

        // Act
        var oneLessTwo = played1 < played2;
        var oneLessThree = played1 < played3;
        var twoLessThree = played2 < played3;

        // Assert
        TestContext.WriteLine($"Played1: {played1}, Played2: {played2}, Played3: {played3}");
        TestContext.WriteLine($"1 < 2: {oneLessTwo}, 1 < 3: {oneLessThree}, 2 < 3: {twoLessThree}");

        Assert.Multiple(() =>
        {
            Assert.That(oneLessTwo, Is.True);
            Assert.That(oneLessThree, Is.True);
            Assert.That(twoLessThree, Is.True);
            Assert.That(played3, Is.GreaterThan(played1));
            Assert.That(played1, Is.Not.EqualTo(played2));
        });
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

        // Act
        var areEqual = played1.Equals(played2);
        var hashesEqual = played1.GetHashCode() == played2.GetHashCode();
        var areDifferent = !played1.Equals(played3);

        // Assert
        TestContext.WriteLine($"Played1 vs Played2 Equality: {areEqual}, Hash Equality: {hashesEqual}");
        TestContext.WriteLine($"Played1 vs Played3 Equality: {!areDifferent}");

        Assert.Multiple(() =>
        {
            Assert.That(areEqual, Is.True);
            Assert.That(areDifferent, Is.True);
            Assert.That(hashesEqual, Is.True);
        });
    }
}
