namespace GaApi.Tests.Services;

#pragma warning disable SKEXP0001

using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Search;
using GaApi.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
///     Relevance of <c>POST /api/voicings/retrieve</c> on the real OPTIC-K index: each probe names a
///     chord, and a hit is a voicing with that root and quality (any inversion or voicing tag).
///     Baseline before the chord-name ranking: Recall@1 2/5, Recall@5 2/5.
///     Needs <c>state/voicings/optick.index</c> (or <c>GA_OPTICK_INDEX_PATH</c>); ignored without it.
/// </summary>
[TestFixture]
[Category("Integration")]
public class SemanticKnowledgeSourceRecallTests
{
    private static readonly (string Query, string Chord)[] Probes =
    [
        ("open G major chord", "G"),
        ("F major barre chord", "F"),
        ("E7#9 Hendrix chord", "E7#9"),
        ("Cmaj7 drop 2 voicing", "Cmaj7"),
        ("Dm7 jazz voicing", "Dm7"),
    ];

    [Test]
    public async Task NamedChordProbes_ReturnTheNamedChordFirst()
    {
        var indexPath = FindIndex();
        if (indexPath is null)
        {
            Assert.Ignore("optick.index not found; set GA_OPTICK_INDEX_PATH.");
        }

        using var strategy = new OptickSearchStrategy(indexPath);
        var source = new SemanticKnowledgeSource(
            new EnhancedVoicingSearchService(new VoicingIndexingService(), strategy),
            new OllamaEmbeddingService(
                Mock.Of<IHttpClientFactory>(f => f.CreateClient(It.IsAny<string>()) == new HttpClient()),
                new ConfigurationBuilder().Build(),
                NullLogger<OllamaEmbeddingService>.Instance),
            new TypedMusicalQueryExtractor(),
            new MusicalQueryEncoder(new ModalVectorService()),
            NullLogger<SemanticKnowledgeSource>.Instance);

        int recallAt1 = 0, recallAt5 = 0;
        foreach (var (query, chord) in Probes)
        {
            var names = (await source.SearchAsync(query, 5, CancellationToken.None))
                .Select(result => ChordNameOf(result.Content))
                .ToList();
            var hits = names.Select(name => IsChord(name, chord)).ToList();
            recallAt1 += hits.FirstOrDefault() ? 1 : 0;
            recallAt5 += hits.Any(hit => hit) ? 1 : 0;
            await TestContext.Out.WriteLineAsync($"{query}: {string.Join(" | ", names)}");
        }

        await TestContext.Out.WriteLineAsync($"Recall@1 {recallAt1}/{Probes.Length}, Recall@5 {recallAt5}/{Probes.Length}");
        Assert.Multiple(() =>
        {
            Assert.That(recallAt1, Is.EqualTo(Probes.Length), "Recall@1");
            Assert.That(recallAt5, Is.EqualTo(Probes.Length), "Recall@5");
        });
    }

    // The snippet starts with "## <index chord name>", e.g. "## E7#9/Ab" or "## Cmaj7(shell)".
    private static string ChordNameOf(string snippet) =>
        snippet.Split('\n')[0].TrimStart('#', ' ').Trim();

    // Same root and quality: drop the slash bass and any "(tag)" from the stored name.
    private static bool IsChord(string storedName, string chord)
    {
        var name = storedName.Split('/')[0];
        var paren = name.IndexOf('(');
        return (paren >= 0 ? name[..paren] : name) == chord;
    }

    private static string? FindIndex()
    {
        var env = Environment.GetEnvironmentVariable("GA_OPTICK_INDEX_PATH");
        if (!string.IsNullOrWhiteSpace(env) && File.Exists(env)) return env;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "state", "voicings", "optick.index");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }
}
