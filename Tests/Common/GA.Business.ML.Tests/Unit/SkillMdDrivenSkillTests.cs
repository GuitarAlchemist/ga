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
        string body = "You are a helpful assistant.",
        string[]? allowedTools = null) =>
        new()
        {
            Name         = name,
            Description  = description,
            Triggers     = triggers ?? ["test-trigger"],
            Body         = body,
            AllowedTools = allowedTools ?? [],
            FilePath     = "<test>",
        };

    private static IMcpToolsProvider EmptyToolsProvider()
    {
        var mock = new Mock<IMcpToolsProvider>();
        mock.Setup(p => p.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        return mock.Object;
    }

    /// <summary>
    /// A real <see cref="AIFunction"/> with a concrete <see cref="AIFunction.Name"/> —
    /// AIFunctionFactory gives a name we can assert against (a Moq'd AIFunction can't
    /// set the non-virtual Name reliably).
    /// </summary>
    private static AIFunction NamedTool(string name) =>
        AIFunctionFactory.Create(() => "ok", name);

    private static IMcpToolsProvider ToolsProvider(params AIFunction[] tools)
    {
        var mock = new Mock<IMcpToolsProvider>();
        mock.Setup(p => p.GetToolsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tools);
        return mock.Object;
    }

    /// <summary>
    /// An <see cref="IChatClient"/> that records the <see cref="ChatOptions"/> it was
    /// called with via <paramref name="captured"/>, so tests can assert tool scoping
    /// and forced tool choice.
    /// </summary>
    private static IChatClient CapturingClient(Action<ChatOptions?> captured)
    {
        var mock = new Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, o, _) => captured(o))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
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

    // ── allowed-tools → forced, scoped tool invocation ────────────────────────
    //
    // The whole point of these tests: a weak model (llama3.2:3b in CI) tends to
    // narrate a plausible answer from training instead of calling the deterministic
    // GA tool, which drops content + grounding. When a SKILL.md declares
    // `allowed-tools`, the skill must (a) scope the visible tool set to those tools
    // and (b) FORCE the tool choice, so the model can't skip the deterministic path.

    [Test]
    public async Task ExecuteAsync_NoAllowedTools_LeavesToolChoiceFree_OverFullSet()
    {
        ChatOptions? captured = null;
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(allowedTools: null),
            ToolsProvider(NamedTool("ga_dsl_eval"), NamedTool("ga_chord_info")),
            FactoryFor(CapturingClient(o => captured = o)),
            NullLogger<SkillMdDrivenSkill>.Instance);

        await skill.ExecuteAsync("test");

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            // Conversational skills declare no allowed-tools: full set, free choice.
            Assert.That(captured!.Tools!, Has.Count.EqualTo(2));
            Assert.That(captured.ToolMode, Is.Null, "no allowed-tools → ToolMode must stay unset (Auto)");
        });
    }

    [Test]
    public async Task ExecuteAsync_SingleAllowedTool_ForcesRequireSpecific_AndScopesToIt()
    {
        ChatOptions? captured = null;
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(allowedTools: ["ga_dsl_eval"]),
            ToolsProvider(NamedTool("ga_dsl_eval"), NamedTool("ga_chord_info"), NamedTool("ga_scale_get_notes")),
            FactoryFor(CapturingClient(o => captured = o)),
            NullLogger<SkillMdDrivenSkill>.Instance);

        await skill.ExecuteAsync("transpose Cmaj7 up a fourth");

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(captured!.Tools!, Has.Count.EqualTo(1), "tool set must be scoped to the single allowed tool");
            Assert.That(captured.Tools![0].Name, Is.EqualTo("ga_dsl_eval"));
            Assert.That(captured.ToolMode, Is.InstanceOf<RequiredChatToolMode>());
            Assert.That(((RequiredChatToolMode)captured.ToolMode!).RequiredFunctionName,
                Is.EqualTo("ga_dsl_eval"), "must force the specific deterministic tool");
        });
    }

    [Test]
    public async Task ExecuteAsync_AllowedToolMatch_IsCaseInsensitive()
    {
        ChatOptions? captured = null;
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(allowedTools: ["GA_DSL_EVAL"]),
            ToolsProvider(NamedTool("ga_dsl_eval"), NamedTool("ga_chord_info")),
            FactoryFor(CapturingClient(o => captured = o)),
            NullLogger<SkillMdDrivenSkill>.Instance);

        await skill.ExecuteAsync("test");

        Assert.That(captured!.Tools!, Has.Count.EqualTo(1));
        Assert.That(((RequiredChatToolMode)captured.ToolMode!).RequiredFunctionName, Is.EqualTo("ga_dsl_eval"));
    }

    [Test]
    public async Task ExecuteAsync_MultipleAllowedTools_ForcesRequireAny_AndScopesToThem()
    {
        ChatOptions? captured = null;
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(allowedTools: ["ga_chord_substitutions", "ga_chord_compare"]),
            ToolsProvider(
                NamedTool("ga_chord_substitutions"),
                NamedTool("ga_chord_compare"),
                NamedTool("ga_dsl_eval")),
            FactoryFor(CapturingClient(o => captured = o)),
            NullLogger<SkillMdDrivenSkill>.Instance);

        await skill.ExecuteAsync("substitutes for Cmaj7");

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(captured!.Tools!, Has.Count.EqualTo(2), "scoped to the two allowed tools");
            Assert.That(captured.ToolMode, Is.InstanceOf<RequiredChatToolMode>());
            // RequireAny forces *some* tool call but names none.
            Assert.That(((RequiredChatToolMode)captured.ToolMode!).RequiredFunctionName, Is.Null);
        });
    }

    [Test]
    public async Task ExecuteAsync_AllowedToolAbsentFromProvider_FallsBackToFreeChoice_FullSet()
    {
        // Declared tool renamed / not wired in the provider set: forcing
        // RequireSpecific on a nonexistent tool would error the call, so the
        // skill must degrade to the full set with a free tool choice.
        ChatOptions? captured = null;
        var skill = new SkillMdDrivenSkill(
            MakeSkillMd(allowedTools: ["ga_does_not_exist"]),
            ToolsProvider(NamedTool("ga_dsl_eval"), NamedTool("ga_chord_info")),
            FactoryFor(CapturingClient(o => captured = o)),
            NullLogger<SkillMdDrivenSkill>.Instance);

        await skill.ExecuteAsync("test");

        Assert.That(captured, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(captured!.Tools!, Has.Count.EqualTo(2), "fall back to the full provider set");
            Assert.That(captured.ToolMode, Is.Null, "absent tool → do NOT force; leave ToolMode unset");
        });
    }
}
