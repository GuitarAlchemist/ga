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
/// A follow-up that carries only a style tag has its referent in the conversation, which this
/// agent does not read. "Show me an easy way to play it on guitar." after a D7 question searched
/// the "open" tag alone and answered with augmented hexachords; "What is the lowest note I can
/// play on it?" after "I play a seven-string guitar tuned B E A D G B E." searched the "note" tag
/// (2026-11-19). Both now decline so an agent that reads the history answers.
/// </summary>
[TestFixture]
public class VoicingAgentFollowUpChordTests
{
    private static readonly StructuredQuery OpenTagOnly = new(null, null, null, null, ["open"]);

    [TestCase("Show me an easy way to play it on guitar.", "What notes are in a D7 chord?")]
    [TestCase("What is the lowest note I can play on it?", "I play a seven-string guitar tuned B E A D G B E.")]
    public async Task TagOnlyFollowUp_Declines(string followUp, string previous)
    {
        var (agent, filters) = Agent(_ => OpenTagOnly);

        var response = await agent.ProcessAsync(new AgentRequest
        {
            Query = followUp,
            ConversationHistory =
            [
                new ChatHistoryTurn("user", previous),
                new ChatHistoryTurn("assistant", "..."),
            ],
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.True, response.Result);
            Assert.That(filters, Is.Empty);
        });
    }

    [Test]
    public async Task TagOnlyFirstMessage_StillSearches()
    {
        var (agent, filters) = Agent(_ => new StructuredQuery(null, null, null, null, ["shell"]));

        var response = await agent.ProcessAsync(new AgentRequest { Query = "Show me shell voicings" });

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.False, response.Result);
            Assert.That(filters[0]?.Tags, Is.EqualTo(new[] { "shell" }));
        });
    }

    [Test]
    public async Task FollowUpNamingAChord_StillSearches()
    {
        var (agent, filters) = Agent(_ => new StructuredQuery("G7", 7, [7, 11, 2, 5], null, ["open"]));

        var response = await agent.ProcessAsync(new AgentRequest
        {
            Query = "Show me an open G7 instead.",
            ConversationHistory = [new ChatHistoryTurn("user", "What notes are in a D7 chord?")],
        });

        Assert.Multiple(() =>
        {
            Assert.That(response.Declined, Is.False, response.Result);
            Assert.That(filters[0]?.ChordName, Is.EqualTo("G7"));
        });
    }

    private static (VoicingAgent Agent, List<VoicingSearchFilters?> Filters) Agent(Func<string, StructuredQuery> extract)
    {
        List<VoicingSearchFilters?> filters = [];
        var strategy = new Mock<IVoicingSearchStrategy>();
        strategy.SetupGet(s => s.IsAvailable).Returns(true);
        strategy.SetupGet(s => s.Name).Returns("stub");
        strategy.Setup(s => s.InitializeAsync(It.IsAny<IEnumerable<VoicingEmbedding>>())).Returns(Task.CompletedTask);
        strategy.Setup(s => s.HybridSearchAsync(It.IsAny<double[]>(), It.IsAny<VoicingSearchFilters>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Callback<double[], VoicingSearchFilters, int, CancellationToken>((_, f, _, _) => filters.Add(f))
            .ReturnsAsync([new VoicingSearchResult(Document(), 0.5, "query")]);
        strategy.Setup(s => s.UnsupportedPopulatedFilters(It.IsAny<VoicingSearchFilters>())).Returns([]);

        var search = new EnhancedVoicingSearchService(new VoicingIndexingService(), strategy.Object);
        search.InitializeEmbeddingsAsync(_ => Task.FromResult(Array.Empty<double>())).GetAwaiter().GetResult();

        var extractor = new Mock<IMusicalQueryExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string text, CancellationToken _) => extract(text));

        var agent = new VoicingAgent(
            new Mock<IChatClient>(MockBehavior.Strict).Object,
            NullLogger<VoicingAgent>.Instance,
            search,
            extractor.Object,
            new MusicalQueryEncoder(new ModalVectorService()));
        return (agent, filters);
    }

    private static ChordVoicingRagDocument Document() => new()
    {
        Id = "v0",
        Diagram = "x-x-0-2-1-2",
        ChordName = "D7",
        PitchClasses = [2, 6, 9, 0],
        IntervalClassVector = "012111",
        Consonance = 0.6,
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
        MidiNotes = [50, 54, 57, 60],
        MinFret = 0,
        MaxFret = 2,
    };
}
