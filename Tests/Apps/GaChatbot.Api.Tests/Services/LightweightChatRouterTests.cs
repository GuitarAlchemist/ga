namespace GaChatbot.Api.Tests.Services;

using GA.Business.Core.Orchestration.Models;
using GaChatbot.Api.Services;

[TestFixture]
public sealed class LightweightChatRouterTests
{
    [TestCase("hi", "direct", PromptProfile.Direct)]
    [TestCase("Hi there", "direct", PromptProfile.Direct)]
    [TestCase("good morning, can you help?", "direct", PromptProfile.Direct)]
    [TestCase("Which modes work over G7?", "theory-lite", PromptProfile.Theory)]
    [TestCase("What is Phrygian mode?", "theory-lite", PromptProfile.Theory)]
    [TestCase("What is a C major chord?", "theory-lite", PromptProfile.Theory)]
    [TestCase("Show me Cmaj7 chord voicings", "voicing-lite", PromptProfile.Voicing)]
    [TestCase("Can you transcribe this riff as tab?", "tab-lite", PromptProfile.Tab)]
    [TestCase("Improve this ii-V-I progression", "critic-lite", PromptProfile.Critic)]
    public void Route_ClassifiesCommonChatbotPrompts(
        string message,
        string expectedAgentId,
        PromptProfile expectedProfile)
    {
        var router = new LightweightChatRouter();

        var decision = router.Route(message);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Routing.AgentId, Is.EqualTo(expectedAgentId));
            Assert.That(decision.PromptProfile, Is.EqualTo(expectedProfile));
        });
    }

    [TestCase("Which modes work over G7?")]
    [TestCase("This chord sounds tense. Why?")]
    [TestCase("Which chord should I use next?")]
    public void Route_DoesNotTreatGreetingFragmentsInsideWordsAsGreeting(string message)
    {
        var router = new LightweightChatRouter();

        var decision = router.Route(message);

        Assert.That(decision.Routing.AgentId, Is.Not.EqualTo("direct"));
    }

    [TestCase("What about minor?", "theory-lite", PromptProfile.Theory)]
    [TestCase("How about a smaller version?", "voicing-lite", PromptProfile.Voicing)]
    [TestCase("Can you continue that?", "tab-lite", PromptProfile.Tab)]
    public void Route_UsesRecentHistoryForShortFollowUps(
        string message,
        string expectedAgentId,
        PromptProfile expectedProfile)
    {
        var router = new LightweightChatRouter();
        List<ConversationTurn> history = expectedProfile switch
        {
            PromptProfile.Theory =>
            [
                new ConversationTurn("user", "Explain the C major chord.", DateTimeOffset.UtcNow),
                new ConversationTurn("assistant", "C major contains C, E, and G.", DateTimeOffset.UtcNow)
            ],
            PromptProfile.Voicing =>
            [
                new ConversationTurn("user", "Show me Cmaj7 voicings.", DateTimeOffset.UtcNow),
                new ConversationTurn("assistant", "Use a compact drop 2 shape.", DateTimeOffset.UtcNow)
            ],
            _ =>
            [
                new ConversationTurn("user", "Write this riff as tab.", DateTimeOffset.UtcNow),
                new ConversationTurn("assistant", "Here is a simple tablature outline.", DateTimeOffset.UtcNow)
            ]
        };

        var decision = router.Route(message, history);

        Assert.Multiple(() =>
        {
            Assert.That(decision.Routing.AgentId, Is.EqualTo(expectedAgentId));
            Assert.That(decision.Routing.RoutingMethod, Is.EqualTo("lightweight-router-context"));
            Assert.That(decision.PromptProfile, Is.EqualTo(expectedProfile));
        });
    }
}
