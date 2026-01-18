namespace GaChatbot.Tests.Services;

using GaChatbot.Services;
using GaChatbot.Models;
using GaChatbot.Abstractions;
using GA.Business.Core.AI;
using GaChatbot.Tests.Mocks;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Musical.Explanation;
using Moq;
using NUnit.Framework;
using System.Threading.Tasks;
using System.Linq;

using GA.Business.ML.Musical.Enrichment;
using GA.Business.ML.Embeddings;

[TestFixture]
public class SpectralRagOrchestratorTests
{
    private FileBasedVectorIndex _index;
    private SpectralRagOrchestrator _orchestrator;

    [SetUp]
    public void Setup()
    {
        var tempFile = System.IO.Path.GetTempFileName();
        _index = new FileBasedVectorIndex(tempFile);
        
        var modalFlavor = new ModalFlavorService();
        var realExplainer = new VoicingExplanationService(modalFlavor); 
        var retrieval = new SpectralRetrievalService(_index);
        
        var promptBuilder = new GroundedPromptBuilder();
        var validator = new ResponseValidator();
        var narrator = new MockGroundedNarrator(promptBuilder, validator);
        var generator = GA.Business.ML.Tests.TestInfrastructure.TestServices.CreateGenerator();

        _orchestrator = new SpectralRagOrchestrator(_index, retrieval, realExplainer, generator, narrator);
    }
    
    private VoicingDocument CreateDoc(string id, string name, int[] midi, int[] pcs, string diagram)
    {
        var doc = new VoicingDocument
        {
            Id = id,
            ChordName = name,
            SearchableText = name,
            PossibleKeys = [],
            SemanticTags = [],
            YamlAnalysis = "{}",
            MidiNotes = midi,
            PitchClasses = pcs,
            PitchClassSet = string.Join(",", pcs),
            IntervalClassVector = "000000",
            AnalysisEngine = "test",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = "0",
            Diagram = diagram,
            RootPitchClass = pcs.Length > 0 ? pcs[0] : 0
        };

        // Use the real generator to make the "Theory" explanation work in logs
        var generator = GA.Business.ML.Tests.TestInfrastructure.TestServices.CreateGenerator();
        return doc with { Embedding = generator.GenerateEmbeddingAsync(doc).GetAwaiter().GetResult() };
    }

    [Test]
    public async Task AnswerAsync_WithDm7_ReturnsIdentityMatchAndNeighbors()
    {
        // Arrange
        // Dm7: D F A C
        var dm7 = CreateDoc("dm7", "Dm7 Open", [50, 53, 57, 60], [2, 5, 9, 0], "x-x-0-2-1-1");
        // Neighbor (F Major): F A C
        var neighbor = CreateDoc("neighbor", "F Major", [53, 57, 60], [5, 9, 0], "x-x-3-2-1-1");

        _index.Add(dm7);
        _index.Add(neighbor);

        var request = new ChatRequest("Dm7 Open");

        // Act
        var response = await _orchestrator.AnswerAsync(request);

        // Assert
        Assert.That(response.Candidates, Is.Not.Empty);
        
        // Should find Dm7 (Identity Search)
        var first = response.Candidates.First();
        Assert.That(first.DisplayName, Is.EqualTo("Dm7 Open"));
        Assert.That(first.Score, Is.GreaterThanOrEqualTo(0.89)); 
        
        // Should also find neighbor
        var second = response.Candidates.Skip(1).First();
        Assert.That(second.DisplayName, Is.EqualTo("F Major"));
    }

    [Test]
    public async Task AnswerAsync_Fallback_ReturnsSomeResults()
    {
        // Arrange
        _index.Add(CreateDoc("1", "C Major", [48, 52, 55], [0, 4, 7], "x-3-2-0-1-0"));
        var request = new ChatRequest("Some unknown nonsense");

        // Act
        var response = await _orchestrator.AnswerAsync(request);

        // Assert
        Assert.That(response.Candidates, Is.Not.Empty);
        Assert.That(response.DebugParams.ToString(), Contains.Substring("Fallback"));
    }
}
