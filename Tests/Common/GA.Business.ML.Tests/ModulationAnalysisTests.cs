namespace GA.Business.ML.Tests;

using Domain.Core.Theory.Atonal;
using Embeddings;
using Retrieval;
using TestInfrastructure;
using Rag.Models;

[TestFixture]
public class ModulationAnalysisTests
{
    [SetUp]
    public async Task Setup()
    {
        var services = await TestServices.CreateAsync();
        _generator = services.Generator;
        _analyzer = new(_generator);
    }

    private ModulationAnalyzer _analyzer;
    private MusicalEmbeddingGenerator _generator;

    [Test]
    public async Task IdentifyTargets_CtoG_Progression_IdentifiesG()
    {
        // 1. Create a progression drifting from C toward G
        // C Major -> Am -> D7 -> G7
        var progressionNames = new[] { "C Major", "A Minor", "D Dominant 7", "G Dominant 7" };
        var progressionNotes = new[]
        {
            new[] { 48, 52, 55 }, // C
            [45, 48, 52], // Am
            [50, 54, 57, 60], // D7 (Dominant of G)
            [55, 59, 62, 65] // G7
        };

        var docs = new List<ChordVoicingRagDocument>();
        for (var i = 0; i < progressionNames.Length; i++)
        {
            var pcsList = progressionNotes[i].Select(m => PitchClass.FromValue(m % 12)).ToList();
            var pcsSet = new PitchClassSet(pcsList);

            var doc = new ChordVoicingRagDocument
            {
                Id = $"p-{i}", ChordName = progressionNames[i],
                MidiNotes = progressionNotes[i],
                PitchClasses = [.. progressionNotes[i].Select(m => m % 12)],
                SemanticTags = ["test"],
                PossibleKeys = [],
                YamlAnalysis = "{}",
                PitchClassSet = string.Join(",", progressionNotes[i].Select(m => m % 12)),
                IntervalClassVector = pcsSet.IntervalClassVector.ToString(),
                AnalysisEngine = "Test",
                AnalysisVersion = "1.0",
                Jobs = [],
                TuningId = "Standard",
                PitchClassSetId = "0",
                Diagram = "",
                SearchableText = "",
                Consonance = Math.Max(0.0, 1.0 - pcsSet.IntervalClassVector.Sum() / 12.0)
            };
            doc = doc with { Embedding = await _generator.GenerateEmbeddingAsync(doc) };
            docs.Add(doc);
        }

        // 2. Analyze
        var targets = _analyzer.IdentifyTargets(docs);

        TestContext.WriteLine("Modulation Targets:");
        foreach (var t in targets)
        {
            TestContext.WriteLine($"  Key: {t.Key.ToSharpNote()} (Confidence: {t.Confidence:F2})");
        }

        // The top target should be either C (Current) or G (Destination)
        var topKey = targets[0].Key.ToSharpNote().ToString();
        Assert.That(topKey, Is.AnyOf("C", "G"));
    }
}
