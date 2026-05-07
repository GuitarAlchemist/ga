namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents.Plugins;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Extensions;
using GA.Business.ML.Skills;
using Microsoft.Extensions.AI;
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

    /// <summary>
    /// Returns an <see cref="IChatClientFactory"/> that hands out the supplied client
    /// for the <c>skill-md</c> purpose (and any purpose, for simplicity in tests).
    /// </summary>
    private static IChatClientFactory FactoryFor(IChatClient client)
    {
        var mock = new Mock<IChatClientFactory>();
        mock.Setup(f => f.Create(It.IsAny<string>())).Returns(client);
        return mock.Object;
    }

    /// <summary>
    /// Returns an <see cref="IChatClientFactory"/> that throws on <c>Create</c> —
    /// simulates a misconfigured provider (e.g. missing API key) without dragging
    /// any vendor SDK into the test.
    /// </summary>
    private static IChatClientFactory ThrowingFactory(string message)
    {
        var mock = new Mock<IChatClientFactory>();
        mock.Setup(f => f.Create(It.IsAny<string>()))
            .Throws(new InvalidOperationException(message));
        return mock.Object;
    }

    private static IChatClientFactory NoopFactory() =>
        // CanHandle / metadata tests never resolve the client; this factory will
        // still throw if invoked but the lazy guarantees that won't happen for
        // tests that don't call ExecuteAsync.
        ThrowingFactory("noop factory — should never be invoked in this test");

    // ── CanHandle ─────────────────────────────────────────────────────────────

    [Test]
    public void CanHandle_MessageContainsTrigger_ReturnsTrue()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: ["transpose"]),
            EmptyToolsProvider(),
            NoopFactory(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.CanHandle("Can you transpose Am7 up a fifth?"), Is.True);
    }

    [Test]
    public void CanHandle_CaseInsensitiveMatch_ReturnsTrue()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: ["PARSE CHORD"]),
            EmptyToolsProvider(),
            NoopFactory(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.CanHandle("parse chord Am7"), Is.True);
    }

    [Test]
    public void CanHandle_MessageMissingAllTriggers_ReturnsFalse()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: ["transpose", "diatonic"]),
            EmptyToolsProvider(),
            NoopFactory(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.CanHandle("what is the weather today?"), Is.False);
    }

    [Test]
    public void CanHandle_EmptyTriggersList_ReturnsFalse()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(triggers: []),
            EmptyToolsProvider(),
            NoopFactory(),
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
            NoopFactory(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.Name, Is.EqualTo("GA Chords"));
    }

    [Test]
    public void Description_ReturnsSkillMdDescription()
    {
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(description: "Parses chord symbols"),
            EmptyToolsProvider(),
            NoopFactory(),
            NullLogger<SkillMdDrivenSkill>.Instance);

        Assert.That(skill.Description, Is.EqualTo("Parses chord symbols"));
    }

    // ── ExecuteAsync with injected IChatClient via factory ────────────────────

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

        var skill = new SkillMdDrivenSkill(
            skillMd,
            EmptyToolsProvider(),
            FactoryFor(clientMock.Object),
            NullLogger<SkillMdDrivenSkill>.Instance);

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

        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(),
            EmptyToolsProvider(),
            FactoryFor(clientMock.Object),
            NullLogger<SkillMdDrivenSkill>.Instance);

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

        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(name: "GA Chords"),
            EmptyToolsProvider(),
            FactoryFor(clientMock.Object),
            NullLogger<SkillMdDrivenSkill>.Instance);

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

        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(),
            toolsProviderMock.Object,
            FactoryFor(clientMock.Object),
            NullLogger<SkillMdDrivenSkill>.Instance);

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

        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(name: "Test"),
            EmptyToolsProvider(),
            FactoryFor(clientMock.Object),
            NullLogger<SkillMdDrivenSkill>.Instance);

        var response = await skill.ExecuteAsync("test");

        Assert.That(response.Confidence, Is.EqualTo(0f));
        Assert.That(response.Result, Contains.Substring("error"));
    }

    [Test]
    public async Task ExecuteAsync_EmptyResponseText_ReturnsZeroConfidenceFailure()
    {
        // PR #151 review (rel-005 / corr-1): an empty/whitespace LLM response
        // — model hit the tool-loop iteration cap, returned only tool_use
        // blocks, or refused — must NOT be forwarded as a successful answer
        // with Confidence=0.9. The chatbot UI would surface a blank bubble
        // and MemoryHook (Confidence>=0.7 + length>=100 check, but the 0.9
        // gate alone is the smell). Guard: empty text → Confidence=0 +
        // explicit "model returned an empty response" message.
        var clientMock = new Mock<IChatClient>();
        clientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "")));

        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(name: "Test"),
            EmptyToolsProvider(),
            FactoryFor(clientMock.Object),
            NullLogger<SkillMdDrivenSkill>.Instance);

        var response = await skill.ExecuteAsync("test");

        Assert.Multiple(() =>
        {
            Assert.That(response.Confidence, Is.EqualTo(0f),
                "empty LLM response must NOT pass through with Confidence=0.9");
            Assert.That(response.Result, Is.Not.Empty,
                "fallback message must be present so the UI doesn't render a blank bubble");
            Assert.That(response.Result.ToLowerInvariant(), Does.Contain("empty"));
        });
    }

    // ── Provider configuration failures (factory-driven) ──────────────────────

    [Test]
    public async Task ExecuteAsync_FactoryThrowsForMissingProvider_ReturnsErrorResponse()
    {
        // ExecuteAsync never throws (except OperationCanceledException) — provider
        // misconfiguration surfaces as a zero-confidence error so the chatbot pipeline
        // can degrade gracefully rather than crashing mid-stream. Replaces the previous
        // ANTHROPIC_API_KEY env-var test, which depended on process-wide state.
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(),
            EmptyToolsProvider(),
            ThrowingFactory("Anthropic chat client requested but no API key is configured."),
            NullLogger<SkillMdDrivenSkill>.Instance);

        var response = await skill.ExecuteAsync("test");

        Assert.That(response.Confidence, Is.EqualTo(0f));
        Assert.That(response.Result, Contains.Substring("error"));
    }

    [Test]
    public void Constructor_RequestsSkillMdPurpose_FromFactory()
    {
        // Documents the contract that callers depend on: SkillMdDrivenSkill always
        // resolves its IChatClient via the "skill-md" purpose. Future refactors that
        // change this string should update this test deliberately.
        var clientMock = new Mock<IChatClient>();
        clientMock
            .Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        var factoryMock = new Mock<IChatClientFactory>();
        factoryMock.Setup(f => f.Create("skill-md")).Returns(clientMock.Object);

        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(),
            EmptyToolsProvider(),
            factoryMock.Object,
            NullLogger<SkillMdDrivenSkill>.Instance);

        // First execute triggers the lazy.
        _ = skill.ExecuteAsync("test").Result;

        factoryMock.Verify(f => f.Create("skill-md"), Times.Once);
        factoryMock.Verify(f => f.Create(It.IsNotIn("skill-md")), Times.Never);
    }
}
