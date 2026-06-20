namespace GA.Business.ML.Tests;

using System.Collections.Generic;
using Rag.Models;
using Musical.Enrichment;
using NUnit.Framework;

[TestFixture]
public class CoverAllModesIntegrationTests
{
    private AutoTaggingService _autoTaggingService;
    private ModalFlavorService _modalFlavorService;

    [SetUp]
    public void Setup()
    {
        _modalFlavorService = new();
        _autoTaggingService = new(_modalFlavorService);
    }

    /// <summary>
    /// Consolidated test for various voicing identification scenarios.
    /// Uses TestCaseSource for clean parameterization of many scenarios.
    /// </summary>
    [Test]
    [TestCaseSource(nameof(IdentificationScenarios))]
    public void Test_Voicing_Identification(
        string description,
        int[] pitchClasses,
        int root,
        string[] expectedTags,
        string[]? excludedTags = null)
    {
        var doc = new ChordVoicingRagDocument
        {
            Id = "test_voicing",
            SearchableText = description,
            RootPitchClass = root,
            PitchClasses = pitchClasses,
            ChordName = description,
            Consonance = 0.5,
            Embedding = new float[216],

            // Required placeholders
            MidiNotes = [],
            SemanticTags = [],
            PossibleKeys = [],
            YamlAnalysis = "{}",
            PitchClassSet = string.Join(",", pitchClasses),
            IntervalClassVector = "000000",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "0",
            Diagram = "x00000",
            BarreRequired = false,
            HandStretch = 0,
            MinFret = 0,
            MaxFret = 0
        };

        var tags = _autoTaggingService.GenerateTags(doc);

        foreach (var expected in expectedTags)
        {
            Assert.That(tags, Does.Contain(expected), $"[{description}] Expected tag '{expected}' missing.");
        }

        if (excludedTags != null)
        {
            foreach (var excluded in excludedTags)
            {
                Assert.That(tags, Does.Not.Contain(excluded), $"[{description}] Excluded tag '{excluded}' should not be present.");
            }
        }
    }

    public static IEnumerable<TestCaseData> IdentificationScenarios()
    {
        yield return new(
            "C Major (Ionian)",
            new[] { 0, 2, 4, 5, 7, 9, 11 },
            0,
            new[] { "Set:7-35", "Flavor:Ionian" }, // 7-35 = canonical Forte for the diatonic collection
            new[] { "Flavor:Lydian" }
        );

        yield return new(
            "C Altered (Super Locrian)",
            new[] { 0, 1, 3, 4, 6, 8, 10 },
            0,
            new[] { "Set:7-34", "Flavor:Altered" }, // 7-34 = canonical Forte (melodic-minor collection)
            null
        );

        yield return new(
            "Whole Tone",
            new[] { 0, 2, 4, 6, 8, 10 },
            0,
            new[] { "Set:6-35", "Flavor:Whole-tone" }, // 6-35 = canonical Forte for the whole-tone scale
            null
        );

        yield return new(
            "Vienna Trichord",
            new[] { 0, 1, 6 },
            0,
            new[] { "Set:3-5" }, // {0,1,6} Viennese trichord = canonical Forte 3-5
            new[] { "Flavor:Ionian", "Flavor:Major" }
        );

        yield return new(
            "C7 (Mixolydian check)",
            new[] { 0, 4, 7, 10 },
            0,
            new[] { "Flavor:Mixolydian" },
            new[] { "Flavor:Enigmatic Lydian" }
        );

        yield return new(
            "Chromatic Cluster",
            new[] { 0, 1, 2 },
            0,
            new[] { "Set:3-1" }, // {0,1,2} chromatic cluster = canonical Forte 3-1
            new[] { "Flavor:Ionian", "Flavor:Major", "Flavor:Dorian" }
        );

        yield return new(
            "Pentatonic Major",
            new[] { 0, 2, 4, 7, 9 },
            0,
            new[] { "Set:5-35" }, // {0,2,4,7,9} major pentatonic = canonical Forte 5-35
            null // Accepts both Pentatonic Major and Ionian
        );

        yield return new(
            "Power Chord (Sparse)",
            new[] { 0, 7 },
            0,
            new[] { "Power Chord", "Set:2-5" }, // {0,7}→prime {0,5} = canonical Forte 2-5
            new[] { "Flavor:Altered", "Flavor:Locrian" }
        );
    }
}
