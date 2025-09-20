namespace GA.Business.Core.Tests;

using GA.Business.Core.Chords;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;

[TestFixture]
public class ChordTemplateFactoryTests
{
    [Test]
    public void StandardChords_ShouldContainBasicTriads()
    {
        // Arrange & Act
        var standardChords = ChordTemplateFactory.StandardChords;

        // Assert
        Assert.That(standardChords.ContainsKey("Major"), Is.True);
        Assert.That(standardChords.ContainsKey("Minor"), Is.True);
        Assert.That(standardChords.ContainsKey("Diminished"), Is.True);
        Assert.That(standardChords.ContainsKey("Augmented"), Is.True);
    }

    [Test]
    public void StandardChords_ShouldContainSeventhChords()
    {
        // Arrange & Act
        var standardChords = ChordTemplateFactory.StandardChords;

        // Assert
        Assert.That(standardChords.ContainsKey("Major7"), Is.True);
        Assert.That(standardChords.ContainsKey("Minor7"), Is.True);
        Assert.That(standardChords.ContainsKey("Dominant7"), Is.True);
        Assert.That(standardChords.ContainsKey("Diminished7"), Is.True);
        Assert.That(standardChords.ContainsKey("HalfDiminished7"), Is.True);
    }

    [Test]
    public void FromSemitones_ShouldCreateValidChordTemplate()
    {
        // Arrange
        var name = "TestChord";
        var semitones = new[] { 4, 7 }; // Major triad

        // Act
        var template = ChordTemplateFactory.FromSemitones(name, semitones);

        // Assert
        Assert.That(template.Name, Is.EqualTo(name));
        Assert.That(template.NoteCount, Is.EqualTo(3)); // Root + 2 intervals
        Assert.That(template.Quality, Is.EqualTo(ChordQuality.Major));
    }

    [Test]
    public void CreateModalChords_ShouldGenerateCorrectNumberOfChords()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var modalChords = ChordTemplateFactory.CreateModalChords(ionianMode, ChordExtension.Triad).ToList();

        // Assert
        Assert.That(modalChords.Count, Is.EqualTo(7)); // 7 degrees in major scale
        Assert.That(modalChords.All(c => c.Extension == ChordExtension.Triad), Is.True);
    }

    [Test]
    public void CreateDiatonicChords_ShouldCreateSevenTriads()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var diatonicChords = ChordTemplateFactory.CreateDiatonicChords(ionianMode);

        // Assert
        Assert.That(diatonicChords.Count, Is.EqualTo(7));
        Assert.That(diatonicChords.All(c => c.Extension == ChordExtension.Triad), Is.True);
        Assert.That(diatonicChords.All(c => c.StackingType == ChordStackingType.Tertian), Is.True);
    }

    [Test]
    public void CreateDiatonicSevenths_ShouldCreateSevenSeventhChords()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var seventhChords = ChordTemplateFactory.CreateDiatonicSevenths(ionianMode);

        // Assert
        Assert.That(seventhChords.Count, Is.EqualTo(7));
        Assert.That(seventhChords.All(c => c.Extension == ChordExtension.Seventh), Is.True);
        Assert.That(seventhChords.All(c => c.StackingType == ChordStackingType.Tertian), Is.True);
    }

    [Test]
    public void CreateQuartalChords_ShouldUseQuartalStacking()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var quartalChords = ChordTemplateFactory.CreateQuartalChords(ionianMode).ToList();

        // Assert
        Assert.That(quartalChords.Count, Is.EqualTo(7));
        Assert.That(quartalChords.All(c => c.StackingType == ChordStackingType.Quartal), Is.True);
        Assert.That(quartalChords.All(c => c.Name.Contains("(4ths)")), Is.True);
    }

    [Test]
    public void CreateQuintalChords_ShouldUseQuintalStacking()
    {
        // Arrange
        var ionianMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);

        // Act
        var quintalChords = ChordTemplateFactory.CreateQuintalChords(ionianMode).ToList();

        // Assert
        Assert.That(quintalChords.Count, Is.EqualTo(7));
        Assert.That(quintalChords.All(c => c.StackingType == ChordStackingType.Quintal), Is.True);
        Assert.That(quintalChords.All(c => c.Name.Contains("(5ths)")), Is.True);
    }

    [Test]
    public void GetStandardChord_ShouldReturnCorrectChord()
    {
        // Act
        var majorChord = ChordTemplateFactory.GetStandardChord("Major");
        var minorChord = ChordTemplateFactory.GetStandardChord("Minor");

        // Assert
        Assert.That(majorChord, Is.Not.Null);
        Assert.That(majorChord!.Quality, Is.EqualTo(ChordQuality.Major));
        Assert.That(minorChord, Is.Not.Null);
        Assert.That(minorChord!.Quality, Is.EqualTo(ChordQuality.Minor));
    }

    [Test]
    public void GetStandardChord_WithInvalidName_ShouldReturnNull()
    {
        // Act
        var result = ChordTemplateFactory.GetStandardChord("NonExistentChord");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void StandardChordCount_ShouldReturnCorrectCount()
    {
        // Act
        var count = ChordTemplateFactory.StandardChordCount;

        // Assert
        Assert.That(count, Is.GreaterThan(30)); // Should have many standard chords
    }
}