namespace GA.Domain.Core.Tests.Theory.Harmony;

using GA.Domain.Core.Primitives;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Theory.Harmony;
using NUnit.Framework;

[TestFixture]
public class ChordTests
{
    [Test]
    public void Constructor_WithRootAndFormula_ShouldCreateCorrectChord()
    {
        // Arrange
        var root = new Note.Accidented(NaturalNote.C, Accidental.Natural);
        var formula = ChordFormula.Major;

        // Act
        var chord = new Chord(root, formula);

        // Assert
        Assert.That(chord.Root, Is.EqualTo(root));
        Assert.That(chord.Formula, Is.EqualTo(formula));
        Assert.That(chord.Quality, Is.EqualTo(ChordQuality.Major));
        Assert.That(chord.Extension, Is.EqualTo(ChordExtension.Triad));
        Assert.That(chord.Notes.Count, Is.EqualTo(3));
        Assert.That(chord.Notes[0].PitchClass, Is.EqualTo(PitchClass.C));
        Assert.That(chord.Notes[1].PitchClass, Is.EqualTo(PitchClass.E));
        Assert.That(chord.Notes[2].PitchClass, Is.EqualTo(PitchClass.G));
        Assert.That(chord.Symbol, Is.EqualTo("C"));
    }

    [Test]
    public void Constructor_WithNotes_ShouldAnalyzeCorrectly()
    {
        // Arrange
        var notes = new AccidentedNoteCollection(
        [
            new Note.Accidented(NaturalNote.C, Accidental.Natural),
            new Note.Accidented(NaturalNote.E, Accidental.Flat),
            new Note.Accidented(NaturalNote.G, Accidental.Natural)
        ]);

        // Act
        var chord = new Chord(notes);

        // Assert
        Assert.That(chord.Root.PitchClass, Is.EqualTo(PitchClass.C));
        Assert.That(chord.Quality, Is.EqualTo(ChordQuality.Minor));
        Assert.That(chord.Extension, Is.EqualTo(ChordExtension.Triad));
        Assert.That(chord.Symbol, Is.EqualTo("Cm"));
    }

    [TestCase("C", ChordQuality.Major)]
    [TestCase("Cm", ChordQuality.Minor)]
    [TestCase("Cdim", ChordQuality.Diminished)]
    [TestCase("Caug", ChordQuality.Augmented)]
    public void FromSymbol_ShouldCreateCorrectQuality(string symbol, ChordQuality expectedQuality)
    {
        // Act
        var chord = Chord.FromSymbol(symbol);

        // Assert
        Assert.That(chord.Quality, Is.EqualTo(expectedQuality));
    }

    [TestCase("C7", ChordExtension.Seventh)]
    [TestCase("Cmaj7", ChordExtension.Seventh)]
    [TestCase("C9", ChordExtension.Ninth)]
    [TestCase("C11", ChordExtension.Eleventh)]
    [TestCase("C13", ChordExtension.Thirteenth)]
    public void FromSymbol_ShouldCreateCorrectExtension(string symbol, ChordExtension expectedExtension)
    {
        // Act
        var chord = Chord.FromSymbol(symbol);

        // Assert
        Assert.That(chord.Extension, Is.EqualTo(expectedExtension));
    }

    [Test]
    public void Inversions_ShouldWorkCorrectly()
    {
        // Arrange
        var root = new Note.Accidented(NaturalNote.C, Accidental.Natural);
        var chord = new Chord(root, ChordFormula.Major); // C E G

        // Act
        var firstInversion = chord.ToInversion(1); // E G C
        var secondInversion = chord.ToInversion(2); // G C E

        // Assert
        Assert.That(firstInversion.IsInverted, Is.True);
        Assert.That(firstInversion.Bass.PitchClass, Is.EqualTo(PitchClass.E));
        
        Assert.That(secondInversion.IsInverted, Is.True);
        Assert.That(secondInversion.Bass.PitchClass, Is.EqualTo(PitchClass.G));

        Assert.That(firstInversion.GetInversion(), Is.EqualTo(1));
        Assert.That(secondInversion.GetInversion(), Is.EqualTo(2));
    }

    [Test]
    public void Equals_ShouldReturnTrueForSameChord()
    {
        // Arrange
        var chord1 = new Chord(new Note.Accidented(NaturalNote.C, Accidental.Natural), ChordFormula.Major);
        var chord2 = new Chord(new Note.Accidented(NaturalNote.C, Accidental.Natural), ChordFormula.Major);

        // Assert
        Assert.That(chord1, Is.EqualTo(chord2));
    }
}
