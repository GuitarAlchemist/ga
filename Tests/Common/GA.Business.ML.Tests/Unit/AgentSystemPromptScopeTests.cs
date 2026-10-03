namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Pins the scope rules every specialized agent sends in its system prompt: the public chatbot
/// must not leak internal agent names ("outside the scope of the Critic Agent") and must decline
/// off-topic requests instead of attempting them.
/// </summary>
[TestFixture]
public sealed class AgentSystemPromptScopeTests
{
    private static IEnumerable<TestCaseData> Agents()
    {
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new TheoryAgent(chat, NullLogger<TheoryAgent>.Instance))).SetName("Theory");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new TabAgent(chat, NullLogger<TabAgent>.Instance))).SetName("Tab");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new TechniqueAgent(chat, NullLogger<TechniqueAgent>.Instance))).SetName("Technique");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new ComposerAgent(chat, NullLogger<ComposerAgent>.Instance))).SetName("Composer");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new CriticAgent(chat, NullLogger<CriticAgent>.Instance))).SetName("Critic");
    }

    [TestCaseSource(nameof(Agents))]
    public async Task SystemPrompt_KeepsInternalsPrivateAndDeclinesOffTopicRequests(Func<IChatClient, GuitarAlchemistAgentBase> create)
    {
        List<ChatMessage>? firstCall = null;
        var chat = new Mock<IChatClient>();
        chat.Setup(client => client.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) => firstCall ??= [.. messages])
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "I'm a guitar and music-theory assistant.")));

        await create(chat.Object).ProcessAsync(new AgentRequest { Query = "can you run a for loop?" });

        Assert.That(firstCall, Is.Not.Null);
        var system = firstCall![0];
        Assert.That(system.Role, Is.EqualTo(ChatRole.System));
        Assert.That(system.Text, Does.Contain("never mention agent names, roles or other internal details"));
        Assert.That(system.Text, Does.Contain("If a request is not about guitar or music, do not attempt it"));
    }
}
