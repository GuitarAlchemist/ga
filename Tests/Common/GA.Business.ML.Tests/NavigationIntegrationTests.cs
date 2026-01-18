namespace GA.Business.ML.Tests;

using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Tests.TestInfrastructure;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Notes;

[TestFixture]
public class NavigationIntegrationTests
{
    private NextChordSuggestionService _service;
    private FileBasedVectorIndex _index;
    private MusicalEmbeddingGenerator _generator;

    [SetUp]
    public async Task Setup()
    {
        var services = await TestServices.CreateAsync();
        _index = services.Index;
        _generator = services.Generator;
        
        var retrieval = new SpectralRetrievalService(_index);
        var physical = new PhysicalCostService();
        _service = new NextChordSuggestionService(retrieval, physical);
    }

    [Test]
    public async Task SuggestNext_FromCMajor_ReturnsSmoothTransitions()
    {
        // 1. Seed the index with related chords
        var chords = new[] {
            ("C Major", new[] { 48, 52, 55 }, new[] { 0, 4, 7 }),
            ("A Minor", new[] { 45, 48, 52 }, new[] { 9, 0, 4 }),
            ("F Major", new[] { 41, 45, 48 }, new[] { 5, 9, 0 }),
            ("G Major", new[] { 43, 47, 50 }, new[] { 7, 11, 2 })
        };

        var docs = new List<VoicingDocument>();
        foreach (var (name, midi, pcs) in chords)
        {
            var doc = new VoicingDocument
            {
                Id = Guid.NewGuid().ToString(),
                ChordName = name,
                MidiNotes = midi,
                PitchClasses = pcs,
                SearchableText = name,
                SemanticTags = new[] { "test" },
                PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = string.Join(",", pcs), IntervalClassVector = "000000", AnalysisEngine = "Test", AnalysisVersion = "1.0", Jobs = [], TuningId = "Standard", PitchClassSetId = "0", Diagram = "x-x-x-x-x-x"
            };
            doc = doc with { Embedding = await _generator.GenerateEmbeddingAsync(doc) };
            _index.Add(doc);
            docs.Add(doc);
        }

        var cMajor = docs.First(d => d.ChordName == "C Major");

        // 2. Get Suggestions
        var suggestions = await _service.SuggestNextAsync(cMajor, topK: 5);

        TestContext.WriteLine($"Suggestions from {cMajor.ChordName}:");
        foreach (var s in suggestions)
        {
            TestContext.WriteLine($"  -> {s.Doc.ChordName} (Harmonic: {s.HarmonicScore:F2}, Physical Cost: {s.PhysicalCost:F2}, Total: {s.TotalScore:F2})");
        }

        Assert.That(suggestions.Count, Is.GreaterThan(0));
    }
}