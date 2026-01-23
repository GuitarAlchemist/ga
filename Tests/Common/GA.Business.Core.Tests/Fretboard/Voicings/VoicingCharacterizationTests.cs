namespace GA.Domain.Core.Tests.Fretboard.Voicings;

using GA.Domain.Core.Primitives.Extensions;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives;
using GA.Domain.Services.Fretboard.Voicings.Analysis;
using NUnit.Framework;

[TestFixture]
public class VoicingCharacterizationTests
{
    [Test]
    public void Drop2Voicing_IsClassified()
    {
        var midiValues = new[] { 40, 48, 50, 53, 57, 60 };
        var voicing = BuildVoicingFromMidiValues(midiValues);
        var analysis = VoicingAnalyzer.Analyze(voicing);
        Assert.That(analysis.VoicingCharacteristics.DropVoicing, Is.EqualTo("Drop-2"));
        Assert.That(analysis.SemanticTags, Contains.Item("drop-2"));
    }
    [Test]
    public void SlashChord_DetectsInversion_WhenAvailable()
    {
        var midiValues = new[] { 59, 64, 67, 71, 74 };
        var voicing = BuildVoicingFromMidiValues(midiValues);
        var analysis = VoicingAnalyzer.Analyze(voicing);
        var slashInfo = analysis.ChordId.SlashChordInfo;
        if (slashInfo != null)
        {
            Assert.That(slashInfo.ToString(), Does.Contain("/"));
        }
        Assert.That(analysis.ChordId.ChordName, Is.Not.Null.Or.Empty);
        Assert.That(analysis.PlayabilityInfo.Difficulty, Is.Not.Null.Or.Empty);
    }
    [Test]
    public void ClusterVoicing_ReportsClusterFeature()
    {
        var midiValues = new[] { 60, 61, 62, 64, 65 };
        var voicing = BuildVoicingFromMidiValues(midiValues);
        var analysis = VoicingAnalyzer.Analyze(voicing);
        Assert.That(analysis.IntervallicInfo.Features, Contains.Item("Cluster (3 semitones)"));
    }
    private static Voicing BuildVoicingFromMidiValues(IReadOnlyList<int> midiValues)
    {
        var positions = new List<Position.Played>();
        var notes = new List<MidiNote>();
        for (var i = 0; i < midiValues.Count; i++)
        {
            var str = new Str((i % 6) + 1);
            var fret = new Fret((i % 5) + 1);
            var location = new PositionLocation(str, fret);
            var midi = new MidiNote(midiValues[i]);
            positions.Add(new Position.Played(location, midi));
            notes.Add(midi);
        }
        return new Voicing([.. positions], [.. notes]);
    }
}