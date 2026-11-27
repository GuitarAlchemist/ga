namespace GaApi.Tests.Services;

using GA.Business.Core.Orchestration.Intents;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Rag.Models;
using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// The semantic router reaches the voicing agent through <see cref="VoicingIntent"/>. The intent
/// used to drop the conversation, so the agent's follow-up decline ("Show me an easy way to play
/// it on guitar." after a D7 question carries only the "open" tag) never fired on that path and
/// the agent searched the tag alone.
/// </summary>
[TestFixture]
public class VoicingIntentHistoryTests
{
    private const string FollowUp = "Show me an easy way to play it on guitar.";

    [Test]
    public async Task TagOnlyFollowUp_WithHistory_Declines()
    {
        IIntent intent = new VoicingIntent(TagOnlyVoicingAgent());
        IReadOnlyList<ChatHistoryTurn> history =
        [
            new("user", "What notes are in a D7 chord?"),
            new("assistant", "D7 contains D, F#, A and C."),
        ];

        var result = await intent.ExecuteAsync(FollowUp, history);

        Assert.That(result.Declined, Is.True);
    }

    [Test]
    public async Task TagOnlyRequest_WithoutHistory_StillSearches()
    {
        IIntent intent = new VoicingIntent(TagOnlyVoicingAgent());

        var result = await intent.ExecuteAsync(FollowUp, []);

        Assert.That(result.Declined, Is.False);
    }

    /// <summary>A voicing agent whose extractor reads only the "open" tag and whose index
    /// returns one matching voicing.</summary>
    private static VoicingAgent TagOnlyVoicingAgent()
    {
        var strategy = new Mock<IVoicingSearchStrategy>();
        strategy.SetupGet(s => s.IsAvailable).Returns(true);
        strategy.SetupGet(s => s.Name).Returns("stub");
        strategy.Setup(s => s.InitializeAsync(It.IsAny<IEnumerable<VoicingEmbedding>>())).Returns(Task.CompletedTask);
        strategy.Setup(s => s.HybridSearchAsync(It.IsAny<double[]>(), It.IsAny<VoicingSearchFilters>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new VoicingSearchResult(Document(), 0.9, "query")]);
        strategy.Setup(s => s.UnsupportedPopulatedFilters(It.IsAny<VoicingSearchFilters>())).Returns([]);

        var search = new EnhancedVoicingSearchService(new VoicingIndexingService(), strategy.Object);
        search.InitializeEmbeddingsAsync(_ => Task.FromResult(Array.Empty<double>())).GetAwaiter().GetResult();

        var extractor = new Mock<IMusicalQueryExtractor>();
        extractor.Setup(e => e.ExtractAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StructuredQuery(null, null, null, null, ["open"]));

        return new VoicingAgent(
            Mock.Of<IChatClient>(),
            NullLogger<VoicingAgent>.Instance,
            search,
            extractor.Object,
            new MusicalQueryEncoder(new ModalVectorService()));
    }

    private static ChordVoicingRagDocument Document() => new()
    {
        Id = "d",
        Diagram = "x-x-0-2-3-2",
        ChordName = "D",
        PitchClasses = [2, 6, 9],
        IntervalClassVector = "001110",
        Consonance = 0.8,
        Brightness = 0.6,
        VoicingType = "guitar",
        PossibleKeys = [],
        SemanticTags = ["open"],
        Jobs = [],
        AnalysisEngine = "Test",
        AnalysisVersion = "1.0",
        SearchableText = "",
        YamlAnalysis = "",
        TuningId = "Standard",
        PitchClassSetId = "",
        PitchClassSet = "",
        MidiNotes = [50, 57, 62, 66],
        MinFret = 0,
        MaxFret = 3,
    };
}
