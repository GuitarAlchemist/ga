namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Rag.Models;
using GA.Business.ML.Search;

/// <summary>
///     The <c>ChordName</c> hard filter of <see cref="OptickSearchStrategy"/> against the chord names
///     the OPTK index actually stores (<c>ChordId.ChordName</c>: root, quality, optional voicing tag and
///     bass), e.g. <c>Gbm7(shell)/A</c> or <c>C + E (Major 3rd)</c> as written by FretboardVoicingsCLI.
/// </summary>
[TestFixture]
public class OptickChordFilterTests
{
    private static VoicingSearchResult Row(string chordName, string diagram, params int[] midi) =>
        new(new ChordVoicingRagDocument
        {
            SearchableText = diagram,
            ChordName = chordName,
            VoicingType = "guitar",
            Diagram = diagram,
            MidiNotes = midi,
            PitchClasses = [],
            PitchClassSet = "{}",
            SemanticTags = [],
            PossibleKeys = [],
            PrimeFormId = "",
            PitchClassSetId = "",
            TranslationOffset = 0,
            AnalysisEngine = "test",
            AnalysisVersion = "test",
            Jobs = [],
            TuningId = "Standard",
            YamlAnalysis = "",
            IntervalClassVector = "",
            DifficultyScore = 1.0,
        }, 0.5, "test");

    private static string[] Filter(string chord, params VoicingSearchResult[] rows) =>
        [.. OptickSearchStrategy.ApplyFilters(rows, new VoicingSearchFilters(ChordName: chord)).Select(r => r.Document.ChordName!)];

    [Test]
    public void Am7_RejectsAnotherChordWithAnABass_AndKeepsAm7Inversions()
    {
        var names = Filter("Am7",
            Row("Gbm7(shell)/A", "2-x-x-2-0-x", 66, 52, 45),
            Row("Am7(shell)/G", "x-x-2-x-3-3", 57, 48, 43),
            Row("Am7", "0-1-0-2-0-x", 64, 60, 55, 52, 45),
            Row("Am/C", "x-x-2-2-3-x", 57, 52, 48));

        Assert.That(names, Is.EqualTo(new[] { "Am7(shell)/G", "Am7" }));
    }

    [Test]
    public void C_MatchesOnlyCMajorTriads_NotDyadsPowerChordsOrOtherChordsOverC()
    {
        var names = Filter("C",
            Row("C", "x-1-0-2-3-x", 60, 55, 52, 48),
            Row("C + E (Major 3rd)", "x-1-x-2-3-x", 60, 52, 48),
            Row("C5", "x-1-0-x-3-x", 60, 55, 48),
            Row("Am/C", "x-1-2-2-3-x", 60, 57, 52, 48),
            Row("C/E", "x-1-0-2-x-0", 60, 55, 52, 40),
            Row("Cmaj7", "3-0-x-2-3-x", 67, 59, 52, 48));

        Assert.That(names, Is.EqualTo(new[] { "C", "C/E" }));
    }

    [Test]
    public void Cmaj7_MatchesShellsAndInversions_AndAliasesAndEnharmonicsCompareEqual()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Filter("Cmaj7",
                    Row("Cmaj7(shell)/B", "x-1-x-2-2-x", 60, 52, 47),
                    Row("Cmaj7", "3-0-x-2-3-x", 67, 59, 52, 48),
                    Row("C7", "x-1-3-2-3-x", 60, 58, 52, 48)),
                Is.EqualTo(new[] { "Cmaj7(shell)/B", "Cmaj7" }));
            Assert.That(Filter("CM7", Row("Cmaj7", "3-0-x-2-3-x", 67, 59, 52, 48)), Has.Length.EqualTo(1));
            Assert.That(Filter("CMaj7", Row("Cmaj7", "3-0-x-2-3-x", 67, 59, 52, 48)), Has.Length.EqualTo(1));
            Assert.That(Filter("AMin7", Row("Am7", "0-1-0-2-0-x", 64, 60, 55, 52, 45)), Has.Length.EqualTo(1));
            Assert.That(Filter("F#m7", Row("Gbm7(shell)/A", "2-x-x-2-0-x", 66, 52, 45)), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public void SlashBassInTheFilter_RequiresThatLowestNote()
    {
        var names = Filter("C/E",
            Row("C", "x-1-0-2-3-x", 60, 55, 52, 48),
            Row("C/E", "x-1-0-2-x-0", 60, 55, 52, 40));

        Assert.That(names, Is.EqualTo(new[] { "C/E" }));
    }
}
