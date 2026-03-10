namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Skills;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

[TestFixture]
public class SkillMdDrivenSkillTests
{
    private static SkillMd MakeSkillMd(
        string name = "Test Skill",
        string description = "Test description",
        string[]? triggers = null,
        string body = "You are a helpful assistant.") =>
        new()
        {
            Name        = name,
            Description = description,
            Triggers    = triggers ?? ["test-trigger"],
            Body        = body,
            FilePath    = "<test>",
        };

    private static IMcpToolsProvider EmptyToolsProvider()
    {
        var mock = new Mock<IMcpToolsProvider>();
        mock.Setup(p => p.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return mock.Object;
    }

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [Test]
    public void CanHandle_MessageContainsTrigger_ReturnsTrue()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: ["transpose"]),
            EmptyToolsProvider(),
            new ConfigurationBuilder().Build(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.CanHandle("Can you transpose Am7 up a fifth?"), Is.True);
    }

    [Test]
    public void CanHandle_CaseInsensitiveMatch_ReturnsTrue()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: ["PARSE CHORD"]),
            EmptyToolsProvider(),
            new ConfigurationBuilder().Build(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.CanHandle("parse chord Am7"), Is.True);
    }

    [Test]
    public void CanHandle_MessageMissingAllTriggers_ReturnsFalse()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: ["transpose", "diatonic"]),
            EmptyToolsProvider(),
            new ConfigurationBuilder().Build(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.CanHandle("what is the weather today?"), Is.False);
    }

    [Test]
    public void CanHandle_EmptyTriggersList_ReturnsFalse()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: []),
            EmptyToolsProvider(),
            new ConfigurationBuilder().Build(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.CanHandle("transpose anything"), Is.False);
    }

    // ── Metadata ──────────────────────────────────────────────────────────────

    [Test]
    public void Name_ReturnsSkillMdName()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(name: "GA Chords"),
            EmptyToolsProvider(),
            new ConfigurationBuilder().Build(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.Name, Is.EqualTo("GA Chords"));
    }

    [Test]
    public void Description_ReturnsSkillMdDescription()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(description: "Parses chord symbols"),
            EmptyToolsProvider(),
            new ConfigurationBuilder().Build(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.Description, Is.EqualTo("Parses chord symbols"));
    }

    // ── ExecuteAsync with injected IChatClient ────────────────────────────────

    [Test]
    public async Task ExecuteAsync_SendsSystemPromptFromSkillMdBody()
    {
        var skillMd = MakeSkillMd(body: "You are a chord theory expert.");
        var clientMock = new Mock<IChatClient>();
        clientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "C major")));

        var skill = SkillMdDrivenSkill.ForTesting(
            skillMd, EmptyToolsProvider(),
            NullLogger<SkillMdDrivenSkill>.Instance,
            clientMock.Object);

        await skill.ExecuteAsync("what is C major?");

        clientMock.Verify(c => c.GetResponseAsync(
            It.Is<IEnumerable<ChatMessage>>(msgs =>
                msgs.Any(m => m.Role == ChatRole.System &&
                              m.Text!.Contains("chord theory expert"))),
            It.IsAny<ChatOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_SendsUserMessageToClient()
    {
        var userMessage = "transpose Am7 up a fifth";
        var clientMock = new Mock<IChatClient>();
        clientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Em7")));

        var skill = SkillMdDrivenSkill.ForTesting(
            MakeSkillMd(), EmptyToolsProvider(),
            NullLogger<SkillMdDrivenSkill>.Instance,
            clientMock.Object);

        await skill.ExecuteAsync(userMessage);

        clientMock.Verify(c => c.GetResponseAsync(
            It.Is<IEnumerable<ChatMessage>>(msgs =>
                msgs.Any(m => m.Role == ChatRole.User && m.Text == userMessage)),
            It.IsAny<ChatOptions?>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ReturnsAgentResponseWithCorrectAgentId()
    {
        var clientMock = new Mock<IChatClient>();
        clientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "answer")));

        var skill = SkillMdDrivenSkill.ForTesting(
            MakeSkillMd(name: "GA Chords"), EmptyToolsProvider(),
            NullLogger<SkillMdDrivenSkill>.Instance,
            clientMock.Object);

        var response = await skill.ExecuteAsync("test");

        Assert.That(response.AgentId, Is.EqualTo("skill.md.ga-chords"));
        Assert.That(response.Confidence, Is.EqualTo(0.9f));
        Assert.That(response.Result, Is.EqualTo("answer"));
    }

    [Test]
    public async Task ExecuteAsync_PassesToolsFromProvider_ToClientOptions()
    {
        var toolMock = new Mock<AIFunction>();
        var toolsProviderMock = new Mock<IMcpToolsProvider>();
        toolsProviderMock
            .Setup(p => p.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([toolMock.Object]);

        var clientMock = new Mock<IChatClient>();
        clientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        var skill = SkillMdDrivenSkill.ForTesting(
            MakeSkillMd(), toolsProviderMock.Object,
            NullLogger<SkillMdDrivenSkill>.Instance,
            clientMock.Object);

        await skill.ExecuteAsync("test");

        clientMock.Verify(c => c.GetResponseAsync(
            It.IsAny<IEnumerable<ChatMessage>>(),
            It.Is<ChatOptions?>(opts => opts != null && opts.Tools!.Count == 1),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_ClientThrows_ReturnsErrorResponse()
    {
        var clientMock = new Mock<IChatClient>();
        clientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("timeout"));

        var skill = SkillMdDrivenSkill.ForTesting(
            MakeSkillMd(name: "Test"), EmptyToolsProvider(),
            NullLogger<SkillMdDrivenSkill>.Instance,
            clientMock.Object);

        var response = await skill.ExecuteAsync("test");

        Assert.That(response.Confidence, Is.EqualTo(0f));
        Assert.That(response.Result, Contains.Substring("error"));
    }

    // ── Missing API key (no test client override) ─────────────────────────────

    [Test]
    public async Task ExecuteAsync_MissingApiKey_ReturnsErrorResponse()
    {
        // ExecuteAsync never throws (except OperationCanceledException) — configuration errors
        // are caught and returned as a zero-confidence error response so the chatbot pipeline
        // can degrade gracefully rather than crashing mid-stream.
        var original = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", null);

        try
        {
            var skill = new SkillMdDrivenSkill(
                MakeSkillMd(),
                EmptyToolsProvider(),
                new ConfigurationBuilder().Build(),
                NullLogger<SkillMdDrivenSkill>.Instance);

            var response = await skill.ExecuteAsync("test");

            Assert.That(response.Confidence, Is.EqualTo(0f));
            Assert.That(response.Result, Contains.Substring("error"));
        }
        finally
        {
            if (original is not null)
                Environment.SetEnvironmentVariable("ANTHROPIC_API_KEY", original);
        }
    }
}
