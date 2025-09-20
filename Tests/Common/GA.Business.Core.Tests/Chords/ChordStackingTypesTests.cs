namespace GA.Business.Core.Tests.Chords;

using GA.Business.Core.Intervals;
using GA.Business.Core.Intervals.Chords;
using GA.Business.Core.Chords;
using GA.Business.Core.Tonal.Modes;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using GA.Business.Core.Atonal;

/// <summary>
/// Tests for different chord stacking types (tertian, quartal, quintal)
/// </summary>
public class ChordStackingTypesTests
{
    [Test]
    public void TertianChord_ShouldStackThirds()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Create a tertian triad on the first degree (C major chord)
        var tertianChord = ChordFormula.FromScaleMode(ionianMode, 1, ChordExtension.Triad, ChordStackingType.Tertian);

        // Assert
        Assert.That(tertianChord.StackingType, Is.EqualTo(ChordStackingType.Tertian));
        Assert.That(tertianChord.Intervals.Count, Is.EqualTo(3)); // Root, 3rd, 5th
        Assert.That(tertianChord.Name, Does.Contain("Degree1"));
    }

    [Test]
    public void QuartalChord_ShouldStackFourths()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Create a quartal triad on the first degree
        var quartalChord = ChordFormula.FromScaleMode(ionianMode, 1, ChordExtension.Triad, ChordStackingType.Quartal);

        // Assert
        Assert.That(quartalChord.StackingType, Is.EqualTo(ChordStackingType.Quartal));
        Assert.That(quartalChord.Intervals.Count, Is.EqualTo(3)); // Root, 4th, 7th
        Assert.That(quartalChord.Name, Does.Contain("(4ths)"));
    }

    [Test]
    public void QuintalChord_ShouldStackFifths()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Create a quintal triad on the first degree
        var quintalChord = ChordFormula.FromScaleMode(ionianMode, 1, ChordExtension.Triad, ChordStackingType.Quintal);

        // Assert
        Assert.That(quintalChord.StackingType, Is.EqualTo(ChordStackingType.Quintal));
        Assert.That(quintalChord.Intervals.Count, Is.EqualTo(3)); // Root, 5th, 2nd+octave
        Assert.That(quintalChord.Name, Does.Contain("(5ths)"));
    }

    [Test]
    public void ChordTemplateFactory_CreateQuartalChords_ShouldGenerateAllDegrees()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var quartalChords = ChordTemplateFactory.CreateQuartalChords(ionianMode, ChordExtension.Triad).ToList();

        // Assert
        Assert.That(quartalChords.Count, Is.EqualTo(7)); // 7 degrees in major scale
        foreach (var chord in quartalChords)
        {
            var modalFormula = chord.Formula as Modal;
            Assert.That(modalFormula, Is.Not.Null);
            Assert.That(modalFormula!.StackingType, Is.EqualTo(ChordStackingType.Quartal));
            Assert.That(chord.Formula.Name, Does.Contain("(4ths)"));
        }
    }

    [Test]
    public void ChordTemplateFactory_CreateQuintalChords_ShouldGenerateAllDegrees()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var quintalChords = ChordTemplateFactory.CreateQuintalChords(ionianMode, ChordExtension.Triad).ToList();

        // Assert
        Assert.That(quintalChords.Count, Is.EqualTo(7)); // 7 degrees in major scale
        foreach (var chord in quintalChords)
        {
            var modalFormula = chord.Formula as Modal;
            Assert.That(modalFormula, Is.Not.Null);
            Assert.That(modalFormula!.StackingType, Is.EqualTo(ChordStackingType.Quintal));
            Assert.That(chord.Formula.Name, Does.Contain("(5ths)"));
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
        Assert.That(ChordStackingPatternGenerator.GetStackingDescription(ChordStackingType.Tertian), Does.Contain("traditional"));
        Assert.That(ChordStackingPatternGenerator.GetStackingDescription(ChordStackingType.Quartal), Does.Contain("modern jazz"));
        Assert.That(ChordStackingPatternGenerator.GetStackingDescription(ChordStackingType.Quintal), Does.Contain("contemporary"));
    }

    [Test]
    public void ExtendedQuartalChord_ShouldGenerateCorrectIntervals()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Create an extended quartal chord (7th)
        var quartalSeventh = ChordFormula.FromScaleMode(ionianMode, 1, ChordExtension.Seventh, ChordStackingType.Quartal);

        // Assert
        Assert.That(quartalSeventh.Intervals.Count, Is.EqualTo(4)); // Root + 3 more fourths
        Assert.That(quartalSeventh.StackingType, Is.EqualTo(ChordStackingType.Quartal));
        Assert.That(quartalSeventh.Name, Does.Contain("7"));
        Assert.That(quartalSeventh.Name, Does.Contain("(4ths)"));
    }

    [Test]
    public void ExtendedQuintalChord_ShouldGenerateCorrectIntervals()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act - Create an extended quintal chord (7th)
        var quintalSeventh = ChordFormula.FromScaleMode(ionianMode, 1, ChordExtension.Seventh, ChordStackingType.Quintal);

        // Assert
        Assert.That(quintalSeventh.Intervals.Count, Is.EqualTo(4)); // Root + 3 more fifths
        Assert.That(quintalSeventh.StackingType, Is.EqualTo(ChordStackingType.Quintal));
        Assert.That(quintalSeventh.Name, Does.Contain("7"));
        Assert.That(quintalSeventh.Name, Does.Contain("(5ths)"));
    }
}
