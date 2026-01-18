namespace GA.Business.ML.Tests;

using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.Core.Tonal.Hierarchies;
using GA.Business.Core.Fretboard.Voicings.Search;
using NUnit.Framework;

[TestFixture]
public class ComplexityIntegrationTests
{
    private MusicalEmbeddingGenerator _generator;

    [SetUp]
    public void Setup()
    {
        _generator = TestInfrastructure.TestServices.CreateGenerator();
    }

    [Test]
    public async Task ComplexityScore_PopulatedInEmbedding()
    {
        // Test Case 1: C Major Triad (Level 2)
        var triad = CreateDoc([0, 4, 7]);
        var embTriad = await _generator.GenerateEmbeddingAsync(triad);
        var scoreTriad = embTriad[EmbeddingSchema.HierarchyComplexityScore];
        
        // Level 2 / 6 = 0.333
        Assert.That(scoreTriad, Is.EqualTo(2.0/6.0).Within(0.001));

        // Test Case 2: Cmaj9 (Level 4)
        var ext = CreateDoc([0, 4, 7, 11, 2]);
        var embExt = await _generator.GenerateEmbeddingAsync(ext);
        var scoreExt = embExt[EmbeddingSchema.HierarchyComplexityScore];
        
        // Level 4 / 6 = 0.666
        Assert.That(scoreExt, Is.EqualTo(4.0/6.0).Within(0.001));
        
        Assert.That(scoreExt, Is.GreaterThan(scoreTriad));
    }

    private VoicingDocument CreateDoc(int[] pcs)
    {
        return new VoicingDocument
        {
            Id = "test",
            PitchClasses = pcs,
            MidiNotes = pcs, // Mock
            SearchableText = "Test",
            // Required
            ChordName = "Test",
            RootPitchClass = 0,
            SemanticTags = [],
            PossibleKeys = [],
            YamlAnalysis = "{}",
            PitchClassSet = string.Join(",", pcs),
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
