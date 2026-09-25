namespace GA.Business.DSL.Tests;

using System.Text.RegularExpressions;
using GA.Business.DSL.Generators;
using GA.Business.DSL.Parsers;
using GA.Business.DSL.Types;
using Microsoft.FSharp.Collections;
using Microsoft.FSharp.Core;

/// <summary>
/// The VexTab parser against VexTab itself, against GA's own generator, and against the
/// examples in Grammars/VexTab.ebnf. Every text in <see cref="VexTabAccepts"/> was checked
/// with VexTab 4.0.5 (npm <c>vextab</c>, the parser behind vexflow.com/vextab), and every
/// text in <see cref="VexTabRejects"/> is refused by it.
/// </summary>
[TestFixture]
public partial class VexTabRoundTripTests
{
    /// <summary>Texts VexTab 4.0.5 accepts, one per feature of its tutorial.</summary>
    public static IEnumerable<TestCaseData> VexTabAccepts()
    {
        yield return Case("chord on one string", "tabstave notation=true\nnotes :q (0/6.3/6)");
        yield return Case("two notes", "tabstave notation=true\nnotes :q 4/4 5/5");
        yield return Case("open C chord", "tabstave notation=true\nnotes :w (0/1.1/2.0/3.2/4.3/5)");
        yield return Case("key and time", "tabstave notation=true key=G time=4/4\nnotes :q 5/3");
        yield return Case("minor key", "tabstave notation=true key=Am\nnotes :q C/4");
        yield return Case("flat written @", "tabstave notation=true tablature=false\nnotes :q B@/4");
        yield return Case("techniques between frets", "tabstave\nnotes 5h7/3 7b9b7/3");
        yield return Case("tap before the fret", "tabstave\nnotes t12p7p5h7/4");
        yield return Case("fret run", "tabstave\nnotes 4-5-6/3");
        yield return Case("duration mid-line", "tabstave\nnotes :q 5/3 :8 7/3 5/3");
        yield return Case("annotation", "tabstave\nnotes :q 5/5 $Am$");
        yield return Case("articulation", "tabstave notation=true\nnotes :q C/4 $.a./top.$");
        yield return Case("text line", "tabstave notation=true\nnotes :h 5/5 7/5\ntext :h,G,C");
        yield return Case("bars and text line",
            "tabstave notation=true time=4/4\n" +
            "notes :h (3/5.2/4.0/3.1/2.0/1) (3/6.2/5.0/4.0/3.0/2.3/1) | (0/4.2/3.3/2.2/1) (0/5.2/4.2/3.2/2.0/1)\n" +
            "text :h,C,G,|,D,A");
        yield return Case("two staves", "tabstave\nnotes :q 0/1 3/1\n\ntabstave\nnotes :q 5/1 7/1");
        yield return Case("GA generator's default stave",
            "tabstave notation=true tablature=true clef=treble tuning=standard\nnotes:w (0/1.1/2.0/3.2/4.3/5)");

        static TestCaseData Case(string name, string text) => new TestCaseData(text).SetName($"VexTab accepts: {name}");
    }

    /// <summary>Texts VexTab 4.0.5 refuses.</summary>
    public static IEnumerable<TestCaseData> VexTabRejects()
    {
        yield return Case("flat written b", "tabstave notation=true tablature=false\nnotes :q Bb/4");
        yield return Case("technique after the string", "tabstave\nnotes 5/3h7-7/3b9b7");
        yield return Case("technique after the string, spaced", "tabstave\nnotes 5/3h7 7/3b9b7");
        yield return Case("string/fret tokens without a stave", "5/3 4/2 3/0 2/1 1/0");

        static TestCaseData Case(string name, string text) => new TestCaseData(text).SetName($"VexTab rejects: {name}");
    }

