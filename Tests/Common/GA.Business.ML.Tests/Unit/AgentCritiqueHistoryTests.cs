namespace GA.Business.ML.Tests.Unit;

using GA.Business.ML.Agents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

/// <summary>
/// Pins that the self-critique passes see the conversation, not only the draft. Without it, a
/// follow-up such as "How many flats does it have?" reads as ambiguous to the critique, and the
/// rewrite adds "the question does not name a key" although the previous turn named C minor.
/// </summary>
[TestFixture]
public sealed class AgentCritiqueHistoryTests
{
    private const string PriorUser = "What is the relative minor of E-flat major?";
    private const string PriorAssistant = "C minor.";

    [Test]
    public async Task EveryCritiquePass_SeesTheConversation()
    {
        List<List<ChatMessage>> calls = [];
        var chat = new Mock<IChatClient>();
        chat.Setup(client => client.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) => calls.Add([.. messages]))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Three flats.")));

        await new TheoryAgent(chat.Object, NullLogger<TheoryAgent>.Instance).ProcessAsync(new AgentRequest
        {
            Query = "How many flats does it have?",
            ConversationHistory = [new ChatHistoryTurn("user", PriorUser), new ChatHistoryTurn("assistant", PriorAssistant)],
        });

        Assert.That(calls, Has.Count.EqualTo(3), "draft, critique and rewrite");
        Assert.Multiple(() =>
        {
            foreach (var call in calls)
            {
                Assert.That(call.Skip(1).Take(2).Select(message => (message.Role, message.Text)), Is.EqualTo(new[]
                {
                    (ChatRole.User, PriorUser),
                    (ChatRole.Assistant, PriorAssistant),
                }));
            }
        });
    }
}
