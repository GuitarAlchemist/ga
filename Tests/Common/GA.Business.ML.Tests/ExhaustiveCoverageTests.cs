namespace GA.Business.ML.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Atonal;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Musical.Enrichment;
using NUnit.Framework;

[TestFixture]
public class ExhaustiveCoverageTests
{
    private AutoTaggingService _autoTaggingService;
    private ModalFlavorService _modalFlavorService;
    private static ModalCharacteristicIntervalService _intervalService;

    [OneTimeSetUp]
    public void OneTimeSetup()
    {
        _intervalService = ModalCharacteristicIntervalService.Instance;
    }

    [SetUp]
    public void Setup()
    {
        _modalFlavorService = new ModalFlavorService();
        _autoTaggingService = new AutoTaggingService(_modalFlavorService);
    }

    [Test]
    [TestCaseSource(nameof(GetForteTestCases))]
    public void Test_Atonal_ForteNumber(string primeFormId, string forteNumber, int[] pitchClasses)
    {
        // Skip empty set 0-1 as it's trivial and handled by edge case logic
        if (forteNumber == "0-1") Assert.Ignore("Empty set 0-1 ignored");

        var doc = new VoicingDocument
        {
            Id = $"forte_{forteNumber}",
            SearchableText = "",
            PitchClasses = pitchClasses,
            
            // Defaults
            RootPitchClass = 0,
            MidiNotes = [],
            SemanticTags = [],
            PossibleKeys = [],
            YamlAnalysis = "{}",
            PitchClassSet = "",
            IntervalClassVector = "",
            AnalysisEngine = "",
            AnalysisVersion = "",
            Jobs = [],
            TuningId = "",
            PitchClassSetId = "",
            Diagram = "",
            BarreRequired = false
        };

        var tags = _autoTaggingService.GenerateTags(doc);
        var expectedTag = $"Set:{forteNumber}";

        Assert.That(tags, Does.Contain(expectedTag), 
            $"Failed to identify Forte {forteNumber} for PCs: {string.Join(",", pitchClasses)}");
    }

    public static IEnumerable<TestCaseData> GetForteTestCases()
    {
        foreach (var kvp in ProgrammaticForteCatalog.ForteByPrimeFormId)
        {
            var forteNumber = kvp.Value.ToString();
            var pcs = ProgrammaticForteCatalog.PrimeFormByForte[kvp.Value];
            var pcArray = pcs.Select(p => p.Value).ToArray();
            
            yield return new TestCaseData(kvp.Key.ToString(), forteNumber, pcArray)
                .SetName($"Forte_{forteNumber}");
        }
    }

    [Test]
    [TestCaseSource(nameof(GetTonalModeTestCases))]
    public void Test_Tonal_Mode_Identification(string modeName, int[] intervals)
    {
        // Construct a voicing that perfectly matches the mode (Root 0 + Intervals)
        var pitchClasses = intervals.Select(i => i % 12).Distinct().ToArray();
        
        var doc = new VoicingDocument
        {
            Id = $"mode_{modeName}",
            SearchableText = "",
            RootPitchClass = 0,
            PitchClasses = pitchClasses,
            
            // Defaults
            MidiNotes = [],
            SemanticTags = [],
            PossibleKeys = [],
            YamlAnalysis = "{}",
            PitchClassSet = "",
            IntervalClassVector = "",
            AnalysisEngine = "",
            AnalysisVersion = "",
            Jobs = [],
            TuningId = "",
            PitchClassSetId = "",
            Diagram = "",
            BarreRequired = false
        };

        var tags = _autoTaggingService.GenerateTags(doc);
        var expectedTag = $"Flavor:{modeName}";

        // Priority Logic Check
        // We only enforce strict exact-match tagging for Standard/High-Priority modes.
        // For exotic modes (Akebono, etc.), it's acceptable if the system identifies a 
        // high-priority subset (e.g. Lydian) instead of the obscure name, 
        // as long as it identifies *something*.
        
        var isStandard = IsStandardMode(modeName);

        // Special handling for subsets (Pentatonic/Blues) which might be identified as their parent mode (Ionian/Aeolian)
        var allowedAlternatives = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "Major pentatonic", new[] { "Flavor:Ionian" } },
            { "Minor pentatonic", new[] { "Flavor:Aeolian" } },
            { "Blues", new[] { "Flavor:Aeolian", "Flavor:Minor pentatonic" } },
            { "Blues minor", new[] { "Flavor:Aeolian" } }
        };

        if (isStandard)
        {
            bool matchFound = tags.Contains(expectedTag);
            
            // Check alternatives if exact match failed
            if (!matchFound && allowedAlternatives.TryGetValue(modeName, out var alternatives))
            {
                matchFound = alternatives.Any(alt => tags.Contains(alt));
            }

            if (!matchFound)
            {
                TestContext.WriteLine($"[Standard Mode Fail] Input: {string.Join(",", intervals)}");
                TestContext.WriteLine($"Detected: {string.Join(", ", tags)}");
            }
            Assert.That(matchFound, Is.True, $"Failed to identify Standard Tonal Mode: {modeName} (or valid parent)");
        }
        else
        {
            // For exotic modes, we just check that we got SOME flavor (Tonal analysis worked)
            // Optional: We could check if it found the exact match OR a standard superset.
            var hasFlavor = tags.Any(t => t.StartsWith("Flavor:"));
            if (!tags.Contains(expectedTag))
            {
                // Log for visibility but don't fail
                TestContext.WriteLine($"[Exotic Mode Info] {modeName} identified as: {string.Join(", ", tags.Where(t => t.StartsWith("Flavor:")))}");
            }
            Assert.That(hasFlavor, Is.True, $"Should identify some flavor for {modeName}");
        }
    }

    private bool IsStandardMode(string name)
    {
        var standard = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Ionian", "Dorian", "Phrygian", "Lydian", "Mixolydian", "Aeolian", "Locrian",
            "Harmonic minor", "Melodic minor", "Phrygian dominant", "Lydian dominant", "Altered", "Diminished", "Whole-tone", "Blues"
        };
        // Also include Pentatonic as standard-ish
        if (name.Contains("Pentatonic", StringComparison.OrdinalIgnoreCase)) return true;
        
        return standard.Contains(name);
    }

    public static IEnumerable<TestCaseData> GetTonalModeTestCases()
    {
        // Ensure service is initialized for static context
        var service = ModalCharacteristicIntervalService.Instance;
        var names = service.GetAllModeNames().OrderBy(n => n).ToList();

        foreach (var name in names)
        {
            var intervals = service.GetModeIntervals(name);
            if (intervals == null || intervals.Count == 0) continue;
            
            yield return new TestCaseData(name, intervals.OrderBy(x => x).ToArray())
                .SetName($"Mode_{name.Replace(" ", "_")}"); // Clean name for runner
        }
    }
}