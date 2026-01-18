namespace GaChatbot.Tests.Services;

using GaChatbot.Services;
using GA.Business.Core.Fretboard.Voicings.Search;
using NUnit.Framework;
using System.Linq;

[TestFixture]
public class InMemoryVectorIndexTests
{
    private InMemoryVectorIndex _index;

    [SetUp]
    public void Setup()
    {
        _index = new InMemoryVectorIndex();
    }

    private VoicingDocument CreateDoc(string id, string name, double[] embedding)
    {
        return new VoicingDocument
        {
            Id = id,
            ChordName = name,
            // Required dummy fields
            SearchableText = "text",
            PossibleKeys = [],
            SemanticTags = [],
            YamlAnalysis = "{}",
            MidiNotes = [],
            PitchClasses = [],
            PitchClassSet = "",
            IntervalClassVector = "",
            AnalysisEngine = "test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "0",
            Diagram = "x-0-0-0-0-0",
            Embedding = embedding
        };
    }

    [Test]
    public void FindByIdentity_ReturnsCorrectDoc()
    {
        _index.Add(CreateDoc("1", "C Major", new double[109]));
        _index.Add(CreateDoc("2", "Dm7", new double[109]));

        var result = _index.FindByIdentity("Dm7");
        
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Id, Is.EqualTo("2"));
    }

    [Test]
    public void FindByIdentity_ReturnsNull_WhenNotFound()
    {
        _index.Add(CreateDoc("1", "C Major", new double[109]));

        var result = _index.FindByIdentity("G7");
        
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Search_ReturnsItemsSortedByCosineSimilarity()
    {
        // Vector A: [1, 0] (Normalized)
        // Vector B: [0, 1] (Normalized, orthogonal to A)
        // Vector C: [0.7, 0.7] (45 deg, closer to A than B is?) No, cos(45)=0.707. Dot(A, C) = 0.7. Dot(A, B) = 0.0.
        
        // Let's use simple 2D vectors padded to 109 length
        var vecA = new double[109]; vecA[0] = 1.0;
        var vecB = new double[109]; vecB[1] = 1.0;
        var vecC = new double[109]; vecC[0] = 0.7071; vecC[1] = 0.7071;

        _index.Add(CreateDoc("A", "DocA", vecA));
        _index.Add(CreateDoc("B", "DocB", vecB));
        _index.Add(CreateDoc("C", "DocC", vecC));

        // Search for A. Should get A (1.0), C (~0.7), B (0.0)
        // Note: Search takes float[]
        var query = new float[109]; query[0] = 1.0f; 

        var results = _index.Search(query).ToList();

        Assert.That(results.Count, Is.EqualTo(3));
        Assert.That(results[0].Doc.Id, Is.EqualTo("A"));
        Assert.That(results[0].Sim, Is.GreaterThan(0.99)); // ~1.0

        Assert.That(results[1].Doc.Id, Is.EqualTo("C"));
        Assert.That(results[1].Sim, Is.GreaterThan(0.70)); 

        Assert.That(results[2].Doc.Id, Is.EqualTo("B"));
        Assert.That(results[2].Sim, Is.LessThan(0.01)); // ~0.0
    }
}
