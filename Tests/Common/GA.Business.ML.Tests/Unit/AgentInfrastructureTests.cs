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
}
