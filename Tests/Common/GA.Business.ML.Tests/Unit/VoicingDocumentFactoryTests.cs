namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Rag;
using GA.Business.ML.Rag.Models;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     Document-level checks of the fields the OPTIC-K embedding reads (ROOT, MODAL, MORPHOLOGY bass,
///     CONTEXT tension) for open chords whose answers every guitarist knows.
/// </summary>
[TestFixture]
public class VoicingDocumentFactoryTests
{
    // Standard tuning, string 1 (high E) first — the order VoicingGenerator and the index use.
    private static readonly int[] OpenStringMidi = [64, 59, 55, 50, 45, 40];

    [TestCase("0-1-0-2-3-x", 0, 48, 0, TestName = "Open C major (x32010)")]
    [TestCase("0-0-0-2-2-0", 4, 40, 0, TestName = "Open E minor (022000)")]
    [TestCase("0-1-2-2-0-x", 9, 45, 0, TestName = "Open A minor (x02210)")]
    [TestCase("3-0-0-0-2-3", 7, 43, 0, TestName = "Open G major (320003)")]
    [TestCase("0-1-0-2-3-0", 0, 40, 1, TestName = "C major over E (032010)")]
    public void RootBassAndInversion_ComeFromTheChordRootAndTheLowestNote(
        string indexDiagram, int expectedRoot, int expectedBassMidi, int expectedInversion)
    {
        var doc = Document(indexDiagram);

        Assert.Multiple(() =>
        {
            Assert.That(doc.RootPitchClass, Is.EqualTo(expectedRoot), "RootPitchClass");
            Assert.That(doc.MidiBassNote, Is.EqualTo(expectedBassMidi), "MidiBassNote");
            Assert.That(doc.Inversion, Is.EqualTo(expectedInversion), "Inversion");
        });
    }

    [Test]
    public void TheOrderOfTheMidiNotes_DoesNotChangeRootOrBass()
    {
        var voicing = BuildVoicing("0-1-0-2-3-x");
        var analysis = VoicingAnalyzer.Analyze(voicing);
        var reversed = analysis with { MidiNotes = [.. analysis.MidiNotes.Reverse()] };

        var a = VoicingDocumentFactory.FromAnalysis(voicing, analysis);
        var b = VoicingDocumentFactory.FromAnalysis(voicing, reversed);

        Assert.Multiple(() =>
        {
            Assert.That(b.RootPitchClass, Is.EqualTo(a.RootPitchClass));
            Assert.That(b.MidiBassNote, Is.EqualTo(a.MidiBassNote));
            Assert.That(b.Inversion, Is.EqualTo(a.Inversion));
        });
    }

    [Test]
    public void Consonance_IsTheAnalyzersConsonance_NotZero()
    {
        var voicing = BuildVoicing("0-1-0-2-3-x");
        var analysis = VoicingAnalyzer.Analyze(voicing);
        var doc = VoicingDocumentFactory.FromAnalysis(voicing, analysis);

        Assert.Multiple(() =>
        {
            Assert.That(analysis.VoicingCharacteristics.Consonance, Is.GreaterThan(0));
            Assert.That(doc.Consonance, Is.EqualTo(analysis.VoicingCharacteristics.Consonance));
        });
    }

    [Test]
    public void AVoicingWiderThanAnOctave_IsOpen_NotRootless()
    {
        // Open C spans C3-E4 (16 semitones) and contains its root.
        var doc = Document("0-1-0-2-3-x");

        Assert.Multiple(() =>
        {
            Assert.That(doc.IsRootless, Is.False, "IsRootless");
            Assert.That(doc.SearchableText, Does.Contain("open voicing"));
            Assert.That(doc.SearchableText, Does.Not.Contain("rootless"));
        });
    }

    private static ChordVoicingRagDocument Document(string indexDiagram)
    {
        var voicing = BuildVoicing(indexDiagram);
        return VoicingDocumentFactory.FromAnalysis(voicing, VoicingAnalyzer.Analyze(voicing));
    }

    private static Voicing BuildVoicing(string indexDiagram)
    {
        var parts = indexDiagram.Split('-');
        var positions = new List<Position>();
        var notes = new List<MidiNote>();
        for (var i = 0; i < parts.Length; i++)
        {
            var str = new Str(i + 1);
            if (parts[i] is "x")
            {
                positions.Add(new Position.Muted(str));
                continue;
            }

            var fret = int.Parse(parts[i]);
            var midiNote = new MidiNote(OpenStringMidi[i] + fret);
            positions.Add(new Position.Played(new PositionLocation(str, new Fret(fret)), midiNote));
            notes.Add(midiNote);
        }

        return new Voicing([.. positions], [.. notes]);
    }
}
