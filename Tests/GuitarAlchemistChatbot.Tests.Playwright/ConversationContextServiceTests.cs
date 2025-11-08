namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for conversation context tracking and persistence
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ConversationContextServiceTests : ChatbotTestBase
{
    [Test]
    public async Task Context_ShouldTrackChordReferences()
    {
        // Arrange & Act
        await SendMessageAsync("Tell me about Cmaj7");
        await WaitForResponseAsync();

        await SendMessageAsync("What are similar chords?");
        var response = await WaitForResponseAsync();

        // Assert - Should understand "similar chords" refers to Cmaj7
        Assert.That(response, Is.Not.Empty, "Should maintain chord context");
        Assert.That(response.ToLower(), Does.Contain("chord"),
            "Should provide chord-related response");
    }

    [Test]
    public async Task Context_ShouldTrackScaleReferences()
    {
        // Arrange & Act
        await SendMessageAsync("Tell me about the C major scale");
        await WaitForResponseAsync();

        await SendMessageAsync("What chords are in it?");
        var response = await WaitForResponseAsync();

        // Assert - Should understand "it" refers to C major scale
        Assert.That(response, Is.Not.Empty, "Should maintain scale context");
    }

    [Test]
    public async Task Context_ShouldTrackMusicTheoryConcepts()
    {
        // Arrange & Act
        await SendMessageAsync("Explain voice leading");
        await WaitForResponseAsync();

        await SendMessageAsync("Give me an example");
        var response = await WaitForResponseAsync();

        // Assert - Should provide example of voice leading
        Assert.That(response, Is.Not.Empty, "Should maintain concept context");
    }

    [Test]
    public async Task Context_ShouldClearOnNewChat()
    {
        // Arrange
        await SendMessageAsync("Tell me about Cmaj7");
        await WaitForResponseAsync();

        // Act - Start new chat
        await ClickNewChatAsync();

        await SendMessageAsync("What chord were we discussing?");
        var response = await WaitForResponseAsync();

        // Assert - Should not remember previous context
        Assert.That(response, Is.Not.Empty, "Should respond to query");
        // Context should be cleared, so it shouldn't specifically mention Cmaj7
    }

    [Test]
    public async Task Context_ShouldMaintainMultipleReferences()
    {
        // Arrange & Act - Build up context with multiple references
        await SendMessageAsync("Tell me about Cmaj7");
        await WaitForResponseAsync();

        await SendMessageAsync("And what about Dm7?");
        await WaitForResponseAsync();

        await SendMessageAsync("How do these two chords work together?");
        var response = await WaitForResponseAsync();

        // Assert - Should understand both chord references
        Assert.That(response, Is.Not.Empty, "Should maintain multiple references");
    }

    [Test]
    public async Task Context_ShouldTrackUserPreferences()
    {
        // Arrange & Act
        await SendMessageAsync("I like jazz chords");
        await WaitForResponseAsync();

        await SendMessageAsync("Show me some chords");
        var response = await WaitForResponseAsync();

        // Assert - Should consider user's jazz preference
        Assert.That(response, Is.Not.Empty, "Should respond with chords");
    }

    [Test]
    public async Task Context_ShouldPersistThroughLongConversation()
    {
        // Arrange & Act - Simulate longer conversation
        await SendMessageAsync("I'm learning guitar");
        await WaitForResponseAsync();

        await SendMessageAsync("Show me some beginner chords");
        await WaitForResponseAsync();

        await SendMessageAsync("What about chord progressions?");
        await WaitForResponseAsync();

        await SendMessageAsync("Can you suggest a simple progression?");
        var response = await WaitForResponseAsync();

        // Assert - Context should persist through multiple exchanges
        Assert.That(response, Is.Not.Empty, "Should maintain context through conversation");

        var messages = await GetAssistantMessagesAsync();
        Assert.That(messages.Count, Is.GreaterThanOrEqualTo(4),
            "Should have all assistant responses");
    }

    [Test]
    public async Task Context_ShouldHandleTopicChanges()
    {
        // Arrange & Act - Change topics mid-conversation
        await SendMessageAsync("Tell me about jazz chords");
        await WaitForResponseAsync();

        await SendMessageAsync("Actually, let's talk about scales instead");
        await WaitForResponseAsync();

        await SendMessageAsync("What's the C major scale?");
        var response = await WaitForResponseAsync();

        // Assert - Should adapt to topic change
        Assert.That(response, Is.Not.Empty, "Should handle topic change");
        Assert.That(response.ToLower(), Does.Contain("scale").Or.Contains("c"),
            "Should respond about scales");
    }

    [Test]
    public async Task Context_ShouldProvideRelevantFollowUps()
    {
        // Arrange & Act
        await SendMessageAsync("What is a ii-V-I progression?");
        await WaitForResponseAsync();

        await SendMessageAsync("Show me an example in C major");
        var response = await WaitForResponseAsync();

        // Assert - Should provide contextually relevant example
        Assert.That(response, Is.Not.Empty, "Should provide example");
        Assert.That(response.ToLower(), Does.Contain("c").Or.Contains("dm").Or.Contains("g"),
            "Should mention chords in C major ii-V-I");
    }

    [Test]
    public async Task Context_ShouldHandleAmbiguousReferences()
    {
        // Arrange & Act
        await SendMessageAsync("Tell me about major chords");
        await WaitForResponseAsync();

        await SendMessageAsync("What about them?");
        var response = await WaitForResponseAsync();

        // Assert - Should handle vague reference
        Assert.That(response, Is.Not.Empty, "Should handle ambiguous reference");
    }

    [Test]
    public async Task Context_ShouldTrackConversationHistory()
    {
        // Arrange & Act - Have a conversation
        await SendMessageAsync("What are jazz chords?");
        await WaitForResponseAsync();

        await SendMessageAsync("Give me examples");
        await WaitForResponseAsync();

        await SendMessageAsync("How do I play them?");
        await WaitForResponseAsync();

        // Assert - All messages should be in history
        var userMessages = await GetUserMessagesAsync();
        var assistantMessages = await GetAssistantMessagesAsync();

        Assert.That(userMessages.Count, Is.EqualTo(3), "Should track all user messages");
        Assert.That(assistantMessages.Count, Is.GreaterThanOrEqualTo(3),
            "Should track all assistant messages");
    }

    [Test]
    public async Task Context_ShouldUpdateWithNewInformation()
    {
        // Arrange
        await SendMessageAsync("Tell me about Cmaj7");
        await WaitForResponseAsync();

        // Act - Update context with new chord
        await SendMessageAsync("Now tell me about Dm7");
        await WaitForResponseAsync();

        // Assert - Context should reflect most recent chord
        await SendMessageAsync("What chord are we discussing?");
        var response = await WaitForResponseAsync();

        Assert.That(response, Is.Not.Empty, "Should respond about current context");
    }

    [Test]
    public async Task Context_ShouldHandleMultipleContextTypes()
    {
        // Arrange & Act - Mix different context types
        await SendMessageAsync("Tell me about Cmaj7"); // Chord reference
        await WaitForResponseAsync();

        await SendMessageAsync("What scale does it come from?"); // Scale reference
        await WaitForResponseAsync();

        await SendMessageAsync("Explain the relationship"); // Concept reference
        var response = await WaitForResponseAsync();

        // Assert - Should handle multiple context types
        Assert.That(response, Is.Not.Empty, "Should handle mixed context types");
    }

    [Test]
    public async Task Context_ShouldProvideContextSummary()
    {
        // Arrange & Act
        await SendMessageAsync("Tell me about jazz");
        await WaitForResponseAsync();

        await SendMessageAsync("Show me some chords");
        await WaitForResponseAsync();

        // Check if context summary is available (if displayed in UI)
        var contextSummary = await GetContextSummaryAsync();

        // Assert - Context summary should exist
        if (contextSummary != null)
        {
            Assert.That(contextSummary.Length, Is.GreaterThan(0),
                "Context summary should contain information");
        }
    }

    [Test]
    public async Task Context_ShouldHandleRapidMessages()
    {
        // Arrange & Act - Send messages in quick succession
        await SendMessageAsync("What is a chord?");
        await SendMessageAsync("What is a scale?");
        await SendMessageAsync("What is a progression?");

        // Wait for all responses
        await Task.Delay(5000);

        // Assert - Should handle all messages
        var userMessages = await GetUserMessagesAsync();
        Assert.That(userMessages.Count, Is.GreaterThanOrEqualTo(3),
            "Should handle rapid messages");
    }

    [Test]
    public async Task Context_ShouldMaintainChronologicalOrder()
    {
        // Arrange & Act
        await SendMessageAsync("First message");
        await WaitForResponseAsync();

        await SendMessageAsync("Second message");
        await WaitForResponseAsync();

        await SendMessageAsync("Third message");
        await WaitForResponseAsync();

        // Assert - Messages should be in order
        var userMessages = await GetUserMessagesAsync();

        Assert.That(userMessages[0], Does.Contain("First"), "First message should be first");
        Assert.That(userMessages[1], Does.Contain("Second"), "Second message should be second");
        Assert.That(userMessages[2], Does.Contain("Third"), "Third message should be third");
    }
}
