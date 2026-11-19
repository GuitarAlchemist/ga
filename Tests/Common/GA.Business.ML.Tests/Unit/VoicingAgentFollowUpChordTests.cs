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
/// A follow-up such as "Show me an easy way to play it on guitar." names its chord in the
/// previous user turn. The live agent searched by the tag the message did carry ("open") with
/// no chord and answered with augmented hexachords for a question about D7 (2026-11-19).
/// </summary>
[TestFixture]
public class VoicingAgentFollowUpChordTests
{
    private static readonly StructuredQuery OpenTagOnly = new(null, null, null, null, ["open"]);
    private static readonly StructuredQuery D7 = new("D7", 2, [2, 6, 9, 0], null, null);

    [Test]
    public async Task MessageWithoutChord_SearchesTheChordOfThePreviousUserTurn()
    {
        var (agent, filters) = Agent(text => text.Contains("D7") ? D7 : OpenTagOnly);

        await agent.ProcessAsync(new AgentRequest
        {
            Query = "Show me an easy way to play it on guitar.",
            ConversationHistory =
            [
                new ChatHistoryTurn("user", "What notes are in a D7 chord?"),
                new ChatHistoryTurn("assistant", "D dominant 7 chord contains D, F#, A, and C."),
            ],
        });

        Assert.That(filters, Is.Not.Empty);
        Assert.Multiple(() =>
        {
            Assert.That(filters[0]?.ChordName, Is.EqualTo("D7"));
            Assert.That(filters[0]?.Tags, Is.EqualTo(new[] { "open" }));
        });
    }

    [Test]
    public async Task MessageNamingAChord_KeepsItsOwnChord()
    {
        var (agent, filters) = Agent(text => text.Contains("G7") ? new StructuredQuery("G7", 7, [7, 11, 2, 5], null, null) : D7);

        await agent.ProcessAsync(new AgentRequest
        {
            Query = "Show me G7 instead.",
            ConversationHistory = [new ChatHistoryTurn("user", "What notes are in a D7 chord?")],
        });

        Assert.That(filters[0]?.ChordName, Is.EqualTo("G7"));
    }

    [Test]
    public async Task OnlyThePreviousUserTurnIsConsulted()
    {
        // An older chord is a stale referent: the user has moved on since.
        var (agent, filters) = Agent(text => text.Contains("D7") ? D7 : OpenTagOnly);

        await agent.ProcessAsync(new AgentRequest
        {
            Query = "Show me an easy way to play it on guitar.",
            ConversationHistory =
            [
                new ChatHistoryTurn("user", "What notes are in a D7 chord?"),
                new ChatHistoryTurn("assistant", "D, F#, A, C."),
                new ChatHistoryTurn("user", "Thanks. What makes a voicing easy?"),
                new ChatHistoryTurn("assistant", "Open strings and few fingers."),
            ],
        });

        Assert.That(filters[0]?.ChordName, Is.Null);
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
