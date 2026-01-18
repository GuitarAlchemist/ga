namespace GA.Business.ML.Tests;

using System.Collections.Generic;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Musical.Enrichment;
using NUnit.Framework;

[TestFixture]
public class ModalFlavorTests
{
    private ModalFlavorService _service;

    [SetUp]
    public void Setup()
    {
        // This relies on Modes.yaml being copied to output dir
        _service = new();
    }

    [Test]
    public void Test_Enrich_Lydian()
    {
        // C Lydian Voicing: C E G B F#
        // Root: C (0)
        // Intervals: 0(1), 4(3), 7(5), 11(7), 6(#4)
        // #4 is the characteristic interval of Lydian

        var doc = new VoicingDocument
        {
            Id = "test_lydian",
            SearchableText = "test",
            RootPitchClass = 0,
            PitchClasses = new[] { 0, 4, 7, 11, 6 }, // Cmaj7#11

            // Required props
            Diagram = "x32000",
            MidiNotes = [],
            SemanticTags = [],
            PossibleKeys = [],
            YamlAnalysis = "",
            PitchClassSet = "",
            IntervalClassVector = "",
            AnalysisEngine = "",
            AnalysisVersion = "",
            Jobs = [],
            TuningId = "",
            PitchClassSetId = ""
        };

        var tags = new HashSet<string>();
        _service.Enrich(doc, tags);

        Assert.That(tags, Does.Contain("Flavor:Lydian"), "Should detect Lydian flavor due to #4");
    }

    [Test]
    public void Test_Enrich_Phrygian()
    {
        // C Phrygian: C Db ...
        // Interval: 1 (b2)

        var doc = new VoicingDocument
        {
            Id = "test_phryg",
            SearchableText = "test",
            RootPitchClass = 0,
            PitchClasses = new[] { 0, 1, 7 }, // C5(b9)

            // Required props
            Diagram = "x32000", MidiNotes = [], SemanticTags = [], PossibleKeys = [], YamlAnalysis = "", PitchClassSet = "", IntervalClassVector = "", AnalysisEngine = "", AnalysisVersion = "", Jobs = [], TuningId = "", PitchClassSetId = ""
        };

        var tags = new HashSet<string>();
        _service.Enrich(doc, tags);

        Assert.That(tags, Does.Contain("Flavor:Phrygian"), "Should detect Phrygian flavor due to b2");
    }

    [Test]
    public void Test_Enrich_Dominant_NotLydian()
    {
        // C7: C E G Bb
        // Intervals: 0, 4, 7, 10(b7).
        // Lydian has 7 (Maj7). Mixolydian has b7.
        // Should be Mixolydian, NOT Lydian.

        var doc = new VoicingDocument
        {
            Id = "test_dom",
            SearchableText = "test",
            RootPitchClass = 0,
            PitchClasses = new[] { 0, 4, 7, 10 },

            // Required props
            Diagram = "x32310", MidiNotes = [], SemanticTags = [], PossibleKeys = [], YamlAnalysis = "", PitchClassSet = "", IntervalClassVector = "", AnalysisEngine = "", AnalysisVersion = "", Jobs = [], TuningId = "", PitchClassSetId = ""
        };

        var tags = new HashSet<string>();
        _service.Enrich(doc, tags);

        Assert.That(tags, Does.Not.Contain("Flavor:Lydian"));
        Assert.That(tags, Does.Contain("Flavor:Mixolydian"));
    }
}
