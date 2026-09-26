namespace GA.Business.Core.Tests.Fretboard.Voicings;

using Business.Core.Analysis.Voicings;
using Domain.Core.Instruments.Fretboard.Voicings.Core;
using Domain.Core.Instruments.Positions;
using Domain.Core.Instruments.Primitives;
using Domain.Core.Primitives.Notes;
using Domain.Services.Fretboard.Voicings.Analysis;

/// <summary>
///     Playability of canonical guitar shapes. Charts are written the way guitarists read them,
///     low E first ("x32010" is open C); the voicing itself stores string 1 (high E) first.
/// </summary>
[TestFixture]
[Category("Unit")]
public class VoicingPhysicalAnalyzerTests
{
    private static readonly int[] _standardTuning = [64, 59, 55, 50, 45, 40]; // string 1 first

    // ── Barre ────────────────────────────────────────────────────────────────

    [TestCase("133211", 1, TestName = "E-shape F barre")]
    [TestCase("x13331", 1, TestName = "A-shape Bb barre")]
    [TestCase("x24432", 2, TestName = "A-minor-shape Bm barre")]
    [TestCase("355433", 3, TestName = "E-shape G barre")]
    [TestCase("x35543", 3, TestName = "A-minor-shape Cm barre")]
    public void BarreChord_IsDetected_AtTheIndexFingerFret(string chart, int barreFret)
    {
        var playability = Playability(chart);

        Assert.Multiple(() =>
        {
            Assert.That(playability.BarreRequired, Is.True, chart);
            Assert.That(playability.BarreInfo, Is.EqualTo($"Fret {barreFret} barre"), chart);
        });
    }

    [TestCase("x32010", TestName = "Open C")]
    [TestCase("320003", TestName = "Open G")]
    [TestCase("xx0232", TestName = "Open D")]
    [TestCase("022100", TestName = "Open E")]
    [TestCase("x02220", TestName = "Open A")]
    [TestCase("x02210", TestName = "Open Am")]
    [TestCase("xx3211", TestName = "Four-string F")]
    [TestCase("x355xx", TestName = "Power chord")]
    [TestCase("1x2x1x", TestName = "Sparse grip held by three fingers")]
    public void OpenOrPartialShape_IsNotABarreChord(string chart)
    {
        var playability = Playability(chart);

        Assert.Multiple(() =>
        {
            Assert.That(playability.BarreRequired, Is.False, chart);
            Assert.That(playability.BarreInfo, Is.Null, chart);
        });
    }

    // ── Minimum fingers ──────────────────────────────────────────────────────

    [TestCase("x32010", 3, TestName = "Open C needs 3 fingers")]
    [TestCase("320003", 3, TestName = "Open G needs 3 fingers")]
    [TestCase("x02010", 2, TestName = "Am7 needs 2 fingers")]
    [TestCase("xx3211", 3, TestName = "Four-string F needs 3 fingers (index across strings 1-2)")]
    [TestCase("x13331", 2, TestName = "A-shape Bb needs 2 fingers (ring finger across fret 3)")]
    [TestCase("x12345", 5, TestName = "Five frets need 5 fingers")]
    [TestCase("103212", 5, TestName = "An open string splits both frets 1 and 2")]
    [TestCase("x02x20", 1, TestName = "A muted string does not split a finger")]
    public void MinimumFingers_CountsOneFingerPerNoteGroupAFingerCanHold(string chart, int expected)
    {
        Assert.That(Playability(chart).MinimumFingers, Is.EqualTo(expected), chart);
    }

    // ── Unplayable voicings ──────────────────────────────────────────────────

