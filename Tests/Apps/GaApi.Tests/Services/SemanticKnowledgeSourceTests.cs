namespace GaApi.Tests.Services;

#pragma warning disable SKEXP0001

using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Rag.Models;
using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Core;
using GaApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
///     <c>POST /api/voicings/retrieve</c> answers through <see cref="SemanticKnowledgeSource" />. On the
///     OPTIC-K path the query's chord must decide which voicings come first: the vector alone ranked
///     <c>Gadd9</c> above every G triad and <c>Cmaj9</c> above every <c>E7#9</c>.
/// </summary>
[TestFixture]
public class SemanticKnowledgeSourceTests
{
    [TestCase("open G major chord", "G")]
    [TestCase("E7#9 Hendrix chord", "E7#9")]
    public async Task OptickSearch_FiltersOnTheParsedChord(string query, string chord)
    {
        var strategy = new RecordingStrategy(named: [Result(chord)], unfiltered: [Result("Cmaj9")]);

        var results = await Create(strategy).SearchAsync(query, 5, CancellationToken.None);

        Assert.That(strategy.ChordNameFilters, Is.EqualTo(new[] { chord }));
        Assert.That(results.Select(r => r.Content), Has.All.StartWith($"## {chord}\n").Or.StartWith($"## {chord}\r\n"));
    }

    [Test]
    public async Task OptickSearch_WhenNoVoicingCarriesTheChordName_FallsBackToTheVectorRanking()
    {
        var strategy = new RecordingStrategy(named: [], unfiltered: [Result("G7b13"), Result("Em7b5")]);

        var results = await Create(strategy).SearchAsync("G7b13 voicing", 5, CancellationToken.None);

        Assert.That(strategy.ChordNameFilters, Is.EqualTo(new string?[] { "G7b13", null }),
            "the corpus may not label a valid chord with this exact symbol; geometry still grounds the answer");
        Assert.That(results, Has.Count.EqualTo(2));
    }

    [TestCase("Em(maj7) voicing", "Em", "Emmaj7")]
    [TestCase("James Bond chord", "Em", "Emmaj7")]
    [TestCase("C6/9 voicing", "C6", "C6/9")]
    public async Task OptickSearch_WithChordTonesTheNameFilterDrops_KeepsTheVectorRanking(
        string query, string truncated, string asked)
    {
        // The name filter reads "(...)" as a stored voicing tag ("Cmaj7(shell)") and "/" as a slash
        // bass, so it would search Em(maj7) as Em and C6/9 as C6. The encoder keeps the added tone;
        // the vector ranking stays.
        var strategy = new RecordingStrategy(named: [Result(truncated)], unfiltered: [Result(asked)]);

        var results = await Create(strategy).SearchAsync(query, 5, CancellationToken.None);

        Assert.That(strategy.ChordNameFilters, Is.EqualTo(new string?[] { null }));
        Assert.That(results, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task OptickSearch_WithoutAChord_DoesNotFilterOnChordName()
    {
        var strategy = new RecordingStrategy(named: [], unfiltered: [Result("Dm7")]);

        var results = await Create(strategy).SearchAsync("dorian", 5, CancellationToken.None);

        Assert.That(strategy.ChordNameFilters, Is.EqualTo(new string?[] { null }));
        Assert.That(results, Has.Count.EqualTo(1));
    }

    private static SemanticKnowledgeSource Create(IVoicingSearchStrategy strategy)
    {
        var httpClientFactory = Mock.Of<IHttpClientFactory>(f => f.CreateClient(It.IsAny<string>()) == new HttpClient());
        var embeddings = new OllamaEmbeddingService(
            httpClientFactory,
            new ConfigurationBuilder().Build(),
            NullLogger<OllamaEmbeddingService>.Instance);

        return new SemanticKnowledgeSource(
            new EnhancedVoicingSearchService(new VoicingIndexingService(), strategy),
            embeddings,
            new TypedMusicalQueryExtractor(),
            new MusicalQueryEncoder(new ModalVectorService()),
            NullLogger<SemanticKnowledgeSource>.Instance);
    }

    private static VoicingSearchResult Result(string chordName) =>
        new(new ChordVoicingRagDocument
        {
            SearchableText = chordName,
            ChordName = chordName,
            Diagram = "x-3-2-0-1-0",
            MidiNotes = [],
            PitchClasses = [],
            PitchClassSet = "{}",
            SemanticTags = [],
            PossibleKeys = [],
            PitchClassSetId = "",
            AnalysisEngine = "test",
            AnalysisVersion = "test",
            Jobs = [],
            TuningId = "Standard",
            YamlAnalysis = "",
            IntervalClassVector = "",
        }, 0.5, chordName);

    /// <summary>
    ///     An OPTIC-K strategy that answers <paramref name="named" /> when the search filters on a chord
    ///     name and <paramref name="unfiltered" /> otherwise, recording the chord name of each search.
    /// </summary>
    private sealed class RecordingStrategy(
        List<VoicingSearchResult> named,
        List<VoicingSearchResult> unfiltered) : IVoicingSearchStrategy
    {
        public List<string?> ChordNameFilters { get; } = [];

        public string Name => "OPTK-test";
        public bool IsAvailable => true;
        public QueryVectorSpace QuerySpace => QueryVectorSpace.OpticCompact112;
        public bool RequiresWarmup => false;
        public VoicingSearchPerformance Performance => new(TimeSpan.Zero, 0, false, false);

        public Task InitializeAsync(IEnumerable<VoicingEmbedding> voicings) => Task.CompletedTask;

        public Task<List<VoicingSearchResult>> SemanticSearchAsync(
            double[] queryEmbedding, int limit = 10, CancellationToken cancellationToken = default)
        {
            ChordNameFilters.Add(null);
            return Task.FromResult(unfiltered.Take(limit).ToList());
        }

        public Task<List<VoicingSearchResult>> HybridSearchAsync(
            double[] queryEmbedding, VoicingSearchFilters filters, int limit = 10,
            CancellationToken cancellationToken = default)
        {
            ChordNameFilters.Add(filters.ChordName);
            var pool = filters.ChordName is null ? unfiltered : named;
            return Task.FromResult(pool.Take(limit).ToList());
        }

        public Task<List<VoicingSearchResult>> FindSimilarVoicingsAsync(
            string voicingId, int limit = 10, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<VoicingSearchResult>());

        public VoicingSearchStats GetStats() => new(0, 0, TimeSpan.Zero, 0);

        public IReadOnlyList<string> UnsupportedPopulatedFilters(VoicingSearchFilters filters) => [];
    }
}
