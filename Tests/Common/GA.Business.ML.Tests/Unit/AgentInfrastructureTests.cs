namespace GA.Business.ML.Tests.Unit;

using Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestFixture]
public class AgentInfrastructureTests
{
    [SetUp]
    public void Setup() => _chatClientMock = new();

    private Mock<IChatClient> _chatClientMock;

    [Test]
    public async Task TheoryAgent_FormulatesCorrectPrompt()
    {
        var agent = new TheoryAgent(_chatClientMock.Object, NullLogger<TheoryAgent>.Instance);
        var request = new AgentRequest
        {
            Query = "What are the intervals in a Cmaj7 chord?"
        };

        var jsonResponse = """
                           {
                             "result": "Intervals are Root, Major 3rd, Perfect 5th, Major 7th.",
                             "confidence": 0.95,
                             "evidence": ["Major 7th detected"],
                             "assumptions": []
                           }
                           """;

        _chatClientMock.Setup(c =>
                c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), default))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        var response = await agent.ProcessAsync(request);

        Assert.That(response.Result, Contains.Substring("Intervals"));
        Assert.That(response.Confidence, Is.GreaterThan(0.9f));
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.Is<IEnumerable<ChatMessage>>(m =>
                m.Any(msg => msg.Text != null && msg.Text.Contains("atonal and tonal theory"))),
            It.IsAny<ChatOptions>(),
            default), Times.AtLeastOnce());
    }

    [Test]
    public async Task TabAgent_FormulatesCorrectPrompt()
    {
        var agent = new TabAgent(_chatClientMock.Object, NullLogger<TabAgent>.Instance);
        var request = new AgentRequest
        {
            Query = "e|---0---|\nB|---1---|\nG|---0---|"
        };

        var jsonResponse = """
                           {
                             "result": "This is part of a C major chord.",
                             "confidence": 0.9,
                             "evidence": ["Fret 1 on B string"],
                             "assumptions": ["Standard tuning"]
                           }
                           """;

        _chatClientMock.Setup(c =>
                c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), default))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, jsonResponse)));

        var response = await agent.ProcessAsync(request);

        Assert.That(response.Result, Contains.Substring("C major"));
        Assert.That(response.Confidence, Is.EqualTo(0.9f));
        _chatClientMock.Verify(c => c.GetResponseAsync(
            It.Is<IEnumerable<ChatMessage>>(m =>
                m.Any(msg => msg.Text != null && msg.Text.Contains("guitar tablature"))),
            It.IsAny<ChatOptions>(),
            default), Times.Once);
    }

    [Test]
    public async Task SemanticRouter_RoutesToTheoryAgent()
    {
        var theoryAgent = new TheoryAgent(_chatClientMock.Object, NullLogger<TheoryAgent>.Instance);
        var tabAgent = new TabAgent(_chatClientMock.Object, NullLogger<TabAgent>.Instance);

        var router = new SemanticRouter(
            new GuitarAlchemistAgentBase[] { theoryAgent, tabAgent },
            null, // No ChatClient for this test
            null, // No embeddings for keyword fallback test
            NullLogger<SemanticRouter>.Instance);

        var result = await router.RouteAsync("Explain the circle of fifths");

        Assert.That(result.SelectedAgent, Is.InstanceOf<TheoryAgent>());
        Assert.That(result.RoutingMethod, Is.EqualTo("keyword"));
    }

    [Test]
    public async Task SemanticRouter_EmbeddingBackendDown_FallsBackToKeywordRouting()
    {
        // Offline host: an embedding generator is registered but its backend is
        // unreachable (Ollama not running). Agent-description embedding throws on
        // the first call; RouteAsync must degrade to keyword routing instead of
        // propagating the exception to the chat endpoint (HTTP 500).
        var theoryAgent = new TheoryAgent(_chatClientMock.Object, NullLogger<TheoryAgent>.Instance);
        var tabAgent = new TabAgent(_chatClientMock.Object, NullLogger<TabAgent>.Instance);

        var failingEmbeddings = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        failingEmbeddings
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var router = new SemanticRouter(
            new GuitarAlchemistAgentBase[] { theoryAgent, tabAgent },
            null,
            failingEmbeddings.Object,
            NullLogger<SemanticRouter>.Instance);

        var result = await router.RouteAsync("Explain the circle of fifths");

        Assert.That(result.SelectedAgent, Is.InstanceOf<TheoryAgent>());
        Assert.That(result.RoutingMethod, Is.EqualTo("keyword"));
    }

    [Test]
    public void SemanticRouter_CallerCancellation_StillPropagates()
    {
        var theoryAgent = new TheoryAgent(_chatClientMock.Object, NullLogger<TheoryAgent>.Instance);

        var embeddings = new Mock<IEmbeddingGenerator<string, Embedding<float>>>();
        embeddings
            .Setup(e => e.GenerateAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<EmbeddingGenerationOptions?>(),
                It.IsAny<CancellationToken>()))
            .Returns<IEnumerable<string>, EmbeddingGenerationOptions?, CancellationToken>(
                (_, _, ct) => Task.FromCanceled<GeneratedEmbeddings<Embedding<float>>>(ct));

        var router = new SemanticRouter(
            new GuitarAlchemistAgentBase[] { theoryAgent },
            null,
            embeddings.Object,
            NullLogger<SemanticRouter>.Instance);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(
            async () => await router.RouteAsync("Explain the circle of fifths", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }
}
