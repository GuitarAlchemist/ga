namespace GA.Business.Core.Tests.Fretboard.Voicings;

using Domain.Core.Instruments.Fretboard.Voicings.Core;
using Domain.Core.Instruments.Positions;
using Domain.Core.Instruments.Primitives;
using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Voicings.Analysis;
using Domain.Services.Fretboard.Voicings.Filtering;

[TestFixture]
public class VoicingCharacterizationTests
{
    [Test]
    public void Drop2Voicing_IsClassified()
    {
        // x3545x, Cmaj7: C3 G3 B3 E4, the close position G3 B3 C4 E4 with C4, its second voice from the top, an octave lower
        var midiValues = new[] { 48, 55, 59, 64 };
        var voicing = BuildVoicingFromMidiValues(midiValues);
        var analysis = VoicingAnalyzer.Analyze(voicing);
        Assert.That(analysis.VoicingCharacteristics.DropVoicing, Is.EqualTo("Drop-2"));
        Assert.That(analysis.SemanticTags, Contains.Item("drop-2"));
    }

    // Cmaj7 under B5: the close position C5 E5 G5 B5, with some of its voices, numbered from the top, an octave lower
    [TestCase(new[] { 72, 76, 79, 83 }, null)]
    [TestCase(new[] { 67, 72, 76, 83 }, "Drop-2")]
    [TestCase(new[] { 64, 72, 79, 83 }, "Drop-3")]
    [TestCase(new[] { 60, 76, 79, 83 }, "Drop-4")]
    [TestCase(new[] { 64, 67, 72, 83 }, "Drop-2+3")]
    [TestCase(new[] { 60, 67, 76, 83 }, "Drop-2+4")]
    [TestCase(new[] { 48, 76, 79, 83 }, null)]         // C3, two octaves below its place
    [TestCase(new[] { 40, 48, 50, 53, 57, 60 }, null)] // E2 C3 D3 F3 A3 C4: C is doubled
    public void DropVoicing_NamesTheVoicesLoweredAnOctave(int[] midiValues, string? expected)
    {
        var analysis = VoicingAnalyzer.Analyze(BuildVoicingFromMidiValues(midiValues));
        Assert.That(analysis.VoicingCharacteristics.DropVoicing, Is.EqualTo(expected));
    }

    [TestCase(VoicingTypeFilter.Drop2, new[] { 67, 72, 76, 83 })]
    [TestCase(VoicingTypeFilter.Drop3, new[] { 64, 72, 79, 83 })]
    [TestCase(VoicingTypeFilter.Drop2And4, new[] { 60, 67, 76, 83 })]
    public void DropFilter_KeepsItsDropVoicingOnly(VoicingTypeFilter filter, int[] kept)
    {
        // The Cmaj7 voicings above: close, drop 2, drop 3, drop 4, drop 2 and 3, drop 2 and 4
        int[][] cmaj7 = [[72, 76, 79, 83], [67, 72, 76, 83], [64, 72, 79, 83], [60, 76, 79, 83], [64, 67, 72, 83], [60, 67, 76, 83]];
        Assert.That(cmaj7.Where(midiValues => Keeps(midiValues, filter)).ToArray(), Is.EqualTo(new[] { kept }));
    }

    [TestCase(new[] { 43, 53, 59 }, true)]      // 3x34xx, G7: root, seventh, third
    [TestCase(new[] { 43, 54, 59 }, true)]      // 3x44xx, Gmaj7
    [TestCase(new[] { 48, 51, 58 }, true)]      // x313xx, Cm7: root, third, seventh
    [TestCase(new[] { 48, 52, 59 }, true)]      // x324xx, Cmaj7
    [TestCase(new[] { 62, 65, 68 }, false)]     // D F A♭: no seventh
    [TestCase(new[] { 52, 58, 60 }, false)]     // E B♭ C: C7's third and seventh under its root
    [TestCase(new[] { 48, 52, 55, 59 }, false)] // x3200x, Cmaj7 with its fifth
    [TestCase(new[] { 50, 57, 62, 66 }, false)] // xx0232, D
    public void ShellVoicing_IsTheRootWithAThirdAndASeventh(int[] midiValues, bool isShell)
    {
        var analysis = VoicingAnalyzer.Analyze(BuildVoicingFromMidiValues(midiValues));
        Assert.Multiple(() =>
        {
            Assert.That(analysis.SemanticTags.Contains("shell-voicing"), Is.EqualTo(isShell), "the shell-voicing tag");
            Assert.That(Keeps(midiValues, VoicingTypeFilter.ShellVoicings), Is.EqualTo(isShell), "the ShellVoicings filter");
        });
    }

    [Test]
    public void SlashChord_DetectsInversion_WhenAvailable()
    {
        var midiValues = new[] { 59, 64, 67, 71, 74 };
        var voicing = BuildVoicingFromMidiValues(midiValues);
        var analysis = VoicingAnalyzer.Analyze(voicing);
        var slashInfo = analysis.ChordId.SlashSuffix;
        if (slashInfo != null)
        {
            Assert.That(slashInfo, Does.Contain("/"));
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
        Assert.That(analysis.IntervallicInfo.Features, Contains.Item("Cluster (2 semitones)"));
    }

    private static bool Keeps(int[] midiValues, VoicingTypeFilter filter)
    {
        var voicing = BuildVoicingFromMidiValues(midiValues);
        return VoicingFilters.MatchesCriteria(voicing, VoicingAnalyzer.Analyze(voicing), new VoicingFilterCriteria { VoicingType = filter });
    }

    private static Voicing BuildVoicingFromMidiValues(IReadOnlyList<int> midiValues)
    {
        var positions = new List<Position.Played>();
        var notes = new List<MidiNote>();
        for (var i = 0; i < midiValues.Count; i++)
        {
            var str = new Str(i % 6 + 1);
            var fret = new Fret(i % 5 + 1);
            var location = new PositionLocation(str, fret);
            var midi = new MidiNote(midiValues[i]);
            positions.Add(new(location, midi));
            notes.Add(midi);
        }

        return new([.. positions], [.. notes]);
    }
}