    [TestCaseSource(nameof(VexTabAccepts))]
    public void ParsesWhatVexTabAccepts(string text)
    {
        var result = VexTabParser.parse(text);
        Assert.That(result.IsOk, Is.True, () => $"Parse error: {result.ErrorValue}");
    }

    [TestCaseSource(nameof(VexTabRejects))]
    public void RejectsWhatVexTabRejects(string text) =>
        Assert.That(VexTabParser.parse(text).IsError, Is.True);

    /// <summary>
    /// Parse, generate, parse the generated text again: the generator's output must be readable
    /// by the parser, and a second trip must change nothing.
    /// </summary>
    [TestCaseSource(nameof(VexTabAccepts))]
    public void GeneratedTextParsesBackToTheSameText(string text)
    {
        var generated = VexTabGenerator.generate(ParseOrFail(text));
        var reparsed = VexTabParser.parse(generated);

        Assert.That(reparsed.IsOk, Is.True, () => $"Generated:\n{generated}\nParse error: {reparsed.ErrorValue}");
        Assert.That(VexTabGenerator.generate(reparsed.ResultValue), Is.EqualTo(generated));
    }

    [TestCase("tabstave\nnotes 5/3")]
    [TestCase("tabstave notation=true key=Am\nnotes :q C/4")]
    [TestCase("tabstave notation=true tablature=false\nnotes :q B@/4")]
    [TestCase("tabstave\nnotes 5h7/3 7b9b7/3")]
    [TestCase("tabstave\nnotes t12p7p5h7/4")]
    [TestCase("tabstave\nnotes :q 5/3 :8 7/3 5/3")]
    [TestCase("tabstave\nnotes :q 5/5 $Am$")]
    [TestCase("tabstave notation=true\nnotes :q C/4 $.a./top.$")]
    [TestCase("tabstave notation=true\nnotes :h 5/5 7/5\ntext :h,G,C")]
    public void GeneratorWritesBackTheCanonicalText(string text) =>
        Assert.That(VexTabGenerator.generate(ParseOrFail(text)).ReplaceLineEndings("\n"), Is.EqualTo(text));

    /// <summary>
    /// What the generator writes for a document built in code, with GA's default stave: the
    /// clef and tuning options it fills in used to make the parser stop at column 51.
    /// </summary>
    [Test]
    public void GeneratorOutputForTheDefaultStaveParses()
    {
        var chord = new VexTabTypes.Chord(
            ListModule.OfSeq(new[] { (0, 1), (1, 2), (0, 3), (2, 4), (3, 5) }
                .Select(p => VexTabTypes.ChordNote.NewTabChordNote(VexTabTypes.createTabNote(p.Item1, p.Item2)))),
            FSharpList<VexTabTypes.Technique>.Empty);
        var document = new VexTabTypes.VexTabDocument(ListModule.OfSeq(new[]
        {
            VexTabTypes.VexTabLine.NewTabstaveLine(VexTabTypes.defaultTabstave),
            VexTabTypes.VexTabLine.NewNotesLine(
                FSharpOption<VexTabTypes.Duration>.Some(new VexTabTypes.Duration(VexTabTypes.DurationCode.Whole, false, false)),
                ListModule.OfSeq(new[] { VexTabTypes.NoteItem.NewChordItem(chord) }))
        }));

        var generated = VexTabGenerator.generate(document);
        var reparsed = VexTabParser.parse(generated);

        Assert.That(reparsed.IsOk, Is.True, () => $"Generated:\n{generated}\nParse error: {reparsed.ErrorValue}");
        Assert.That(VexTabGenerator.generate(reparsed.ResultValue), Is.EqualTo(generated));
    }

