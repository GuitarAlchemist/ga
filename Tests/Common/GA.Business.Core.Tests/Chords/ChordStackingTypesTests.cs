namespace GA.Business.Core.Tests.Chords;

using Core.Chords;
using Core.Tonal.Modes.Diatonic;
using Core.Tonal.Primitives.Diatonic;
using Intervals.Chords;

/// <summary>
///     Tests for different chord stacking types (tertian, quartal, quintal)
/// </summary>
public class ChordStackingTypesTests
{
    [Test]
    public void TertianChord_ShouldStackThirds()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Create a tertian triad on the first degree (C major chord)
        var tertianChords = ChordTemplateFactory.CreateModalChords(ionianMode).ToList();
        var firstDegreeChord = tertianChords.First();

        // Assert
        Assert.That(firstDegreeChord.StackingType, Is.EqualTo(ChordStackingType.Tertian));
        Assert.That(firstDegreeChord.Formula.Intervals.Count, Is.EqualTo(2)); // 3rd, 5th (root is implicit)
        Assert.That(firstDegreeChord.Name, Does.Contain("Degree1"));
    }

    [Test]
    public void QuartalChord_ShouldStackFourths()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Use systematic generation to find quartal chords
        var quartalChords = ChordTemplateFactory.GetChordsByCharacteristics(
            stackingType: ChordStackingType.Quartal,
            extension: ChordExtension.Triad).Take(10).ToList();

        // Assert - Should find quartal chords from systematic generation
        Assert.That(quartalChords.Count, Is.GreaterThan(0), "Should find quartal chords from scale generation");

        var firstQuartalChord = quartalChords.First();
        Assert.That(firstQuartalChord.StackingType, Is.EqualTo(ChordStackingType.Quartal));
        Assert.That(firstQuartalChord.Extension, Is.EqualTo(ChordExtension.Triad));
        Assert.That(firstQuartalChord.Name, Does.Contain("(4ths)"));
    }

    [Test]
    public void QuintalChord_ShouldStackFifths()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Use systematic generation to find quintal chords
        var quintalChords = ChordTemplateFactory.GetChordsByCharacteristics(
            stackingType: ChordStackingType.Quintal,
            extension: ChordExtension.Triad).Take(10).ToList();

        // Assert - Should find quintal chords from systematic generation
        Assert.That(quintalChords.Count, Is.GreaterThan(0), "Should find quintal chords from scale generation");

        var firstQuintalChord = quintalChords.First();
        Assert.That(firstQuintalChord.StackingType, Is.EqualTo(ChordStackingType.Quintal));
        Assert.That(firstQuintalChord.Extension, Is.EqualTo(ChordExtension.Triad));
        Assert.That(firstQuintalChord.Name, Does.Contain("(5ths)"));
    }

    [Test]
    public void SystematicGeneration_ShouldGenerateQuartalChords()
    {
        // Test that systematic generation produces quartal chords from scale modes
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Generate all chords from this mode and filter for quartal triads
        var allChords = ChordTemplateFactory.GenerateFromScaleMode(ionianMode).ToList();
        var quartalTriads = allChords
            .Where(c => c.StackingType == ChordStackingType.Quartal && c.Extension == ChordExtension.Triad).ToList();


        // Assert - Should generate quartal chords for each scale degree (7 degrees in major scale)
        Assert.That(quartalTriads.Count, Is.EqualTo(7), "Should generate quartal triads from all 7 scale degrees");
        foreach (var chord in quartalTriads)
        {
            Assert.That(chord.StackingType, Is.EqualTo(ChordStackingType.Quartal));
            // Check that the chord has the correct extension
            Assert.That(chord.Extension, Is.EqualTo(ChordExtension.Triad));
        }
    }

    [Test]
    public void SystematicGeneration_ShouldGenerateQuintalChords()
    {
        // Test that systematic generation produces quintal chords from scale modes
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Generate all chords from this mode and filter for quintal triads
        var allChords = ChordTemplateFactory.GenerateFromScaleMode(ionianMode).ToList();
        var quintalTriads = allChords
            .Where(c => c.StackingType == ChordStackingType.Quintal && c.Extension == ChordExtension.Triad).ToList();

        // Assert - Should generate quintal chords for each scale degree
        Assert.That(quintalTriads.Count, Is.GreaterThan(0), "Should generate quintal triads from scale degrees");
        foreach (var chord in quintalTriads)
        {
            Assert.That(chord.StackingType, Is.EqualTo(ChordStackingType.Quintal));
            Assert.That(chord.Name, Does.Contain("(5ths)"));
        }
    }

    [Test]
    public void ChordStackingPatternGenerator_GetIntervalStepSize_ShouldReturnCorrectValues()
    {
        // Act & Assert
        Assert.That(ChordStackingPatternGenerator.GetIntervalStepSize(ChordStackingType.Tertian), Is.EqualTo(2));
        Assert.That(ChordStackingPatternGenerator.GetIntervalStepSize(ChordStackingType.Quartal), Is.EqualTo(3));
        Assert.That(ChordStackingPatternGenerator.GetIntervalStepSize(ChordStackingType.Quintal), Is.EqualTo(4));
    }

    [Test]
    public void ChordStackingPatternGenerator_GetStackingDescription_ShouldReturnDescriptiveText()
    {
        // Act & Assert
        Assert.That(ChordStackingPatternGenerator.GetStackingDescription(ChordStackingType.Tertian),
            Does.Contain("traditional"));
        Assert.That(ChordStackingPatternGenerator.GetStackingDescription(ChordStackingType.Quartal),
            Does.Contain("modern jazz"));
        Assert.That(ChordStackingPatternGenerator.GetStackingDescription(ChordStackingType.Quintal),
            Does.Contain("contemporary"));
    }

    [Test]
    public void ExtendedQuartalChord_ShouldGenerateCorrectIntervals()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Find extended quartal chords (7th) from systematic generation
        var quartalSevenths = ChordTemplateFactory.GetChordsByCharacteristics(
            stackingType: ChordStackingType.Quartal,
            extension: ChordExtension.Seventh).Take(5).ToList();

        // Assert - Should find quartal seventh chords
        Assert.That(quartalSevenths.Count, Is.GreaterThan(0), "Should find quartal seventh chords");
        var firstDegreeChord = quartalSevenths.First();
        Assert.That(firstDegreeChord.StackingType, Is.EqualTo(ChordStackingType.Quartal));
        Assert.That(firstDegreeChord.Extension, Is.EqualTo(ChordExtension.Seventh));
        // Verify the chord has 4 notes (root + 3 intervals for seventh chord)
        Assert.That(firstDegreeChord.NoteCount, Is.EqualTo(4), "Seventh chord should have 4 notes");
    }

    [Test]
    public void ExtendedQuintalChord_ShouldGenerateCorrectIntervals()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Find extended quintal chords (7th) from systematic generation
        var quintalSevenths = ChordTemplateFactory.GetChordsByCharacteristics(
            stackingType: ChordStackingType.Quintal,
            extension: ChordExtension.Seventh).Take(5).ToList();

        // Assert - Should find quintal seventh chords
        Assert.That(quintalSevenths.Count, Is.GreaterThan(0), "Should find quintal seventh chords");
        var firstDegreeChord = quintalSevenths.First();
        Assert.That(firstDegreeChord.StackingType, Is.EqualTo(ChordStackingType.Quintal));
        Assert.That(firstDegreeChord.Extension, Is.EqualTo(ChordExtension.Seventh));
        // Verify the chord has 4 notes (root + 3 intervals for seventh chord)
        Assert.That(firstDegreeChord.NoteCount, Is.EqualTo(4), "Seventh chord should have 4 notes");
    }
}