    [TestCase("x12345")]
    [TestCase("103212")]
    public void VoicingNeedingMoreThanFourFingers_ScoresAsUnplayable(string chart)
    {
        var layout = Layout(chart);
        var playability = VoicingPhysicalAnalyzer.CalculatePlayability(layout);
        var ergonomics = VoicingPhysicalAnalyzer.AnalyzeErgonomics(layout, playability);

        Assert.Multiple(() =>
        {
            Assert.That(playability.DifficultyScore, Is.EqualTo(10.0), chart); // the top of the 1-10 scale
            // GaApi's "maxDifficulty: 8" filter must not return it
            Assert.That(playability.DifficultyScore, Is.GreaterThan(8.0), chart);
            Assert.That(ergonomics.IsImpossible, Is.True, chart);
        });
    }

    [TestCase("x13331")]
    [TestCase("133211")]
    [TestCase("x32010")]
    public void PlayableVoicing_IsNotFlaggedImpossible(string chart)
    {
        var layout = Layout(chart);
        var playability = VoicingPhysicalAnalyzer.CalculatePlayability(layout);

        Assert.That(VoicingPhysicalAnalyzer.AnalyzeErgonomics(layout, playability).IsImpossible, Is.False, chart);
    }

    // ── Label versus score ───────────────────────────────────────────────────

    [TestCase("x32010", "Beginner", TestName = "Open C is Beginner")]
    [TestCase("x02210", "Beginner", TestName = "Open Am is Beginner")]
    [TestCase("133211", "Intermediate", TestName = "F barre is Intermediate")]
    public void DifficultyLabel_OfCanonicalShapes(string chart, string expected)
    {
        Assert.That(Playability(chart).Difficulty, Is.EqualTo(expected), chart);
    }

    [Test]
    public void DifficultyLabel_NeverRanksAHigherScoreAsEasier()
    {
        // Every six-string combination of muted, open and frets 1-4: 6^6 = 46,656 layouts
        var rank = new Dictionary<string, int> { ["Beginner"] = 0, ["Intermediate"] = 1, ["Advanced"] = 2 };
        var scored = new List<(double Score, int Rank, string Frets)>();
        var frets = new int[6];
        for (var code = 0; code < 46656; code++)
        {
            var c = code;
            for (var s = 0; s < 6; s++, c /= 6) frets[s] = c % 6 - 1;
            var playability = VoicingPhysicalAnalyzer.CalculatePlayability(Layout(frets));
            scored.Add((playability.DifficultyScore, rank[playability.Difficulty], string.Join(",", frets)));
        }

        // Highest easier label must sit below the lowest score of any harder label
        foreach (var harder in new[] { 1, 2 })
        {
            var easier = scored.Where(x => x.Rank < harder).MaxBy(x => x.Score);
            var hard = scored.Where(x => x.Rank >= harder).MinBy(x => x.Score);
            Assert.That(easier.Score, Is.LessThan(hard.Score),
                $"[{easier.Frets}] scores {easier.Score:F2} with an easier label than [{hard.Frets}] at {hard.Score:F2}");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static PlayabilityInfo Playability(string chart) =>
        VoicingPhysicalAnalyzer.CalculatePlayability(Layout(chart));

    private static PhysicalLayout Layout(string chart) => Layout(FromChart(chart));

    private static PhysicalLayout Layout(int[] fretsStringOneFirst) =>
        VoicingPhysicalAnalyzer.ExtractPhysicalLayout(BuildVoicing(fretsStringOneFirst));

    // "x32010": low E first, one character per string -> frets string 1 first
    private static int[] FromChart(string chart) =>
        [.. chart.Reverse().Select(ch => ch is 'x' or 'X' ? -1 : ch - '0')];

    private static Voicing BuildVoicing(int[] fretsStringOneFirst)
    {
        var positions = new List<Position>();
        var notes = new List<MidiNote>();
        for (var i = 0; i < fretsStringOneFirst.Length; i++)
        {
            var str = new Str(i + 1);
            var fret = fretsStringOneFirst[i];
            if (fret < 0)
            {
                positions.Add(new Position.Muted(str));
                continue;
            }

            var midi = new MidiNote(_standardTuning[i] + fret);
            positions.Add(new Position.Played(new PositionLocation(str, new Fret(fret)), midi));
            notes.Add(midi);
        }

        return new([.. positions], [.. notes]);
    }
}
