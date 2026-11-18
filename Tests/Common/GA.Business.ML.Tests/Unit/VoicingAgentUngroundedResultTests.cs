namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Rag.Models;
using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// A voicing search whose every result scores 0 matched nothing musical: the query named no
/// chord, mode or known voicing tag ("finger vibrato", "CAGED system"), and the index returned
/// arbitrary voicings. The agent declines so the request reaches another handler instead of
/// answering "Found 10 voicings matching tags [finger, vibrato]" at score 0.000.
/// </summary>
[TestFixture]
public class VoicingAgentUngroundedResultTests
{
    [Test]
    public async Task AllResultsScoreZero_Declines()
    {
        var response = await Agent(scores: [0.0, 0.0, 0.0]).ProcessAsync(new AgentRequest { Query = "How do I develop a good finger vibrato?" });

        Assert.That(response.Declined, Is.True, response.Result);
    }

    // Known voicing tags ("shell", "drop 2") score low but above zero on the live index.
    [Test]
    public async Task SomeResultScoresAboveZero_Answers()
    {
        var response = await Agent(scores: [0.071, 0.058, 0.0]).ProcessAsync(new AgentRequest { Query = "Show me shell voicings" });

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.False);
            Assert.That(response.Result, Does.StartWith("Found 3 voicings"));
        });
    }

    // A follow-up such as "Which chord does it fit best?" names no chord, mode or tag: the
    // agent declines so an agent that reads the conversation history answers instead of
    // replying "I couldn't find a chord name, mode, or style tag in your request".
    [Test]
    public async Task NoMusicalEntity_Declines()
    {
        var response = await Agent(scores: [0.9], extracted: new StructuredQuery(null, null, null, null, null))
            .ProcessAsync(new AgentRequest { Query = "Which chord does it fit best?" });

        Assert.That(response.Declined, Is.True, response.Result);
    }

    private static VoicingAgent Agent(double[] scores, StructuredQuery? extracted = null)
    {
        var results = scores
            .Select((score, i) => new VoicingSearchResult(Document($"v{i}"), score, "query"))
            .ToList();

        var strategy = new Mock<IVoicingSearchStrategy>();
        strategy.SetupGet(s => s.IsAvailable).Returns(true);
        strategy.SetupGet(s => s.Name).Returns("stub");
        strategy.Setup(s => s.InitializeAsync(It.IsAny<IEnumerable<VoicingEmbedding>>())).Returns(Task.CompletedTask);
        strategy.Setup(s => s.HybridSearchAsync(It.IsAny<double[]>(), It.IsAny<VoicingSearchFilters>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);
        strategy.Setup(s => s.UnsupportedPopulatedFilters(It.IsAny<VoicingSearchFilters>())).Returns([]);

        var search = new EnhancedVoicingSearchService(new VoicingIndexingService(), strategy.Object);
        search.InitializeEmbeddingsAsync(_ => Task.FromResult(Array.Empty<double>())).GetAwaiter().GetResult();

        var extractor = new Mock<IMusicalQueryExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(extracted ?? new StructuredQuery(null, null, null, null, ["finger", "vibrato"]));

        return new VoicingAgent(
            new Mock<IChatClient>(MockBehavior.Strict).Object,
            NullLogger<VoicingAgent>.Instance,
            search,
            extractor.Object,
            new MusicalQueryEncoder(new ModalVectorService()));
    }

    private static ChordVoicingRagDocument Document(string id) => new()
    {
        Id = id,
        Diagram = "x-3-2-0-1-0",
        ChordName = "C",
        PitchClasses = [0, 4, 7],
        IntervalClassVector = "001110",
        Consonance = 0.8,
        Brightness = 0.6,
        VoicingType = "guitar",
        PossibleKeys = [],
        SemanticTags = [],
        Jobs = [],
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "",
        YamlAnalysis = "",
        TuningId = "Standard",
        PitchClassSetId = "",
        PitchClassSet = "",
        MidiNotes = [48, 52, 55],
        MinFret = 0,
        MaxFret = 3,
    };
}
