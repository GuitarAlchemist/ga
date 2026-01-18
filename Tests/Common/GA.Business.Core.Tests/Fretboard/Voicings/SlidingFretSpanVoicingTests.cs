namespace GA.Business.Core.Tests.Fretboard.Voicings;

using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Analysis;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Notes.Primitives;
using NUnit.Framework;

[TestFixture]
public class SlidingFretSpanVoicingTests
{
    private const int Span = 5;
    private const double CoverageThreshold = 0.65;

    [Test]
    public void SlidingFiveFretWindows_ProduceChordNames()
    {
        var fretboard = Fretboard.Default;
        var maxBase = fretboard.FretCount - Span;
        Assert.That(maxBase, Is.GreaterThanOrEqualTo(Span), "Fretboard must support sliding windows.");

        var missingWindows = new List<int>();
        var windowsAnalyzed = 0;

        for (var baseFret = 0; baseFret <= maxBase; baseFret++)
        {
            var voicing = BuildSlidingVoicing(fretboard, baseFret);
            var analysis = VoicingAnalyzer.Analyze(voicing);
            windowsAnalyzed++;

            var name = analysis.ChordId.ChordName ?? analysis.ChordId.AlternateName;
            if (string.IsNullOrWhiteSpace(name))
            {
                missingWindows.Add(baseFret);
            }
        }

        if (missingWindows.Any())
        {
            TestContext.WriteLine(
                $"Windows missing chord names (> {ClusterThreshold(Span)}): {string.Join(", ", missingWindows)}");
        }

        var coverage = 1.0 - missingWindows.Count / (double)windowsAnalyzed;
        Assert.That(coverage, Is.GreaterThan(CoverageThreshold),
            "Expected most sliding windows to produce a chord name.");
    }

    [Test]
    public void NutWindow_HasChordName()
    {
        var fretboard = Fretboard.Default;
        var voicing = BuildSlidingVoicing(fretboard, 0);
        var analysis = VoicingAnalyzer.Analyze(voicing);

        var chordName = analysis.ChordId.ChordName ?? analysis.ChordId.AlternateName;
        Assert.That(chordName, Is.Not.Null.Or.Empty);
    }

    private static Voicing BuildSlidingVoicing(Fretboard fretboard, int baseFret)
    {
        var positions = new List<Position.Played>();
        var notes = new List<MidiNote>();

        for (var stringIndex = 0; stringIndex < fretboard.StringCount; stringIndex++)
        {
            var fret = baseFret + (stringIndex % Span);
            var str = new Str(stringIndex + 1);
            var tuningNote = fretboard.Tuning[str];
            var midi = tuningNote.MidiNote + fret;
            var location = new PositionLocation(str, new Fret(fret));

            positions.Add(new Position.Played(location, midi));
            notes.Add(midi);
        }

        return new Voicing([.. positions], [.. notes]);
    }

    private static int ClusterThreshold(int span) => span - 1;
}
