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
            Assert.That(chord.Quality, Is.EqualTo(ChordQuality.Minor7));
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
    public void Formula_Quality_IsClassifiedFromIntervals()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ChordFormula.Major.Quality, Is.EqualTo(ChordQuality.Major));
            Assert.That(ChordFormula.Minor.Quality, Is.EqualTo(ChordQuality.Minor));
            Assert.That(ChordFormula.Diminished.Quality, Is.EqualTo(ChordQuality.Diminished));
            Assert.That(ChordFormula.Augmented.Quality, Is.EqualTo(ChordQuality.Augmented));
            Assert.That(ChordFormula.Dominant7.Quality, Is.EqualTo(ChordQuality.Dominant));
            Assert.That(ChordFormula.Major7.Quality, Is.EqualTo(ChordQuality.Major7));
            Assert.That(ChordFormula.Minor7.Quality, Is.EqualTo(ChordQuality.Minor7));
        });
    }

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

    // Root is whatever Note type the caller passed while Notes are Accidented: comparing the records
    // made every root-position chord built on a Note.Sharp or Note.Chromatic root look inverted.
    [Test]
    public void IsInverted_RootPosition_IsFalse_WhateverTheRootNoteType() =>
        Assert.Multiple(() =>
        {
            Assert.That(new Chord(Note.Sharp.C, ChordFormula.Major).IsInverted, Is.False);
            Assert.That(new Chord(Note.Chromatic.C, ChordFormula.Major).IsInverted, Is.False);
            Assert.That(new Chord(Note.Sharp.C, ChordFormula.Major).ToInversion(1).IsInverted, Is.True);
        });

    // One letter per chord degree, counted up from the root letter in thirds (then 2nds, 4ths, 6ths
    // for added tones), with whatever accidental the interval needs: Cm is C Eb G, never C D# G.
    [TestCase("C", "C E G")]
    [TestCase("Cm", "C Eb G")]
    [TestCase("Eb", "Eb G Bb")]
    [TestCase("Ab", "Ab C Eb")]
    [TestCase("F#", "F♯ A♯ C♯")]
    [TestCase("Bbm7", "Bb Db F Ab")]
    [TestCase("Cdim", "C Eb Gb")]
    [TestCase("Cdim7", "C Eb Gb Bbb")]
    [TestCase("F#dim7", "F♯ A C Eb")]
    [TestCase("Gb7", "Gb Bb Db Fb")]
    [TestCase("Cm7b5", "C Eb Gb Bb")]
    [TestCase("Caug", "C E G♯")]
    [TestCase("Csus2", "C D G")]
    [TestCase("Csus4", "C F G")]
    [TestCase("C6", "C E G A")]
    [TestCase("Cm6", "C Eb G A")]
    [TestCase("C9", "C E G Bb D")]
    [TestCase("Cadd9", "C E G D")]
    [TestCase("C13", "C E G Bb D F A")]
    public void FromSymbol_SpellsOneLetterPerChordDegree(string symbol, string expectedNotes) =>
        Assert.That(string.Join(" ", Chord.FromSymbol(symbol).Notes), Is.EqualTo(expectedNotes));

    [TestCase(new[] { 1, 4, 7, 10 }, "C Db E G Bb")]  // 7(b9)
    [TestCase(new[] { 3, 4, 7, 10 }, "C D♯ E G Bb")]  // 7(#9)
    [TestCase(new[] { 4, 6, 7, 10 }, "C E F♯ G Bb")]  // 7(#11)
    [TestCase(new[] { 4, 7, 8, 10 }, "C E G Ab Bb")]  // 7(b13)
    [TestCase(new[] { 4, 6, 10 }, "C E Gb Bb")]        // 7(b5)
    [TestCase(new[] { 4, 8, 10 }, "C E G♯ Bb")]       // 7(#5)
    public void Constructor_SpellsAlteredTonesByTheirDegree(int[] semitones, string expectedNotes)
    {
        var chord = new Chord(Note.Accidented.C, ChordFormula.FromSemitones("Altered", semitones));

        Assert.That(string.Join(" ", chord.Notes), Is.EqualTo(expectedNotes));
    }

    // A chord built from notes measures its intervals from the root, whatever note is in the bass:
    // C/E (E G C) used to skip E instead of C and lose its third (quality Other).
    [TestCase("C", 1, ChordQuality.Major, "C")]
    [TestCase("C", 2, ChordQuality.Major, "C")]
    [TestCase("Cm", 1, ChordQuality.Minor, "Cm")]
    [TestCase("C7", 3, ChordQuality.Dominant, "C7")]
    [TestCase("Cmaj7", 1, ChordQuality.Major7, "Cmaj7")]
    public void ToInversion_KeepsQualityAndSymbol(string symbol, int inversion, ChordQuality expectedQuality, string expectedSymbol)
    {
        var inverted = Chord.FromSymbol(symbol).ToInversion(inversion);

        Assert.Multiple(() =>
        {
            Assert.That(inverted.GetInversion(), Is.EqualTo(inversion));
            Assert.That(inverted.Quality, Is.EqualTo(expectedQuality));
            Assert.That(inverted.Symbol, Is.EqualTo(expectedSymbol));
            Assert.That(inverted.Formula, Is.EqualTo(Chord.FromSymbol(symbol).Formula));
        });
    }

    // The suffix must name the seventh chord, not just the triad quality plus "7":
    // Cmaj7 was "7", Cm7b5 was "dim7" and Cdim7 (3, 6, 9 semitones) was "dim6".
    [TestCase("C", "")]
    [TestCase("Cm", "m")]
    [TestCase("Cdim", "dim")]
    [TestCase("Caug", "aug")]
    [TestCase("C6", "6")]
    [TestCase("C7", "7")]
    [TestCase("Cmaj7", "maj7")]
    [TestCase("Cm7", "m7")]
    [TestCase("Cm7b5", "m7b5")]
    [TestCase("Cdim7", "dim7")]
    [TestCase("C9", "9")]
    [TestCase("Cmaj9", "maj9")]
    [TestCase("Cm9", "m9")]
    [TestCase("Cadd9", "add9")]
    public void GetSymbolSuffix_NamesSeventhChordQualities(string symbol, string expectedSuffix) =>
        Assert.That(Chord.FromSymbol(symbol).Formula.GetSymbolSuffix(), Is.EqualTo(expectedSuffix));

    [TestCase(new[] { 0, 4, 7, 11 }, "Cmaj7")]
    [TestCase(new[] { 0, 3, 6, 10 }, "Cm7b5")]
    [TestCase(new[] { 0, 3, 6, 9 }, "Cdim7")]
    [TestCase(new[] { 0, 3, 7, 10 }, "Cm7")]
    [TestCase(new[] { 0, 4, 7, 10 }, "C7")]
    public void Constructor_WithNotes_GeneratesSeventhChordSymbol(int[] pitchClasses, string expectedSymbol)
    {
        var notes = new AccidentedNoteCollection(
            [.. pitchClasses.Select(pc => PitchClass.FromValue(pc).ToChromaticNote().ToAccidented())]);

        Assert.That(new Chord(notes).Symbol, Is.EqualTo(expectedSymbol));
    }

    [Test]
    public void Formula_Quality_DistinguishesSeventhChordSpecies() =>
        Assert.Multiple(() =>
        {
            Assert.That(Chord.FromSymbol("Cmaj7").Quality, Is.EqualTo(ChordQuality.Major7));
            Assert.That(Chord.FromSymbol("Cm7").Quality, Is.EqualTo(ChordQuality.Minor7));
            Assert.That(Chord.FromSymbol("Cm7b5").Quality, Is.EqualTo(ChordQuality.HalfDiminished));
            Assert.That(Chord.FromSymbol("Cdim7").Quality, Is.EqualTo(ChordQuality.Diminished7));
        });

    [Test]
    public void DiminishedSeventh_IsASeventhExtension() =>
        Assert.That(Chord.FromSymbol("Cdim7").Extension, Is.EqualTo(ChordExtension.Seventh));

    [Test]
    public void MinorMajorSeventh_SymbolRoundTrips()
    {
        var chord = new Chord(Note.Accidented.C, ChordFormula.FromSemitones("Minor major seventh", 3, 7, 11));

        Assert.That(Chord.FromSymbol(chord.Symbol).Formula, Is.EqualTo(chord.Formula));
    }
}
