namespace GA.Business.ML.Tests.Notation;

using System.Text.RegularExpressions;
using GA.Business.DSL.Parsers;
using GA.Business.DSL.Types;
using GA.Business.ML.Notation;
using GA.Domain.Core.Instruments;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Services.Fretboard.Voicings.Generation;

[TestFixture]
public sealed class PlayableNotationFormatterTests
{
    [Test]
    public async Task VoicingDiagrams_ListStringOneFirst()
    {
        // The premise of ToChartOrder: generated voicings (and the index documents built from
        // them) put the highest string first.
        var checkedVoicings = 0;
        await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(
                           Fretboard.CreateGuitar(2), windowSize: 2, parallel: false))
        {
            for (var i = 0; i < voicing.Positions.Length; i++)
            {
                Assert.That(voicing.Positions[i].Location.Str.Value, Is.EqualTo(i + 1));
            }

            if (++checkedVoicings == 50)
            {
                break;
            }
        }

        Assert.That(checkedVoicings, Is.EqualTo(50));
    }

    [TestCase("3-0-x-2-3-x", "x-3-2-x-0-3")]
    [TestCase("0-1-0-2-3-x", "x-3-2-0-1-0")]
    [TestCase("10-13-10-x", "x-10-13-10")]
    [TestCase("x-x-x-x-x-x", "x-x-x-x-x-x")]
    public void ToChartOrder_ReversesVoicingDiagram(string voicingDiagram, string expected) =>
        Assert.That(PlayableNotationFormatter.ToChartOrder(voicingDiagram), Is.EqualTo(expected));

    [Test]
    public void ToChartOrder_ThenVexTab_PlaysTheIndexedNotes()
    {
        // Cmaj7 as stored in the index, string 1 first: G4 B3 x E3 C3 x.
        const string voicingDiagram = "3-0-x-2-3-x";

        var tab = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(
            PlayableNotationFormatter.ToChartOrder(voicingDiagram));

        // Read the positions back with GA's VexTab parser, not by splitting the text.
        var pitchClasses = ChordPositions(tab!)
            .Select(p => (Tuning.Default[new Str(p.String)].MidiNote + p.Fret).PitchClass.Value)
            .OrderBy(pc => pc)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(tab, Is.EqualTo("tabstave\nnotes :w (3/5.2/4.0/2.3/1)"));
            Assert.That(pitchClasses, Is.EqualTo(new[] { 0, 4, 7, 11 }), "C E G B");
        });
    }

    [TestCase(null)]
    [TestCase("")]
    public void ToChartOrder_EmptyInput_ReturnsInput(string? voicingDiagram) =>
        Assert.That(PlayableNotationFormatter.ToChartOrder(voicingDiagram), Is.EqualTo(voicingDiagram));

    [TestCase("x-3-2-0-1-0", "tabstave\nnotes :w (3/5.2/4.0/3.1/2.0/1)")]
    [TestCase("x32010", "tabstave\nnotes :w (3/5.2/4.0/3.1/2.0/1)")]
    [TestCase("0-2-2-1-0-0", "tabstave\nnotes :w (0/6.2/5.2/4.1/3.0/2.0/1)")]
    [TestCase("x-x-0-2-3-2", "tabstave\nnotes :w (0/4.2/3.3/2.2/1)")]
    public void TryFormatChordDiagramAsVexTab_ConvertsSixStringDiagrams(
        string diagram,
        string expected)
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(diagram);

        Assert.That(actual, Is.EqualTo(expected));
    }

    // Voicing.Diagram / OPTK index order: string 1 (high E) first.
    // 3-0-x-2-3-x is Cmaj7 (A3 = C, D2 = E, B0 = B, e3 = G); 0-1-0-2-3-x is open C.
    [TestCase("3-0-x-2-3-x", "tabstave\nnotes :w (3/5.2/4.0/2.3/1)")]
    [TestCase("0-1-0-2-3-x", "tabstave\nnotes :w (3/5.2/4.0/3.1/2.0/1)")]
    [TestCase("10-13-10-x-x-x", "tabstave\nnotes :w (10/3.13/2.10/1)")]
    public void TryFormatChordDiagramAsVexTab_HighToLowOrder_NumbersStringsFromHighE(
        string diagram,
        string expected)
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(diagram, DiagramStringOrder.HighToLow);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void VoicingAgentResultLine_RendersIndexDiagramInTheRightStringOrder()
    {
        // Course finding: the chatbot rendered the Cmaj7 result `3-0-x-2-3-x` as
        // "6/3 5/0 3/2 2/3" (G A A D) because the host read the index diagram low-E-first;
        // in VexTab, fret first, that mirrored tab is (3/6.0/5.2/3.3/2).
        var sb = new System.Text.StringBuilder();
        GA.Business.ML.Agents.VoicingAgent.AppendResultLine(sb, "Cmaj7", "3-0-x-2-3-x", null, 0.369);

        var augmented = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(sb.ToString());

        Assert.Multiple(() =>
        {
            Assert.That(augmented.Text, Does.Contain("(3/5.2/4.0/2.3/1)"));
            Assert.That(augmented.Text, Does.Not.Contain("(3/6.0/5.2/3.3/2)"));
            Assert.That(augmented.AddedFenceCount, Is.EqualTo(0));
            Assert.That(augmented.Text.Split("```vextab").Length - 1, Is.EqualTo(1));
        });
    }

    [Test]
    public void TryFormatChordDiagramAsMarkdownFence_UsesVexTabFence()
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsMarkdownFence("x-3-2-0-1-0");

        Assert.That(actual, Does.StartWith("```vextab"));
        Assert.That(actual, Does.Contain("tabstave\nnotes :w (3/5.2/4.0/3.1/2.0/1)"));
        Assert.That(actual, Does.EndWith("```"));
    }

    [Test]
    public void AugmentMarkdownWithVexTabFences_AddsFenceAfterVoicingLines()
    {
        const string markdown =
            """
            Found 1 voicing:

            - **Dm7(shell)** `10-13-10-x-x-x` (guitar, score 0.594)
            """;

        var actual = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(markdown);

        Assert.Multiple(() =>
        {
            Assert.That(actual.DiagramCount, Is.EqualTo(1));
            Assert.That(actual.AddedFenceCount, Is.EqualTo(1));
            Assert.That(actual.Text, Does.Contain("```vextab"));
            Assert.That(actual.Text, Does.Contain("notes :w (10/6.13/5.10/4)"));
        });
    }

    [Test]
    public void AugmentMarkdownWithVexTabFences_DoesNotDuplicateExistingFence()
    {
        const string markdown =
            """
            - **C** `x-3-2-0-1-0`
            ```vextab
            tabstave
            notes :w (3/5.2/4.0/3.1/2.0/1)
            ```
            """;

        var actual = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(markdown);

        Assert.Multiple(() =>
        {
            Assert.That(actual.DiagramCount, Is.EqualTo(1));
            Assert.That(actual.AddedFenceCount, Is.EqualTo(0));
            Assert.That(actual.Text.Split("```vextab").Length - 1, Is.EqualTo(1));
        });
    }

    /// <summary>
    /// Course finding (VexTab and VexFlow, lesson 4): every block the formatter emitted, the eight
    /// beginner chords included, was a GA token list that VexTab and GA's own parser refused.
    /// The diagrams are BeginnerChordsSkill's, plus a muted-heavy shape and a high one.
    /// </summary>
    [TestCase("x-3-2-0-1-0")]
    [TestCase("3-2-0-0-0-3")]
    [TestCase("x-x-0-2-3-2")]
    [TestCase("x-0-2-2-2-0")]
    [TestCase("0-2-2-1-0-0")]
    [TestCase("x-0-2-2-1-0")]
    [TestCase("0-2-2-0-0-0")]
    [TestCase("x-x-0-2-3-1")]
    [TestCase("x-x-x-x-x-3")]
    [TestCase("x-12-14-14-13-12")]
    public void MarkdownFence_IsVexTabThatGaParserReads(string diagram)
    {
        var fence = PlayableNotationFormatter.TryFormatChordDiagramAsMarkdownFence(diagram)!;
        var body = Regex.Match(fence, "```vextab\\r?\\n(?<body>.*)\\r?\\n```", RegexOptions.Singleline).Groups["body"].Value;

        var result = VexTabParser.parse(body);

        Assert.That(result.IsOk, Is.True, () => $"Block:\n{body}\nParse error: {(result.IsError ? result.ErrorValue : "")}");
    }

    [Test]
    public void MarkdownFence_KeepsTheDiagramsFretsAndStrings()
    {
        // x-3-2-0-1-0: string 5 fret 3, string 4 fret 2, string 3 open, string 2 fret 1, string 1 open.
        var tab = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab("x-3-2-0-1-0")!;

        Assert.That(ChordPositions(tab), Is.EqualTo(new[] { (5, 3), (4, 2), (3, 0), (2, 1), (1, 0) }));
    }

    [Test]
    public void AugmentMarkdownWithVexTabFences_CountsTheBlocksGaParserReads()
    {
        const string markdown =
            """
            - **C** `x-3-2-0-1-0`

            A block in the old token format, as a model may still write it:
            ```vextab
            5/3 4/2 3/0 2/1 1/0
            ```

            - **Am** `x-0-2-2-1-0`
            """;

        var actual = PlayableNotationFormatter.AugmentMarkdownWithVexTabFences(markdown);

        Assert.Multiple(() =>
        {
            Assert.That(actual.AddedFenceCount, Is.EqualTo(2));
            Assert.That(actual.VexTabBlockCount, Is.EqualTo(3));
            Assert.That(actual.ValidVexTabBlockCount, Is.EqualTo(2));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not a diagram")]
    [TestCase("x-3-2-0-1")]
    [TestCase("x-3-2-0-1-99")]
    public void TryFormatChordDiagramAsVexTab_RejectsUnknownShapes(string? diagram)
    {
        var actual = PlayableNotationFormatter.TryFormatChordDiagramAsVexTab(diagram);

        Assert.That(actual, Is.Null);
    }

    /// <summary>The (string, fret) positions of the single chord of a VexTab text.</summary>
    private static (int String, int Fret)[] ChordPositions(string vextab)
    {
        var result = VexTabParser.parse(vextab);
        Assert.That(result.IsOk, Is.True, () => $"Parse error: {(result.IsError ? result.ErrorValue : "")}");

        var chord = result.ResultValue.Lines
            .OfType<VexTabTypes.VexTabLine.NotesLine>().Single().Item2
            .OfType<VexTabTypes.NoteItem.ChordItem>().Single().Item;

        return chord.Notes
            .OfType<VexTabTypes.ChordNote.TabChordNote>()
            .Select(n => (n.Item.String, ((VexTabTypes.Fret.FretNumber)n.Item.Fret).Item))
            .ToArray();
    }
}
