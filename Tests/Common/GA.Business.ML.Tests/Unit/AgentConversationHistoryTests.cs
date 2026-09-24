namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Pins that every specialized agent sends <see cref="AgentRequest.ConversationHistory"/> to the
/// LLM on the non-streaming path, between the system prompt and the current query.
/// </summary>
[TestFixture]
public sealed class AgentConversationHistoryTests
{
    private const string PriorUser = "My favorite guitarist is Django Reinhardt.";
    private const string PriorAssistant = "Noted.";
    private const string Query = "Describe his playing style.";

    private static IEnumerable<TestCaseData> Agents()
    {
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new TheoryAgent(chat, NullLogger<TheoryAgent>.Instance))).SetName("Theory");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new TabAgent(chat, NullLogger<TabAgent>.Instance))).SetName("Tab");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new TechniqueAgent(chat, NullLogger<TechniqueAgent>.Instance))).SetName("Technique");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new ComposerAgent(chat, NullLogger<ComposerAgent>.Instance))).SetName("Composer");
        yield return new TestCaseData(new Func<IChatClient, GuitarAlchemistAgentBase>(chat => new CriticAgent(chat, NullLogger<CriticAgent>.Instance))).SetName("Critic");
    }

    [TestCaseSource(nameof(Agents))]
    public async Task ProcessAsync_SendsHistoryBeforeCurrentQuery(Func<IChatClient, GuitarAlchemistAgentBase> create)
    {
        List<ChatMessage>? firstCall = null;
        var chat = new Mock<IChatClient>();
        chat.Setup(client => client.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) => firstCall ??= [.. messages])
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Gypsy jazz.")));

        await create(chat.Object).ProcessAsync(new AgentRequest
        {
            Query = Query,
            ConversationHistory = [new ChatHistoryTurn("user", PriorUser), new ChatHistoryTurn("assistant", PriorAssistant)],
        });

        Assert.That(firstCall, Is.Not.Null);
        Assert.That(firstCall!.Skip(1).Select(message => (message.Role, message.Text)), Is.EqualTo(new[]
        {
            (ChatRole.User, PriorUser),
            (ChatRole.Assistant, PriorAssistant),
            (ChatRole.User, Query),
        }));
    }
}
