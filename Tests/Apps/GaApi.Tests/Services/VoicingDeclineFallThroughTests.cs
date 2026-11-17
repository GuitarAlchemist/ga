namespace GaApi.Tests.Services;

using GA.Business.Core.Orchestration.Models;
using GA.Business.Core.Orchestration.Services;
using GA.Business.ML.Agents;
using GA.Business.ML.Agents.Intents;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Extensions;
using GA.Business.ML.Rag.Models;
using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Core;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using AiChatResponse = Microsoft.Extensions.AI.ChatResponse;
using ChatResponse = GA.Business.Core.Orchestration.Models.ChatResponse;

/// <summary>
/// When every voicing the index returns scores 0, the voicing agent declines and the production
/// orchestrator answers with another agent instead of "Found 10 voicings matching tags [finger,
/// vibrato]" (live chat, 2026-11-16). The voicing agent searches a stub index whose results all
/// score 0; the embedder puts "vibrato" next to the voicing agent's description so the agent
/// router picks it. Runs without Ollama.
/// </summary>
[TestFixture]
public class VoicingDeclineFallThroughTests
{
    private const string AgentAnswer = "Rock the fretting finger from the wrist.";

    [Test]
    public async Task AgentRouterPicksVoicing_VoicingDeclines_NextAgentAnswers()
    {
        var (response, chat) = await AskAsync("How do I develop a good finger vibrato?", streaming: false);

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Does.Not.Contain("voicing"));
            Assert.That(response.Routing?.RoutingMethod, Is.EqualTo("agent-declined-fallthrough"));
            Assert.That(response.Routing?.AgentId, Is.Not.EqualTo(AgentIds.Voicing));
            Assert.That(chat.Calls, Is.GreaterThan(0), "the next agent must answer");
        });
    }

    // The voicing guard fires on the word "voicings"; a declining voicing agent leaves the
    // request to the rest of the cascade.
    [TestCase(false)]
    [TestCase(true)]
    public async Task VoicingGuardFires_VoicingDeclines_CascadeContinues(bool streaming)
    {
        var (response, chat) = await AskAsync("Show me finger vibrato voicings", streaming);

        Assert.Multiple(() =>
        {
            Assert.That(response.NaturalLanguageAnswer, Does.Not.Contain("No voicing"));
            Assert.That(response.NaturalLanguageAnswer, Does.Not.StartWith("Found"));
            Assert.That(response.Routing?.RoutingMethod, Is.Not.EqualTo("deterministic-voicing"));
            Assert.That(chat.Calls, Is.GreaterThan(0), "an LLM-backed agent must answer");
        });
    }

    private static async Task<(ChatResponse Response, FixedChatClient Chat)> AskAsync(string prompt, bool streaming)
    {
        var chat = new FixedChatClient();
        using var factory = new TestWebApplicationFactory().WithWebHostBuilder(builder =>
        {
            // Unreachable Ollama: any call the fakes miss fails fast instead of reaching a local model.
            builder.UseSetting("Ollama:Endpoint", "http://127.0.0.1:9");
            builder.UseSetting("Ollama:BaseUrl", "http://127.0.0.1:9");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IIntent>();
                var embeddings = new VibratoNearVoicingEmbeddingGenerator();
                services.RemoveAll<IEmbeddingGenerator<string, Embedding<float>>>();
                services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(embeddings);
                services.RemoveAll<IEmbeddingGeneratorFactory>();
                services.AddSingleton<IEmbeddingGeneratorFactory>(new FixedEmbeddingGeneratorFactory(embeddings));
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(chat);
                services.RemoveAll<VoicingAgent>();
                services.AddTransient(_ => UngroundedVoicingAgent(chat));
            });
        });
        using var scope = factory.Services.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<ProductionOrchestrator>();
        var request = new ChatRequest(prompt);

        var response = streaming
            ? await orchestrator.AnswerStreamingAsync(request, _ => Task.CompletedTask)
            : await orchestrator.AnswerAsync(request);
        return (response, chat);
    }

    /// <summary>A voicing agent whose index returns voicings that all score 0.</summary>
    private static VoicingAgent UngroundedVoicingAgent(IChatClient chat)
    {
        var results = Enumerable.Range(0, 3)
            .Select(i => new VoicingSearchResult(Document($"v{i}"), 0.0, "query"))
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
            .ReturnsAsync(new StructuredQuery(null, null, null, null, ["finger", "vibrato"]));

        return new VoicingAgent(
            chat,
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

    private sealed class FixedEmbeddingGeneratorFactory(IEmbeddingGenerator<string, Embedding<float>> embeddings) : IEmbeddingGeneratorFactory
    {
        public IEmbeddingGenerator<string, Embedding<float>> Create(string purpose) => embeddings;
    }

    /// <summary>
    /// Embeds "vibrato" questions onto the voicing agent's description (it alone mentions
    /// "OPTIC-K geometry") and everything else onto an orthogonal axis.
    /// </summary>
    private sealed class VibratoNearVoicingEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
    {
        public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
            IEnumerable<string> values,
            EmbeddingGenerationOptions? options = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new GeneratedEmbeddings<Embedding<float>>(values.Select(Embed)));

        private static Embedding<float> Embed(string text) =>
            text.Contains("vibrato", StringComparison.OrdinalIgnoreCase) || text.Contains("OPTIC-K geometry", StringComparison.Ordinal)
                ? new Embedding<float>(new float[] { 1f, 0f, 0f })
                : new Embedding<float>(new float[] { 0f, 1f, 0f });

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class FixedChatClient : IChatClient
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public Task<AiChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            return Task.FromResult(new AiChatResponse(new ChatMessage(ChatRole.Assistant, AgentAnswer)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);
            await Task.Yield();
            yield return new ChatResponseUpdate(ChatRole.Assistant, AgentAnswer);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}
