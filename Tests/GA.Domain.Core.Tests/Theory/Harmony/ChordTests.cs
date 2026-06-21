namespace GA.Domain.Core.Tests.Theory.Harmony;

using System.Linq;
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
        Assert.That(Chord.FromSymbol(symbol).Quality, Is.EqualTo(expectedQuality));
    }

    [TestCase("C7", ChordExtension.Seventh)]
    [TestCase("Cmaj7", ChordExtension.Seventh)]
    [TestCase("C9", ChordExtension.Ninth)]
    [TestCase("C11", ChordExtension.Eleventh)]
    [TestCase("C13", ChordExtension.Thirteenth)]
    public void FromSymbol_ShouldCreateCorrectExtension(string symbol, ChordExtension expectedExtension)
    {
        Assert.That(Chord.FromSymbol(symbol).Extension, Is.EqualTo(expectedExtension));
    }

    [Test]
    public void FromSymbol_ParsesRootWithAccidental()
    {
        var chord = Chord.FromSymbol("F#m7");
        Assert.Multiple(() =>
        {
            Assert.That(chord.Root.PitchClass.Value, Is.EqualTo(6)); // F#
            Assert.That(chord.Quality, Is.EqualTo(ChordQuality.Minor));
            Assert.That(chord.Extension, Is.EqualTo(ChordExtension.Seventh));
        });
    }

    [Test]
    public void TryFromSymbol_InvalidSymbol_ReturnsFalse()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Chord.TryFromSymbol("H7", out _), Is.False);   // H is not a note letter
            Assert.That(Chord.TryFromSymbol("Cwobble", out _), Is.False); // unknown suffix
            Assert.That(Chord.TryFromSymbol("C", out var c), Is.True);
            Assert.That(c!.Quality, Is.EqualTo(ChordQuality.Major));
        });
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

    private static Chord CChord(ChordFormula formula) =>
        new(new Note.Accidented(NaturalNote.C, Accidental.Natural), formula);

    private static int[] PitchClassValues(Chord chord) =>
        chord.PitchClassSet.Select(pc => pc.Value).OrderBy(v => v).ToArray();

    [Test]
    public void MajorTriad_HasRootMajorThirdPerfectFifth()
    {
        // C major triad = {C, E, G} = {0, 4, 7}.
        Assert.That(PitchClassValues(CChord(ChordFormula.Major)), Is.EqualTo(new[] { 0, 4, 7 }));
    }

    [Test]
    public void Dominant7_HasExpectedPitchClasses()
    {
        // C7 = {C, E, G, Bb} = {0, 4, 7, 10}.
        var chord = CChord(ChordFormula.Dominant7);
        Assert.Multiple(() =>
        {
            Assert.That(chord.Notes.Count, Is.EqualTo(4));
            Assert.That(PitchClassValues(chord), Is.EqualTo(new[] { 0, 4, 7, 10 }));
            Assert.That(chord.Extension, Is.EqualTo(ChordExtension.Seventh));
        });
    }

    [Test]
    public void Dominant7_FormulaQualityIsDominant_ButChordQualityFallsBackToMajor()
    {
        // Characterizes a real divergence: ChordFormula.DetermineQuality knows "Dominant"
        // (major 3rd + minor 7th), but Chord.DetermineQuality only classifies the triad
        // (3rd + 5th) and therefore reports Major for a dominant-7th chord. Pinned here so
        // the inconsistency is visible and a future fix would deliberately update this test.
        var chord = CChord(ChordFormula.Dominant7);
        Assert.Multiple(() =>
        {
            Assert.That(chord.Formula.Quality, Is.EqualTo(ChordQuality.Dominant));
            Assert.That(chord.Quality, Is.EqualTo(ChordQuality.Major));
        });
    }

    [TestCaseSource(nameof(SeventhChordCases))]
    public void SeventhChords_ClassifyAsSeventhExtension(ChordFormula formula)
    {
        Assert.That(CChord(formula).Extension, Is.EqualTo(ChordExtension.Seventh));
    }

    public static IEnumerable<TestCaseData> SeventhChordCases
    {
        get
        {
            yield return new TestCaseData(ChordFormula.Dominant7).SetName("Dominant7");
            yield return new TestCaseData(ChordFormula.Major7).SetName("Major7");
            yield return new TestCaseData(ChordFormula.Minor7).SetName("Minor7");
        }
    }

    [Test]
    public void Formula_Quality_IsClassifiedFromIntervals()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChordFormula.Major.Quality, Is.EqualTo(ChordQuality.Major));
            Assert.That(ChordFormula.Minor.Quality, Is.EqualTo(ChordQuality.Minor));
            Assert.That(ChordFormula.Diminished.Quality, Is.EqualTo(ChordQuality.Diminished));
            Assert.That(ChordFormula.Augmented.Quality, Is.EqualTo(ChordQuality.Augmented));
            Assert.That(ChordFormula.Dominant7.Quality, Is.EqualTo(ChordQuality.Dominant));
            Assert.That(ChordFormula.Major7.Quality, Is.EqualTo(ChordQuality.Major));
            Assert.That(ChordFormula.Minor7.Quality, Is.EqualTo(ChordQuality.Minor));
        });
    }

    [Test]
    public void SuspendedFormulas_AreDetectedAsSuspended()
    {
        // A suspended chord replaces the third with a 2nd (sus2) or 4th (sus4).
        Assert.Multiple(() =>
        {
            Assert.That(ChordFormula.Suspended2.IsSuspended, Is.True);
            Assert.That(ChordFormula.Suspended4.IsSuspended, Is.True);
            Assert.That(ChordFormula.Suspended2.Quality, Is.EqualTo(ChordQuality.Suspended));
            Assert.That(ChordFormula.Suspended4.Quality, Is.EqualTo(ChordQuality.Suspended));
            Assert.That(ChordFormula.Suspended2.Extension, Is.EqualTo(ChordExtension.Sus2));
            Assert.That(ChordFormula.Suspended4.Extension, Is.EqualTo(ChordExtension.Sus4));
        });
    }

    [Test]
    public void NonSuspendedFormulas_AreNotSuspended()
    {
        // Chords that contain a third (major or minor) are never suspended.
        Assert.Multiple(() =>
        {
            Assert.That(ChordFormula.Major.IsSuspended, Is.False);
            Assert.That(ChordFormula.Minor.IsSuspended, Is.False);
            Assert.That(ChordFormula.Dominant7.IsSuspended, Is.False);
        });
    }

    [Test]
    public void ToInversion_PreservesPitchClassContent()
    {
        var root = new Note.Accidented(NaturalNote.C, Accidental.Natural);
        var chord = new Chord(root, ChordFormula.Major);

        var inverted = chord.ToInversion(1);

        Assert.That(PitchClassValues(inverted), Is.EqualTo(PitchClassValues(chord)));
    }
}
