namespace GA.Business.Core.Tests.Fretboard.Voicings;

using Domain.Core.Instruments.Fretboard.Voicings.Core;
using Domain.Core.Instruments.Positions;
using Domain.Core.Instruments.Primitives;
using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Voicings.Analysis;

[TestFixture]
public class ProgressionVoiceLeadingAnalyzerTests
{
    [Test]
    public void Analyze_NormalProgressionNoParallelMotion_ShouldBeClean()
    {
        // G-like → C-like simplified to two voices with no parallel fifths/octaves
        // str1: 67(G4)→64(E4), str2: 62(D4)→55(G3)
        // icFrom = |62-67|%12 = 5 (fourth), icTo = |55-64|%12 = 9 (sixth) → no match
        var from = CreateTwoVoiceVoicing(1, 67, 2, 62);
        var to   = CreateTwoVoiceVoicing(1, 64, 2, 55);

        var report = ProgressionVoiceLeadingAnalyzer.Analyze([from, to]);

        Assert.That(report.IsClean, Is.True);
    }

    [Test]
    public void Analyze_TwoVoicesBothMovingAFifthApart_ShouldDetectParallelFifths()
    {
        // str1: C4(60)→G4(67), str2: G4(67)→D5(74)
        // Both move up; icFrom = |67-60|%12 = 7, icTo = |74-67|%12 = 7 → parallel fifths
        var from = CreateTwoVoiceVoicing(1, 60, 2, 67);
        var to   = CreateTwoVoiceVoicing(1, 67, 2, 74);

        var report = ProgressionVoiceLeadingAnalyzer.Analyze([from, to]);

        Assert.That(report.HasParallelFifths, Is.True);
        Assert.That(report.IsClean, Is.False);
    }

    [Test]
    public void Analyze_TwoVoicesBothMovingAnOctaveApart_ShouldDetectParallelOctaves()
    {
        // str1: C4(60)→F4(65), str2: C5(72)→F5(77)
        // Both move up; icFrom = |72-60|%12 = 0, icTo = |77-65|%12 = 0 → parallel octaves
        var from = CreateTwoVoiceVoicing(1, 60, 2, 72);
        var to   = CreateTwoVoiceVoicing(1, 65, 2, 77);

        var report = ProgressionVoiceLeadingAnalyzer.Analyze([from, to]);

        Assert.That(report.HasParallelOctaves, Is.True);
        Assert.That(report.IsClean, Is.False);
    }

    [Test]
    public void Analyze_ContraryMotion_ShouldBeClean()
    {
        // str1 moves up (60→67), str2 moves down (67→60) — opposite directions
        var from = CreateTwoVoiceVoicing(1, 60, 2, 67);
        var to   = CreateTwoVoiceVoicing(1, 67, 2, 60);

        var report = ProgressionVoiceLeadingAnalyzer.Analyze([from, to]);

        Assert.That(report.IsClean, Is.True);
    }

    [Test]
    public void Analyze_ObliqueMotion_ShouldBeClean()
    {
        // str1 stationary (60→60), str2 moves up (67→72) — oblique motion
        var from = CreateTwoVoiceVoicing(1, 60, 2, 67);
        var to   = CreateTwoVoiceVoicing(1, 60, 2, 72);

        var report = ProgressionVoiceLeadingAnalyzer.Analyze([from, to]);

        Assert.That(report.IsClean, Is.True);
    }

    [Test]
    public void Analyze_EmptyProgression_ShouldBeClean()
    {
        var singleVoicing = CreateTwoVoiceVoicing(1, 60, 2, 67);

        var reportEmpty  = ProgressionVoiceLeadingAnalyzer.Analyze([]);
        var reportSingle = ProgressionVoiceLeadingAnalyzer.Analyze([singleVoicing]);

        Assert.That(reportEmpty.IsClean,  Is.True);
        Assert.That(reportSingle.IsClean, Is.True);
    }

    [Test]
    public void Analyze_ThreeChordProgressionParallelFifthsInSecondTransitionOnly_ShouldReturnOneIssue()
    {
        // Chord 1: str1=C4(60), str2=F4(65) — fourth apart
        // Chord 2: str1=F4(65), str2=C5(72) — fifth apart
        //   Transition 1→2: str1 60→65 (up), str2 65→72 (up); icFrom=5 (fourth) ≠ 7 → no match
        // Chord 3: str1=C5(72), str2=G5(79) — fifth apart
        //   Transition 2→3: str1 65→72 (up), str2 72→79 (up); icFrom=7 (fifth), icTo=7 → parallel fifths!
        var chord1 = CreateTwoVoiceVoicing(1, 60, 2, 65);
        var chord2 = CreateTwoVoiceVoicing(1, 65, 2, 72);
        var chord3 = CreateTwoVoiceVoicing(1, 72, 2, 79);

        var report = ProgressionVoiceLeadingAnalyzer.Analyze([chord1, chord2, chord3]);

        Assert.That(report.Issues.Count, Is.EqualTo(1));
        Assert.That(report.HasParallelFifths, Is.True);
        Assert.That(report.HasParallelOctaves, Is.False);
    }

    [Test]
    public void DetectParallelMotion_BothVoicesStationary_ShouldBeClean()
    {
        // Neither voice moves — no motion at all
        var voicing = CreateTwoVoiceVoicing(1, 60, 2, 67);

        var issues = ProgressionVoiceLeadingAnalyzer.DetectParallelMotion(voicing, voicing);

        Assert.That(issues, Is.Empty);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Voicing CreateTwoVoiceVoicing(int strA, int midiA, int strB, int midiB)
    {
        var posA = new Position.Played(new PositionLocation(new Str(strA), new Fret(0)), new MidiNote(midiA));
        var posB = new Position.Played(new PositionLocation(new Str(strB), new Fret(0)), new MidiNote(midiB));
        return new Voicing([posA, posB], [new MidiNote(midiA), new MidiNote(midiB)]);
    }
}
