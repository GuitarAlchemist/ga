namespace GA.Business.ML.Tests;

using System.Collections.Generic;
using System.Linq;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Wavelets;
using NUnit.Framework;

[TestFixture]
public class ProgressionEmbeddingTests
{
    private ProgressionEmbeddingService _embeddingService;

    [SetUp]
    public void Setup()
    {
        var waveletService = new WaveletTransformService();
        var signalService = new ProgressionSignalService();
        _embeddingService = new ProgressionEmbeddingService(signalService, waveletService);
    }

    [Test]
    public void Test_GenerateProgressionEmbedding()
    {
        // Arrange
        // Create a simple progression: C Major -> G Major -> Am -> F Major
        // (Tonic -> Dominant -> Tonic-parallel -> Predominant)
        var progression = new List<VoicingDocument>
        {
            CreateDummyDoc("C", 0.9, new double[216]), // High stability
            CreateDummyDoc("G", 0.7, new double[216].Select(x => 0.5).ToArray()), // Lower stability
            CreateDummyDoc("Am", 0.8, new double[216].Select(x => 0.2).ToArray()),
            CreateDummyDoc("F", 0.75, new double[216].Select(x => 0.8).ToArray())
        };

        // Act
        var embedding = _embeddingService.GenerateEmbedding(progression);

        // Assert
        TestContext.WriteLine($"Progression: {string.Join(" -> ", progression.Select(p => p.ChordName))}");
        TestContext.WriteLine($"Generated Embedding Length: Expected=80, Actual={embedding.Length} (16 bins * 5 stability/tension metrics)");

        Assert.Multiple(() =>
        {
            Assert.That(embedding, Is.Not.Null, "Progression embedding should not be null.");
            Assert.That(embedding.Length, Is.EqualTo(80), "Embedding length must be exactly 80 (5 metrics * 16 bins).");

            // Check that some values are non-zero (assuming dummy inputs produced motion)
            Assert.That(embedding.Any(x => x != 0), Is.True, "The embedding should contain non-zero data representing the progression's motion.");
        });
    }

    private VoicingDocument CreateDummyDoc(string name, double consonance, double[] embedding)
    {
        // Set entropy (index 108) manually for test
        embedding[108] = 0.5;

        return new VoicingDocument
        {
            Id = name,
            SearchableText = name,
            ChordName = name,
            Consonance = consonance,
            Embedding = embedding,

            // Required placeholders
            MidiNotes = [],
            PitchClasses = [0, 4, 7], // Default to C Major triad for validity
            SemanticTags = [],
            PossibleKeys = [],
            YamlAnalysis = "{}",
            PitchClassSet = "",
            IntervalClassVector = "000000",
            AnalysisEngine = "Test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "0",
            Diagram = ""
        };
    }
}
