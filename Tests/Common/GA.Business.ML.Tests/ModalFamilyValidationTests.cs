namespace GA.Business.ML.Tests;

using Embeddings;
using Embeddings.Services;
using Rag.Models;

[TestFixture]
public class ModalFamilyValidationTests
{
    [SetUp]
    public void Setup() => _service = new();

    private ModalVectorService _service;

    [Test]
    public void Test_MajorFamily_Lydian()
    {
        // C Lydian: 0 2 4 6 7 9 11
        var doc = CreateDoc(0, [0, 2, 4, 6, 7, 9, 11]);
        var vector = _service.ComputeEmbedding(doc);

        var score = vector[EmbeddingSchema.ModalLydian - EmbeddingSchema.ModalOffset];
        Console.WriteLine($"Lydian Score: {score}");
        Assert.That(score, Is.GreaterThan(0.9));
    }

    [Test]
    public void Test_HarmonicMinorFamily_PhrygianDominant()
    {
        // G Phrygian Dominant (5th mode of C Harmonic Minor): G Ab B C D Eb F
        // Relative to G: 0 1 4 5 7 8 10
        var doc = CreateDoc(7, [7, 8, 11, 0, 2, 3, 5]);
        var vector = _service.ComputeEmbedding(doc);

        var score = vector[EmbeddingSchema.ModalPhrygianDominant - EmbeddingSchema.ModalOffset];
        Console.WriteLine($"Phrygian Dominant Score: {score}");
        Assert.That(score, Is.GreaterThan(0.9));
    }

    [Test]
    public void Test_MelodicMinorFamily_Altered()
    {
        // B Altered (7th mode of C Melodic Minor): B C D Eb F G A (Wait, B C D D# F G A)
        // B C D Eb F G A -> 0 1 3 4 6 8 10
        var doc = CreateDoc(11, [11, 0, 2, 3, 5, 7, 9]);
        var vector = _service.ComputeEmbedding(doc);

        var score = vector[EmbeddingSchema.ModalAltered - EmbeddingSchema.ModalOffset];
        Console.WriteLine($"Altered Score: {score}");
        Assert.That(score, Is.GreaterThan(0.9));
    }

    [Test]
    public void Test_HarmonicMajorFamily_LydianFlat3()
    {
        // F Lydian b3 (4th mode of C Harmonic Major): F G Ab B C D E
        // Relative to F: 0 2 3 6 7 9 11
        var doc = CreateDoc(5, [5, 7, 8, 11, 0, 2, 4]);
        var vector = _service.ComputeEmbedding(doc);

        var score = vector[EmbeddingSchema.ModalLydianFlat3 - EmbeddingSchema.ModalOffset];
        Console.WriteLine($"Lydian b3 Score: {score}");
        Assert.That(score, Is.GreaterThan(0.9));
    }

    private ChordVoicingRagDocument CreateDoc(int root, int[] pcs) =>
        new()
        {
            Id = "test",
            SearchableText = "test",
            RootPitchClass = root,
            PitchClasses = pcs,
            Diagram = "x", MidiNotes = [], SemanticTags = [], PossibleKeys = [], YamlAnalysis = "", PitchClassSet = "",
            IntervalClassVector = "", AnalysisEngine = "test", AnalysisVersion = "1.0", Jobs = [],
            TuningId = "standard", PitchClassSetId = "p"
        };
}
