namespace GA.Domain.Core.Tests;

using GA.Domain.Core.Instruments;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Core.Theory.Harmony;

[TestFixture]
public class CoreHardeningRegressionTests
{
    [TestCase("b", -1)]
    [TestCase("bb", -2)]
    [TestCase("bbb", -3)]
    public void FlatAccidental_Parse_PreservesValue(string text, int expected) =>
        Assert.That(FlatAccidental.Parse(text).Value, Is.EqualTo(expected));

    [TestCase("#")]
    [TestCase("x")]
    [TestCase("b\n")]
    public void FlatAccidental_TryParse_InvalidNotation_ReturnsFalse(string text) =>
        Assert.That(FlatAccidental.TryParse(text, null, out _), Is.False);

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(4)]
    [TestCase(9)]
    public void FlatFactories_PreserveNamedNoteAndOctave(int octave) => Assert.Multiple(() =>
    {
        Assert.That(Pitch.Flat.DFlat(octave), Is.EqualTo(new Pitch.Flat(Note.Flat.DFlat, octave)));
        Assert.That(Pitch.Flat.FFlat(octave), Is.EqualTo(new Pitch.Flat(Note.Flat.FFlat, octave)));
        Assert.That(Pitch.Flat.GFlat(octave), Is.EqualTo(new Pitch.Flat(Note.Flat.GFlat, octave)));
    });

    [TestCase(null)]
    [TestCase("")]
    [TestCase("junkC4tail")]
    [TestCase("C4\n")]
    [TestCase("C10")]
    [TestCase("C11")]
    [TestCase("C-2")]
    [TestCase("C99")]
    public void PitchTryParse_InvalidInput_ReturnsFalse(string? input) => Assert.Multiple(() =>
    {
        Assert.That(Pitch.Sharp.TryParse(input, null, out _), Is.False);
        Assert.That(Pitch.Flat.TryParse(input, null, out _), Is.False);
    });

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(9)]
    public void PitchTryParse_SupportedOctaves_RoundTrip(int octave) => Assert.Multiple(() =>
    {
        Assert.That(Pitch.Sharp.Parse($"C#{octave}"), Is.EqualTo(Pitch.Sharp.CSharp(octave)));
        Assert.That(Pitch.Flat.Parse($"Db{octave}"), Is.EqualTo(new Pitch.Flat(Note.Flat.DFlat, octave)));
    });

    [Test]
    public void FormulaEquality_ReorderedIntervals_WorkAsDictionaryKeys()
    {
        var first = ChordFormula.FromSemitones("first", 4, 7, 4);
        var reordered = ChordFormula.FromSemitones("reordered", 7, 4, 4);
        Dictionary<ChordFormula, string> lookup = new() { [first] = "found" };
        Assert.Multiple(() =>
        {
            Assert.That(first.Equals(reordered), Is.True);
            Assert.That(reordered.Equals(first), Is.True);
            Assert.That(reordered.GetHashCode(), Is.EqualTo(first.GetHashCode()));
            Assert.That(lookup.TryGetValue(reordered, out _), Is.True);
        });
    }

    [Test]
    public void FormulaEquality_DifferentMultiplicity_IsSymmetric()
    {
        var repeated = ChordFormula.FromSemitones("repeated", 4, 4);
        var major = ChordFormula.Major;
        Assert.Multiple(() =>
        {
            Assert.That(repeated.Equals(major), Is.False);
            Assert.That(major.Equals(repeated), Is.False);
        });
    }

    [Test]
    public void Inversion_WithSharpRoot_ComparesPitchClasses()
    {
        var chord = new Chord(Note.Sharp.C, ChordFormula.Major);
        var inverted = chord.ToInversion(1);
        Assert.Multiple(() =>
        {
            Assert.That(chord.IsInverted, Is.False);
            Assert.That(inverted.IsInverted, Is.True);
            Assert.That(inverted.GetInversion(), Is.EqualTo(1));
            Assert.That(inverted.ToInversion(1).GetInversion(), Is.EqualTo(1));
            Assert.That(inverted.ToInversion(2).GetInversion(), Is.EqualTo(2));
            Assert.That(inverted.ToInversion(0).IsInverted, Is.False);
        });
    }

    [TestCase("C")]
    [TestCase("Cm")]
    [TestCase("Cmaj7")]
    [TestCase("C9")]
    [TestCase("C13")]
    public void ToInversion_PreservesFormulaAndSymbol(string symbol)
    {
        var chord = Chord.FromSymbol(symbol);
        for (var inversion = 0; inversion < chord.Notes.Count; inversion++)
        {
            var inverted = chord.ToInversion(inversion);
            Assert.Multiple(() =>
            {
                Assert.That(inverted.Formula, Is.EqualTo(chord.Formula));
                Assert.That(inverted.Quality, Is.EqualTo(chord.Quality));
                Assert.That(inverted.Symbol, Is.EqualTo(chord.Symbol));
                Assert.That(inverted.GetInversion(), Is.EqualTo(inversion));
                Assert.That(inverted.ToInversion(0).Bass.PitchClass, Is.EqualTo(chord.Root.PitchClass));
            });
        }
    }

    [Test]
    public void NotesConstructor_InvertedNotes_AnalyzesAllNonRootNotes()
    {
        var chord = new Chord(AccidentedNoteCollection.Parse("E G C"), Note.Accidented.Parse("C", null));
        Assert.That(chord.Quality, Is.EqualTo(ChordQuality.Major));
        Assert.That(chord.Formula, Is.EqualTo(ChordFormula.Major));
    }

    [TestCase("Cmaj7")]
    [TestCase("Cmaj9")]
    [TestCase("Cmaj11")]
    [TestCase("Cmaj13")]
    [TestCase("C7")]
    [TestCase("Cm7")]
    [TestCase("C6/9")]
    public void GeneratedSymbol_PreservesChordMeaning(string symbol)
    {
        var parsed = Chord.FromSymbol(symbol);
        var generated = new Chord(parsed.Root, parsed.Formula);
        Assert.Multiple(() =>
        {
            Assert.That(generated.Symbol, Is.EqualTo(symbol));
            Assert.That(Chord.FromSymbol(generated.Symbol).Formula, Is.EqualTo(parsed.Formula));
            Assert.That(parsed.Formula.GetSymbolSuffix(), Is.EqualTo(symbol[1..]));
        });
    }

    [Test]
    public void FretboardConstructor_InvalidGeometry_RejectsInput() => Assert.Multiple(() =>
    {
        Assert.That(() => Fretboard.CreateGuitar(-1), Throws.TypeOf<ArgumentOutOfRangeException>());
        Assert.That(() => new Fretboard(new Tuning(PitchCollection.Empty), 24), Throws.ArgumentException);
        Assert.That(() => new Fretboard(null!, 24), Throws.ArgumentNullException);
    });

    [Test]
    public void FretboardConstructor_ZeroFrets_AllowsOpenStrings()
    {
        var fretboard = Fretboard.CreateGuitar(0);
        Assert.That(fretboard.GetNote(0, 0).PitchClass, Is.EqualTo(Note.Sharp.E.PitchClass));
    }
}


