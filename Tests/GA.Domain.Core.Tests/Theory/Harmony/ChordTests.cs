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
    public void FromSymbol_ShouldCreateCorrectQuality(string symbol, ChordQuality expectedQuality) => Assert.That(Chord.FromSymbol(symbol).Quality, Is.EqualTo(expectedQuality));

    [TestCase("C7", ChordExtension.Seventh)]
    [TestCase("Cmaj7", ChordExtension.Seventh)]
    [TestCase("C9", ChordExtension.Ninth)]
    [TestCase("C11", ChordExtension.Eleventh)]
    [TestCase("C13", ChordExtension.Thirteenth)]
    public void FromSymbol_ShouldCreateCorrectExtension(string symbol, ChordExtension expectedExtension) => Assert.That(Chord.FromSymbol(symbol).Extension, Is.EqualTo(expectedExtension));

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
    public void TryFromSymbol_InvalidSymbol_ReturnsFalse() => Assert.Multiple(() =>
    {
        Assert.That(Chord.TryFromSymbol("H7", out _), Is.False);   // H is not a note letter
        Assert.That(Chord.TryFromSymbol("Cwobble", out _), Is.False); // unknown suffix
        Assert.That(Chord.TryFromSymbol("C", out var c), Is.True);
        Assert.That(c!.Quality, Is.EqualTo(ChordQuality.Major));
    });

    // Regression: IsInverted compared a Note.Accidented against the original Note subtype, so record equality
    // across subtypes always failed and a root-position chord reported as inverted.
    [Test]
    public void RootPosition_IsNotInverted() => Assert.Multiple(() =>
    {
        Assert.That(Chord.FromSymbol("C").IsInverted, Is.False);
        Assert.That(Chord.FromSymbol("C").GetInversion(), Is.Zero);
        Assert.That(Chord.FromSymbol("F#m7").IsInverted, Is.False);
        Assert.That(new Chord(Note.Sharp.C, ChordFormula.Major).IsInverted, Is.False);
    });

    // TryFromSymbol must not rely on exceptions as control flow
    [Test]
    public void TryFromSymbol_DoesNotThrowForAnyInput() => Assert.Multiple(() =>
    {
        Assert.That(() => Chord.TryFromSymbol("", out _), Throws.Nothing);
        Assert.That(() => Chord.TryFromSymbol("   ", out _), Throws.Nothing);
        Assert.That(() => Chord.TryFromSymbol("Amm7", out _), Throws.Nothing);
        Assert.That(Chord.TryFromSymbol("", out _), Is.False);
        Assert.That(Chord.TryFromSymbol("   ", out _), Is.False);
        Assert.That(Chord.TryFromSymbol("Amm7", out _), Is.False);
    });

    [Test]
    public void FromSymbol_InvalidSymbol_Throws() => Assert.Multiple(() =>
    {
        Assert.That(() => Chord.FromSymbol(""), Throws.ArgumentException);
        Assert.That(() => Chord.FromSymbol("Amm7"), Throws.ArgumentException);
        Assert.That(() => Chord.FromSymbol("H7"), Throws.ArgumentException);
    });

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
        [.. chord.PitchClassSet.Select(pc => pc.Value).OrderBy(v => v)];

    // C major triad = {C, E, G} = {0, 4, 7}.
    [Test]
    public void MajorTriad_HasRootMajorThirdPerfectFifth() => Assert.That(PitchClassValues(CChord(ChordFormula.Major)), Is.EqualTo(new[] { 0, 4, 7 }));

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
    public void Chord_Quality_AgreesWithFormula()
    {
        // Chord.Quality/Extension delegate to Formula, so a chord and its formula never disagree.
        // A dominant-7th chord reports Dominant (not the old triad-only fallback of Major).
        var dom = CChord(ChordFormula.Dominant7);
        var sus = CChord(ChordFormula.Suspended2);
        Assert.Multiple(() =>
        {
            Assert.That(dom.Quality, Is.EqualTo(ChordQuality.Dominant));
            Assert.That(dom.Quality, Is.EqualTo(dom.Formula.Quality));
            Assert.That(sus.Quality, Is.EqualTo(ChordQuality.Suspended));
            Assert.That(sus.Quality, Is.EqualTo(sus.Formula.Quality));
            Assert.That(sus.Extension, Is.EqualTo(ChordExtension.Sus2));
            Assert.That(sus.Extension, Is.EqualTo(sus.Formula.Extension));
        });
    }

    [TestCaseSource(nameof(SeventhChordCases))]
    public void SeventhChords_ClassifyAsSeventhExtension(ChordFormula formula) => Assert.That(CChord(formula).Extension, Is.EqualTo(ChordExtension.Seventh));

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
    public void Formula_Quality_IsClassifiedFromIntervals() => Assert.Multiple(() =>
    {
        Assert.That(ChordFormula.Major.Quality, Is.EqualTo(ChordQuality.Major));
        Assert.That(ChordFormula.Minor.Quality, Is.EqualTo(ChordQuality.Minor));
        Assert.That(ChordFormula.Diminished.Quality, Is.EqualTo(ChordQuality.Diminished));
        Assert.That(ChordFormula.Augmented.Quality, Is.EqualTo(ChordQuality.Augmented));
        Assert.That(ChordFormula.Dominant7.Quality, Is.EqualTo(ChordQuality.Dominant));
        Assert.That(ChordFormula.Major7.Quality, Is.EqualTo(ChordQuality.Major));
        Assert.That(ChordFormula.Minor7.Quality, Is.EqualTo(ChordQuality.Minor));
    });

    // A suspended chord replaces the third with a 2nd (sus2) or 4th (sus4).
    [Test]
    public void SuspendedFormulas_AreDetectedAsSuspended() => Assert.Multiple(() =>
    {
        Assert.That(ChordFormula.Suspended2.IsSuspended, Is.True);
        Assert.That(ChordFormula.Suspended4.IsSuspended, Is.True);
        Assert.That(ChordFormula.Suspended2.Quality, Is.EqualTo(ChordQuality.Suspended));
        Assert.That(ChordFormula.Suspended4.Quality, Is.EqualTo(ChordQuality.Suspended));
        Assert.That(ChordFormula.Suspended2.Extension, Is.EqualTo(ChordExtension.Sus2));
        Assert.That(ChordFormula.Suspended4.Extension, Is.EqualTo(ChordExtension.Sus4));
    });

    // Chords that contain a third (major or minor) are never suspended.
    [Test]
    public void NonSuspendedFormulas_AreNotSuspended() => Assert.Multiple(() =>
    {
        Assert.That(ChordFormula.Major.IsSuspended, Is.False);
        Assert.That(ChordFormula.Minor.IsSuspended, Is.False);
        Assert.That(ChordFormula.Dominant7.IsSuspended, Is.False);
    });

    [Test]
    public void ToInversion_PreservesPitchClassContent()
    {
        var root = new Note.Accidented(NaturalNote.C, Accidental.Natural);
        var chord = new Chord(root, ChordFormula.Major);

        var inverted = chord.ToInversion(1);

        Assert.That(PitchClassValues(inverted), Is.EqualTo(PitchClassValues(chord)));
    }
}