    /// <summary>The generator writes VexTab's syntax, not the one VexTab refuses.</summary>
    [Test]
    public void GeneratorWritesVexTabSyntax()
    {
        var flatB = new VexTabTypes.StandardNote(
            VexTabTypes.NoteLetter.B, FSharpOption<VexTabTypes.VexAccidental>.Some(VexTabTypes.VexAccidental.Flat), 4,
            FSharpList<VexTabTypes.Technique>.Empty, FSharpOption<VexTabTypes.Articulation>.None);
        var hammer = new VexTabTypes.TabNote(
            VexTabTypes.Fret.NewFretNumber(5), 3,
            ListModule.OfSeq(new[] { VexTabTypes.Technique.NewHammerOn(7) }), FSharpOption<VexTabTypes.Articulation>.None);
        var tap = new VexTabTypes.TabNote(
            VexTabTypes.Fret.NewFretNumber(12), 4,
            ListModule.OfSeq(new[] { VexTabTypes.Technique.Tap, VexTabTypes.Technique.NewPullOff(7) }),
            FSharpOption<VexTabTypes.Articulation>.None);
        var aMinor = new VexTabTypes.KeySignature(
            VexTabTypes.NoteLetter.A, FSharpOption<VexTabTypes.VexAccidental>.None, FSharpOption<string>.Some("minor"));

        Assert.Multiple(() =>
        {
            Assert.That(VexTabGenerator.formatStandardNote(flatB), Is.EqualTo("B@/4"));
            Assert.That(VexTabGenerator.formatTabNote(hammer), Is.EqualTo("5h7/3"));
            Assert.That(VexTabGenerator.formatTabNote(tap), Is.EqualTo("t12p7/4"));
            Assert.That(VexTabGenerator.formatKeySignature(aMinor), Is.EqualTo("Am"));
            Assert.That(VexTabGenerator.formatDuration(new VexTabTypes.Duration(VexTabTypes.DurationCode.Eighth, true, true)),
                Is.EqualTo(":8Sd"));
        });
    }

    [Test]
    public void TechniquesBetweenFretsBelongToTheNote()
    {
        var note = SingleTabNote("tabstave\nnotes 5h7/3");

        Assert.Multiple(() =>
        {
            Assert.That(note.Fret, Is.EqualTo(VexTabTypes.Fret.NewFretNumber(5)));
            Assert.That(note.String, Is.EqualTo(3));
            Assert.That(note.Techniques, Is.EqualTo(ListModule.OfSeq(new[] { VexTabTypes.Technique.NewHammerOn(7) })));
        });
    }

    [Test]
    public void BendAndReleaseIsOneBend()
    {
        var note = SingleTabNote("tabstave\nnotes 7b9b7/3");

        Assert.That(note.Techniques,
            Is.EqualTo(ListModule.OfSeq(new[] { VexTabTypes.Technique.NewBend(9, FSharpOption<int>.Some(7)) })));
    }

    [Test]
    public void FretRunIsOneNotePerFretOnTheSameString()
    {
        var notes = NoteItems("tabstave\nnotes 4-5-6/3").Cast<VexTabTypes.NoteItem.TabNoteItem>().Select(i => i.Item).ToList();

        Assert.That(notes.Select(n => (n.Fret, n.String)), Is.EqualTo(new[]
        {
            (VexTabTypes.Fret.NewFretNumber(4), 3), (VexTabTypes.Fret.NewFretNumber(5), 3), (VexTabTypes.Fret.NewFretNumber(6), 3)
        }));
    }

    [Test]
    public void FlatIsWrittenWithAnAt()
    {
        var item = NoteItems("tabstave notation=true\nnotes :q B@/4").Single();
        var note = ((VexTabTypes.NoteItem.StandardNoteItem)item).Item;

        Assert.That(note.Accidental, Is.EqualTo(FSharpOption<VexTabTypes.VexAccidental>.Some(VexTabTypes.VexAccidental.Flat)));
    }

    [Test]
    public void MinorKeyIsWrittenWithAnM()
    {
        var stave = (VexTabTypes.VexTabLine.TabstaveLine)ParseOrFail("tabstave key=Am\nnotes 5/3").Lines.Head;
        var key = stave.Item.Key.Value;

        Assert.Multiple(() =>
        {
            Assert.That(key.Root, Is.EqualTo(VexTabTypes.NoteLetter.A));
            Assert.That(key.Mode, Is.EqualTo(FSharpOption<string>.Some("minor")));
        });
    }

