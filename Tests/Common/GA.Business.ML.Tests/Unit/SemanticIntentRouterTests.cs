namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Intents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestFixture]
public class SemanticIntentRouterTests
{
    private sealed class StubIntent(
        string id,
        string description,
        IReadOnlyList<string> examples,
        string answer = "stub answer") : IIntent
    {
        public string Id => id;
        public string Description => description;
        public IReadOnlyList<string> ExamplePrompts => examples;

        public Task<IntentResult> ExecuteAsync(string query, CancellationToken cancellationToken = default) =>
            Task.FromResult(new IntentResult(answer));
    }

    /// <summary>
    /// Returns an embedding generator that produces deterministic vectors
    /// keyed by string identity — same string → same vector, different
    /// strings → orthogonal-ish vectors. Lets us drive cosine similarity
    /// without standing up a real embedder.
    /// </summary>
    private static IEmbeddingGenerator<string, Embedding<float>> StubEmbedder(
        Dictionary<string, float[]> vectors)
    {
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mock.Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken>(
                (inputs, _, _) =>
                {
                    var fallback = new float[] { 0f, 0f, 0f, 0f };
                    var list = inputs.Select(input =>
                        vectors.TryGetValue(input, out var v)
                            ? new Embedding<float>(v)
                            : new Embedding<float>(fallback))
                        .ToList();
                    return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(list));
                });
        return mock.Object;
    }

    private static IServiceProvider Services(params IIntent[] intents)
    {
        var sc = new ServiceCollection();
        foreach (var intent in intents) sc.AddSingleton(intent);
        return sc.BuildServiceProvider();
    }

    [Test]
    public async Task RouteAsync_ExactMatch_ReturnsIntent()
    {
        var algebraIntent = new StubIntent(
            id: "algebra",
            description: "set theory",
            examples: ["are 0146 and 0137 z-related"],
            answer: "yes Z-related");

        var vectors = new Dictionary<string, float[]>
        {
            ["are 0146 and 0137 z-related"] = [1f, 0f, 0f, 0f],
            ["set theory"]                   = [0.5f, 0f, 0f, 0f],
            // The query the user typed:
            ["are 0146 and 0137 z-related?"] = [1f, 0f, 0f, 0f],
        };

        var router = new SemanticIntentRouter(
            StubEmbedder(vectors),
            NullLogger<SemanticIntentRouter>.Instance);

        var match = await router.RouteAsync("are 0146 and 0137 z-related?", Services(algebraIntent));

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Value.Intent.Id, Is.EqualTo("algebra"));
        Assert.That(match.Value.Confidence, Is.GreaterThan(0.99f));
    }

    [Test]
    public async Task RouteAsync_PicksHighestScorer_AcrossIntents()
    {
        var algebra = new StubIntent("algebra", "set theory",
            ["are these z-related"]);
        var modes = new StubIntent("skill.modes", "modes of major scale",
            ["modes of the major scale"]);

        var vectors = new Dictionary<string, float[]>
        {
            ["are these z-related"]        = [1f, 0f, 0f, 0f],
            ["set theory"]                 = [0.9f, 0.1f, 0f, 0f],
            ["modes of the major scale"]   = [0f, 1f, 0f, 0f],
            ["modes of major scale"]       = [0.1f, 0.9f, 0f, 0f],
            // Query: closer to modes than algebra
            ["what are the modes"]         = [0f, 0.95f, 0f, 0f],
        };

        var router = new SemanticIntentRouter(
            StubEmbedder(vectors),
            NullLogger<SemanticIntentRouter>.Instance);

        var match = await router.RouteAsync("what are the modes", Services(algebra, modes));

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Value.Intent.Id, Is.EqualTo("skill.modes"));
    }

    [Test]
    public async Task RouteAsync_BelowThreshold_ReturnsNull()
    {
        var algebra = new StubIntent("algebra", "set theory",
            ["are these z-related"]);

        var vectors = new Dictionary<string, float[]>
        {
            ["are these z-related"]   = [1f, 0f, 0f, 0f],
            ["set theory"]            = [1f, 0f, 0f, 0f],
            // Query: orthogonal — no match
            ["what is for breakfast"] = [0f, 0f, 0f, 1f],
        };

        var router = new SemanticIntentRouter(
            StubEmbedder(vectors),
            NullLogger<SemanticIntentRouter>.Instance) { MinConfidence = 0.5f };

        var match = await router.RouteAsync("what is for breakfast", Services(algebra));

        Assert.That(match, Is.Null);
    }

    [Test]
    public async Task RouteAsync_NoEmbedder_ReturnsNull()
    {
        var algebra = new StubIntent("algebra", "set theory", ["z-related"]);

        var router = new SemanticIntentRouter(
            textEmbeddings: null,
            NullLogger<SemanticIntentRouter>.Instance);

        var match = await router.RouteAsync("anything", Services(algebra));

        Assert.That(match, Is.Null);
    }

    [Test]
    public async Task RouteAsync_NoIntentsWithExamples_ReturnsNull()
    {
        var bareIntent = new StubIntent("bare", "no examples", []);

        var router = new SemanticIntentRouter(
            StubEmbedder([]),
            NullLogger<SemanticIntentRouter>.Instance);

        var match = await router.RouteAsync("anything", Services(bareIntent));

        Assert.That(match, Is.Null);
    }

    [Test]
    public async Task RouteAsync_NullOrWhitespaceQuery_ReturnsNull()
    {
        var algebra = new StubIntent("algebra", "set theory", ["z-related"]);
        var router = new SemanticIntentRouter(
            StubEmbedder([]),
            NullLogger<SemanticIntentRouter>.Instance);

        Assert.That(await router.RouteAsync("",      Services(algebra)), Is.Null);
        Assert.That(await router.RouteAsync("   ",   Services(algebra)), Is.Null);
        Assert.That(await router.RouteAsync(null!,   Services(algebra)), Is.Null);
    }

    [Test]
    public async Task RouteAsync_ExampleEmbeddingsAreCachedAcrossCalls()
    {
        var algebra = new StubIntent("algebra", "set theory", ["z-related"]);

        var callCount = 0;
        var mock = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        mock.Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken>(
                (inputs, _, _) =>
                {
                    callCount++;
                    var dummy = new float[] { 1f, 0f };
                    var list = inputs.Select(_ => new Embedding<float>(dummy)).ToList();
                    return Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(list));
                });

        var router = new SemanticIntentRouter(
            mock.Object,
            NullLogger<SemanticIntentRouter>.Instance);

        var sp = Services(algebra);
        await router.RouteAsync("first call",  sp);
        await router.RouteAsync("second call", sp);
        await router.RouteAsync("third call",  sp);

        // Three queries → 3 query-embedding calls + 1 startup intent embedding.
        Assert.That(callCount, Is.EqualTo(4),
            "intent example embeddings must be cached across queries; only the per-query embedding repeats");
    }

    [Test]
    public async Task RouteAsync_OnScoreTie_PrefersIntentWithShorterDescription()
    {
        // Reproduces the ChordInfo-vs-KeyIdentification ambiguity: two intents have
        // an example that tie at score=1.0; the SHORTER description should win
        // because it signals tighter scope.
        var chordInfo = new StubIntent(
            id: "skill.chordinfo",
            description: "Returns chord intervals.",
            examples: ["What is a C major chord?"]);

        var keyId = new StubIntent(
            id: "skill.keyidentification",
            description: "Identifies the most likely musical key from a list of chords or notes, " +
                         "with confidence scoring across all 24 major and minor keys.",
            examples: ["What is a C major chord?"]);

        // Both example strings tie at 1.0 against the query; descriptions diverge.
        var queryVec = new float[] { 1f, 0f };
        var vectors = new Dictionary<string, float[]>
        {
            ["What is a C major chord?"] = queryVec,                     // shared example text
            ["Returns chord intervals."] = [0f, 1f],                     // description (orthogonal)
            ["Identifies the most likely musical key from a list of chords or notes, " +
             "with confidence scoring across all 24 major and minor keys."] = [0f, 1f],
        };

        var router = new SemanticIntentRouter(
            StubEmbedder(vectors),
            NullLogger<SemanticIntentRouter>.Instance);

        var match = await router.RouteAsync(
            "What is a C major chord?",
            Services(chordInfo, keyId));

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Value.Intent.Id, Is.EqualTo("skill.chordinfo"),
            "tie at score=1.0 should resolve to the intent with the shorter description");
    }

    [Test]
    public async Task RouteAsync_BareWhatIsXMajor_RoutesToScaleInfoNotLLM()
    {
        // Regression for the user-reported bug: "What is C major?" was falling
        // back to the LLM path because no example prompt was structurally
        // close enough to score above the 0.65 threshold. Fix in PR-pending:
        // ScaleInfoSkill.ExamplePrompts now includes the bare
        // "What is C major?" / "What is A minor?" patterns.
        //
        // This test uses stub embeddings calibrated to mirror the real-world
        // scenario: the bare query has high cosine similarity with the new
        // ScaleInfo example (literal match → 1.0) and lower similarity with
        // ChordInfo's "What is a C major chord?" (different vector → 0.6).
        var scaleInfo = new StubIntent(
            id: "skill.scaleinfo",
            description: "Returns the notes of a major or minor key.",
            examples: ["What is C major?", "What is A minor?", "What notes are in C major?"]);

        var chordInfo = new StubIntent(
            id: "skill.chordinfo",
            description: "Returns chord intervals.",
            examples: ["What is a C major chord?", "What notes are in Dm7?"]);

        var queryVec       = new float[] { 1f, 0f, 0f, 0f };
        var chordExampleV  = new float[] { 0.6f, 0.8f, 0f, 0f };  // close but not literal
        var vectors = new Dictionary<string, float[]>
        {
            ["What is C major?"]               = queryVec,         // literal match, score 1.0
            ["What is A minor?"]               = [0.9f, 0.1f, 0f, 0f],
            ["What notes are in C major?"]     = [0.7f, 0.7f, 0f, 0f],
            ["What is a C major chord?"]       = chordExampleV,
            ["What notes are in Dm7?"]         = [0f, 1f, 0f, 0f],
            ["Returns the notes of a major or minor key."] = [0f, 0f, 1f, 0f],
            ["Returns chord intervals."]                   = [0f, 0f, 0f, 1f],
        };

        var router = new SemanticIntentRouter(
            StubEmbedder(vectors),
            NullLogger<SemanticIntentRouter>.Instance);

        var match = await router.RouteAsync(
            "What is C major?",
            Services(scaleInfo, chordInfo));

        Assert.That(match, Is.Not.Null,
            "bare 'What is C major?' must route — the regression that drove this fix was LLM fallback at 0% confidence");
        Assert.That(match!.Value.Intent.Id, Is.EqualTo("skill.scaleinfo"),
            "bare 'What is X major' phrasing should route to scale-info (key/scale lookup), not chord-info");
        Assert.That(match.Value.Confidence, Is.GreaterThanOrEqualTo(0.65f),
            "must clear the MinConfidence threshold the production router applies");
    }

    [Test]
    public async Task IntentMatch_RecordsMatchedExample()
    {
        var modes = new StubIntent("skill.modes", "modes of major scale",
            ["List the diatonic modes", "What are the modes of the major scale?"]);

        var vectors = new Dictionary<string, float[]>
        {
            // Two examples with distinct vectors; description is orthogonal so
            // it never wins. Query is exactly the first example so it scores 1.0
            // there and < 1.0 elsewhere.
            ["List the diatonic modes"]                 = [1f, 0f, 0f],
            ["What are the modes of the major scale?"]  = [0f, 1f, 0f],
            ["modes of major scale"]                    = [0f, 0f, 1f],
            ["List the diatonic modes please"]          = [1f, 0f, 0f],
        };

        var router = new SemanticIntentRouter(
            StubEmbedder(vectors),
            NullLogger<SemanticIntentRouter>.Instance);

        var match = await router.RouteAsync("List the diatonic modes please", Services(modes));

        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Value.MatchedExample, Is.EqualTo("List the diatonic modes"));
    }
}
