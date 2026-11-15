namespace GA.Business.Core.Tests.Chords;

using Domain.Core.Theory.Atonal;
using Domain.Core.Theory.Harmony;
using Domain.Services.Chords;

[TestFixture]
public class ChordNamingTests
{
    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectSeventhChordNames()
    {
        // Act
        // Test major 7th
        var maj7Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Seventh);
        // Test minor 7th
        var min7Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Minor, ChordExtension.Seventh);
        // Test dominant 7th
        var dom7Name = BasicChordExtensionsService.GetExtensionNotation(
            ChordExtension.Seventh, ChordQuality.Major);
        // Assert
        TestContext.WriteLine($"Maj7: Expected=Cmaj7, Actual={maj7Name}");
        TestContext.WriteLine($"Min7: Expected=Cm7, Actual={min7Name}");
        TestContext.WriteLine(
            $"Dom7 Extension: Expected=maj7, Actual={dom7Name} (Legacy dominant 7th extension mapping)");
        Assert.Multiple(() =>
        {
            Assert.That(maj7Name, Is.EqualTo("Cmaj7"), "Major 7th chord should be named with 'maj7' suffix.");
            Assert.That(min7Name, Is.EqualTo("Cm7"), "Minor 7th chord should be named with 'm7' suffix.");
            Assert.That(dom7Name, Is.EqualTo("maj7"), "Internal extension notation for dominant should be 'maj7'.");
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectNinthChordNames()
    {
        // Act
        // Test major 9th
        var maj9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Ninth);
        // Test minor 9th
        var min9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Minor, ChordExtension.Ninth);
        // Assert
        TestContext.WriteLine($"Maj9: {maj9Name}, Min9: {min9Name}");
        Assert.Multiple(() =>
        {
            Assert.That(maj9Name, Is.EqualTo("Cmaj9"));
            Assert.That(min9Name, Is.EqualTo("Cm9"));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectSuspendedChordNames()
    {
        // Act
        // Test sus4
        var sus4Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Suspended, ChordExtension.Sus4);
        // Test sus2
        var sus2Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Suspended, ChordExtension.Sus2);
        // Assert
        TestContext.WriteLine($"Sus4: {sus4Name}, Sus2: {sus2Name}");
        Assert.Multiple(() =>
        {
            Assert.That(sus4Name, Is.EqualTo("Csus4"));
            Assert.That(sus2Name, Is.EqualTo("Csus2"));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldGenerateCorrectAddedToneChordNames()
    {
        // Act
        // Test add9
        var add9Name = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.Add9);
        // Test 6/9
        var sixNineName = BasicChordExtensionsService.GenerateChordName(
            PitchClass.C, ChordQuality.Major, ChordExtension.SixNine);
        // Assert
        TestContext.WriteLine($"Add9: {add9Name}, 6/9: {sixNineName}");
        Assert.Multiple(() =>
        {
            Assert.That(add9Name, Is.EqualTo("Cadd9"));
            Assert.That(sixNineName, Is.EqualTo("C6/9"));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldDetectHighestExtension()
    {
        // Arrange
        var thirteenthIntervals = new[] { 3, 7, 10, 2, 5, 9 }; // 3rd, 5th, 7th, 9th, 11th, 13th
        var ninthIntervals = new[] { 3, 7, 10, 2 }; // 3rd, 5th, 7th, 9th
        var triadIntervals = new[] { 3, 7 }; // 3rd, 5th
        // Act
        var highestExtension = BasicChordExtensionsService.GetHighestExtension(thirteenthIntervals);
        var ninthExtension = BasicChordExtensionsService.GetHighestExtension(ninthIntervals);
        var triadExtension = BasicChordExtensionsService.GetHighestExtension(triadIntervals);
        // Assert
        TestContext.WriteLine($"13th Intervals Extension: {highestExtension}");
        TestContext.WriteLine($"9th Intervals Extension: {ninthExtension}");
        TestContext.WriteLine($"Triad Intervals Extension: {triadExtension}");
        Assert.Multiple(() =>
        {
            Assert.That(highestExtension, Is.EqualTo(ChordExtension.Thirteenth));
            Assert.That(ninthExtension, Is.EqualTo(ChordExtension.Ninth));
            Assert.That(triadExtension, Is.EqualTo(ChordExtension.Triad));
        });
    }

    [Test]
    public void BasicChordExtensionsService_ShouldValidateExtensions()
    {
        // Arrange
        // 9th chord should require 7th
        var ninthWithSeventh = new[] { 3, 7, 10, 2 };
        var ninthWithoutSeventh = new[] { 3, 7, 2 };
        // Act
        var isValidNinth = BasicChordExtensionsService.IsValidExtension(ChordExtension.Ninth, ninthWithSeventh);
        var isInvalidNinth = BasicChordExtensionsService.IsValidExtension(ChordExtension.Ninth, ninthWithoutSeventh);
        // Assert
        TestContext.WriteLine($"9th with 7th Valid: {isValidNinth}, 9th without 7th Valid: {isInvalidNinth}");
        Assert.Multiple(() =>
        {
            Assert.That(isValidNinth, Is.True);
            Assert.That(isInvalidNinth, Is.False);
        });
    }
}