    [Test]
    public void TabstaveAloneSetsNoOption()
    {
        var stave = (VexTabTypes.VexTabLine.TabstaveLine)ParseOrFail("tabstave\nnotes 5/3").Lines.Head;

        Assert.That(stave.Item.Notation, Is.Null, "VexTab's default is notation=false; the parser must not turn it on");
    }

    [Test]
    public void ArticulationAttachesToTheNoteBeforeIt()
    {
        var item = NoteItems("tabstave notation=true\nnotes :q C/4 $.a./top.$").Single();
        var note = ((VexTabTypes.NoteItem.StandardNoteItem)item).Item;

        Assert.That(note.Articulation, Is.EqualTo(FSharpOption<VexTabTypes.Articulation>.Some(
            new VexTabTypes.Articulation(VexTabTypes.ArticulationType.Staccato, VexTabTypes.ArticulationPosition.Top))));
    }

    [Test]
    public void AnnotationIsANoteItem()
    {
        var items = NoteItems("tabstave\nnotes :q 5/5 $Am$");

        Assert.That(items.Last(), Is.EqualTo(VexTabTypes.NoteItem.NewAnnotationItem(VexTabTypes.createAnnotation("Am"))));
    }

    [Test]
    public void ErrorIsReportedWhereTheTextIsWrong()
    {
        // An unknown option is reported at its first character, with the options expected there.
        // The old parser stopped at the same column but expected the end of the line: `key=G`
        // had eaten the space that starts the next option.
        var result = VexTabParser.parse("tabstave notation=true key=G tempo=4/4\nnotes :q 5/3");

        Assert.That(result.IsError, Is.True);
        Assert.That(result.ErrorValue, Does.StartWith("Error in Ln: 1 Col: 30"));
        Assert.That(result.ErrorValue, Does.Contain("time="));
    }

    /// <summary>
    /// Every example in the grammar file parses. The examples are read from the file, so an
    /// example added there is tested too.
    /// </summary>
    [Test]
    public void GrammarExamplesParse()
    {
        var grammar = File.ReadAllText(System.IO.Path.Combine(TestContext.CurrentContext.TestDirectory, "Grammars", "VexTab.ebnf"));
        var examples = ExampleRegex().Matches(grammar)
            .Select(m => string.Join("\n", m.Groups["body"].Value.ReplaceLineEndings("\n").Split('\n')
                .Select(line => line.Trim()).Where(line => line.Length > 0)))
            .ToList();

        Assert.That(examples, Has.Count.GreaterThanOrEqualTo(4));
        Assert.Multiple(() =>
        {
            foreach (var example in examples)
            {
                var result = VexTabParser.parse(example);
                Assert.That(result.IsOk, Is.True, () => $"Example:\n{example}\nParse error: {result.ErrorValue}");
            }
        });
    }

    [GeneratedRegex(@"\(\* Example \d+:[^\n]*\n(?<body>.*?)\*\)", RegexOptions.Singleline)]
    private static partial Regex ExampleRegex();

    private static VexTabTypes.VexTabDocument ParseOrFail(string text)
    {
        var result = VexTabParser.parse(text);
        Assert.That(result.IsOk, Is.True, () => $"Parse error: {result.ErrorValue}");
        return result.ResultValue;
    }

    private static IReadOnlyList<VexTabTypes.NoteItem> NoteItems(string text) =>
        ParseOrFail(text).Lines.OfType<VexTabTypes.VexTabLine.NotesLine>().Single().Item2.ToList();

    private static VexTabTypes.TabNote SingleTabNote(string text) =>
        ((VexTabTypes.NoteItem.TabNoteItem)NoteItems(text).Single()).Item;
}
