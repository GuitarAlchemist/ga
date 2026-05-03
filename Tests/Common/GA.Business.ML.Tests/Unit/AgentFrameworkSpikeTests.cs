namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using GA.Business.ML.Agents.AgentFramework;
using GA.Business.ML.Agents.Skills;
using GA.Business.ML.Extensions;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Phase 3 spike: prove the Agent Framework integration shape works without
/// touching <c>ProductionOrchestrator</c>, per the migration recommendation
/// §"Phase 3 — Agent Framework spike".
/// </summary>
/// <remarks>
/// <para><b>Verified in this fixture:</b></para>
/// <list type="bullet">
///   <item>An <see cref="AIAgent"/> can be constructed over an
///         <see cref="IChatClient"/> resolved through <see cref="IChatClientFactory"/>.</item>
///   <item>A <see cref="FileBasedSkillsProvider"/> attaches via
///         <see cref="ChatClientAgentOptions.AIContextProviders"/>.</item>
///   <item>A deterministic <see cref="IOrchestratorSkill"/> can be exposed as
///         an <see cref="AIFunction"/> for tool-calling without leaking GA
///         types into the function signature.</item>
///   <item>End-to-end <c>agent.RunAsync</c> over a fake <see cref="IChatClient"/>
///         returns the expected text and propagates cancellation.</item>
/// </list>
/// <para>Stack: <c>Microsoft.Agents.AI 1.3.0</c> + <c>Microsoft.Extensions.AI 10.5.1</c>.
/// Earlier preview pairs had an ABI mismatch on <c>ChatOptions.ContinuationToken</c>;
/// the bump lifted that gate. Pin both packages together when bumping.</para>
/// </remarks>
[TestFixture]
public class AgentFrameworkSpikeTests
{
    private static IChatClientFactory FactoryFor(IChatClient client)
    {
        var mock = new Mock<IChatClientFactory>();
        mock.Setup(f => f.Create(It.IsAny<string>())).Returns(client);
        return mock.Object;
    }

