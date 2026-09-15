namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Rag;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     Index documents carry the chord root, the lowest sounding note and the inversion. Voicings list
///     their notes in position order, string 1 (high E) first, like <c>VoicingGenerator</c> builds them.
/// </summary>
[TestFixture]
public class VoicingDocumentFactoryTests
{
    // Standard tuning, string 1 (high E) first.
    private static readonly int[] OpenStringMidi = [64, 59, 55, 50, 45, 40];

    [TestCase("0-1-0-2-3-x", 0, 48, 0, TestName = "Open C: root C, bass C3")]
    [TestCase("0-1-0-2-3-0", 0, 40, 1, TestName = "C/E: bass E2, first inversion")]
    [TestCase("0-1-2-2-0-x", 9, 45, 0, TestName = "Open Am: root A, bass A2")]
    [TestCase("0-0-1-2-2-0", 4, 40, 0, TestName = "Open E: root E, bass E2")]
    public void FromAnalysis_UsesChordRootAndLowestNote(string stringOneFirstDiagram, int root, int bass, int inversion)
    {
        var voicing = BuildVoicing(stringOneFirstDiagram);

        var doc = VoicingDocumentFactory.FromAnalysis(voicing, VoicingAnalyzer.Analyze(voicing));

        Assert.Multiple(() =>
        {
            Assert.That(doc.RootPitchClass, Is.EqualTo(root));
            Assert.That(doc.MidiBassNote, Is.EqualTo(bass));
            Assert.That(doc.Inversion, Is.EqualTo(inversion));
        });
    }

    [Test]
    public void FromAnalysis_CarriesVoicingConsonance()
    {
        var voicing = BuildVoicing("0-1-0-2-3-x");
        var analysis = VoicingAnalyzer.Analyze(voicing);

        var doc = VoicingDocumentFactory.FromAnalysis(voicing, analysis);

        Assert.That(doc.Consonance, Is.EqualTo(analysis.VoicingCharacteristics.Consonance).And.GreaterThan(0));
    }

    private static Voicing BuildVoicing(string stringOneFirstDiagram)
    {
        var positions = new List<Position>();
        var notes = new List<MidiNote>();
        var parts = stringOneFirstDiagram.Split('-');
        for (var i = 0; i < parts.Length; i++)
        {
            var str = new Str(i + 1);
            if (parts[i] == "x")
            {
                positions.Add(new Position.Muted(str));
                continue;
            }

            var fret = int.Parse(parts[i]);
            var note = new MidiNote(OpenStringMidi[i] + fret);
            positions.Add(new Position.Played(new PositionLocation(str, new Fret(fret)), note));
            notes.Add(note);
        }

        return new Voicing([.. positions], [.. notes]);
    }
}