    private static IChatClient FakeClient(string responseText)
    {
        var mock = new Mock<IChatClient>();
        mock.Setup(c => c.GetResponseAsync(
                It.IsAny<IEnumerable<ChatMessage>>(),
                It.IsAny<ChatOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, responseText)));
        return mock.Object;
    }

    private static string FreshSkillsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "ga-af-spike-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Test]
    public void SkillsAgentBuilder_BuildsRunnableAgent()
    {
        var skillsDir = FreshSkillsDir();
        try
        {
            var agent = SkillsAgentBuilder.Build(
                FactoryFor(FakeClient("hello from agent")),
                purpose: "skill-md",
                skillsDirectory: skillsDir,
                agentName: "TestAgent");

            Assert.That(agent, Is.Not.Null);
            Assert.That(agent, Is.InstanceOf<ChatClientAgent>());
        }
        finally
        {
            try { Directory.Delete(skillsDir, recursive: true); } catch { }
        }
    }

    [Test]
    public async Task SkillsAgentBuilder_RunsEndToEnd()
    {
        // End-to-end agent run was previously gated by an ABI mismatch between
        // Microsoft.Agents.AI 1.0.0-preview.251028.1 (compiled against MEAI 9.10.1)
        // and our MEAI. Stable Agents.AI 1.3.0 + MEAI 10.5.x removed that gap.
        var skillsDir = FreshSkillsDir();
        try
        {
            var agent = SkillsAgentBuilder.Build(
                FactoryFor(FakeClient("hello from agent")),
                purpose: "skill-md",
                skillsDirectory: skillsDir);

            var session = await agent.CreateSessionAsync();
            var response = await agent.RunAsync("hi", session);

            Assert.That(response.Text, Is.EqualTo("hello from agent"));
        }
        finally
        {
            try { Directory.Delete(skillsDir, recursive: true); } catch { }
        }
    }

    [Test]
    public void SkillsAgentBuilder_RunAsync_PropagatesCancellation()
    {
        var skillsDir = FreshSkillsDir();
        try
        {
            var clientMock = new Mock<IChatClient>();
            clientMock.Setup(c => c.GetResponseAsync(
                    It.IsAny<IEnumerable<ChatMessage>>(),
                    It.IsAny<ChatOptions?>(),
                    It.IsAny<CancellationToken>()))
                .Returns<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>(
                    async (_, _, ct) =>
                    {
                        await Task.Delay(Timeout.Infinite, ct);
                        return new ChatResponse(new ChatMessage(ChatRole.Assistant, "unreachable"));
                    });

            var agent = SkillsAgentBuilder.Build(
                FactoryFor(clientMock.Object),
                purpose: "skill-md",
                skillsDirectory: skillsDir);

            using var cts = new CancellationTokenSource();
            cts.CancelAfter(TimeSpan.FromMilliseconds(50));

            var session = agent.CreateSessionAsync().GetAwaiter().GetResult();
            Assert.That(
                async () => await agent.RunAsync("hi", session, cancellationToken: cts.Token),
                Throws.InstanceOf<OperationCanceledException>());
        }
        finally
        {
            try { Directory.Delete(skillsDir, recursive: true); } catch { }
        }
    }

    [Test]
    public void SkillsAgentBuilder_HasExpectedAgentName()
    {
        var skillsDir = FreshSkillsDir();
        try
        {
            var agent = SkillsAgentBuilder.Build(
                FactoryFor(FakeClient("ok")),
                purpose: "skill-md",
                skillsDirectory: skillsDir,
                agentName: "MyTestAgent");

            // Name flows from ChatClientAgentOptions → ChatClientAgent.Name.
            // Verifying it round-trips proves the options were applied.
            Assert.That(agent.Name, Is.EqualTo("MyTestAgent"));
        }
        finally
        {
            try { Directory.Delete(skillsDir, recursive: true); } catch { }
        }
    }

    [Test]
    public void SkillsAgentBuilder_NullArguments_Throw()
    {
        var skillsDir = FreshSkillsDir();
        try
        {
            Assert.That(
                () => SkillsAgentBuilder.Build(null!, "skill-md", skillsDir),
                Throws.ArgumentNullException);
            Assert.That(
                () => SkillsAgentBuilder.Build(FactoryFor(FakeClient("x")), "", skillsDir),
                Throws.ArgumentException);
            Assert.That(
                () => SkillsAgentBuilder.Build(FactoryFor(FakeClient("x")), "skill-md", ""),
                Throws.ArgumentException);
        }
        finally
        {
            try { Directory.Delete(skillsDir, recursive: true); } catch { }
        }
    }

    [Test]
    public async Task ModesSkill_ExposedAsAIFunction_ReturnsScaleModeAnswer()
    {
        var modes = new ModesSkill(NullLogger<ModesSkill>.Instance);
        var fn = modes.AsAIFunction();

        Assert.That(fn.Name, Is.EqualTo("ga_skill_modes"));
        Assert.That(fn.Description, Does.Contain("modes of the major scale"));

        var args = new Dictionary<string, object?> { ["message"] = "list the modes" };
        var result = await fn.InvokeAsync(new AIFunctionArguments(args));
        var resultText = result?.ToString() ?? string.Empty;

        Assert.That(resultText, Does.Contain("Ionian"));
        Assert.That(resultText, Does.Contain("Lydian"));
        Assert.That(resultText, Does.Contain("Locrian"));
    }

    [Test]
    public async Task AsAIFunction_PropagatesCancellation()
    {
        // Build a skill that blocks until cancellation, wrap it as an AIFunction,
        // and verify cancellation propagates through the function-invocation surface.
        var slowSkill = new Mock<IOrchestratorSkill>();
        slowSkill.Setup(s => s.Name).Returns("Slow");
        slowSkill.Setup(s => s.Description).Returns("test slow skill");
        slowSkill.Setup(s => s.ExecuteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                 .Returns<string, CancellationToken>(async (_, ct) =>
                 {
                     await Task.Delay(Timeout.Infinite, ct);
                     return new GA.Business.ML.Agents.AgentResponse { AgentId = "test", Result = "unreachable", Confidence = 0f };
                 });

        var fn = slowSkill.Object.AsAIFunction();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));

        try
        {
            await fn.InvokeAsync(new AIFunctionArguments(new Dictionary<string, object?>
            {
                ["message"] = "anything",
            }), cts.Token);
            Assert.Fail("expected OperationCanceledException to propagate");
        }
        catch (OperationCanceledException)
        {
            Assert.Pass();
        }
    }

    [Test]
    public void AsAIFunction_NullSkill_Throws()
    {
        IOrchestratorSkill? nullSkill = null;
        Assert.That(
            () => nullSkill!.AsAIFunction(),
            Throws.ArgumentNullException);
    }
}
