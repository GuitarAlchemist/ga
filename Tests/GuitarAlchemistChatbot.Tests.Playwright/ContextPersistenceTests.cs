namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for conversation context persistence and short-term memory
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ContextPersistenceTests : ChatbotTestBase
{
    [Test]
    public async Task Context_ShouldPersistAcrossMessages()
    {
        // Arrange & Act - First message
        await SendMessageAsync("Tell me about C major chord");
        var response1 = await WaitForResponseAsync();

        // Second message referencing the first
        await SendMessageAsync("What are similar chords to that one?");
        var response2 = await WaitForResponseAsync();

        // Assert
        Assert.That(response1, Is.Not.Empty, "First response should not be empty");
        Assert.That(response2, Is.Not.Empty, "Second response should not be empty");
        Assert.That(response2.ToLower(), Does.Not.Contain("which chord"),
            "AI should understand 'that one' refers to C major");
    }

    [Test]
    public async Task Context_ShouldDisplayContextIndicator()
    {
        // Arrange & Act
        await SendMessageAsync("Show me some jazz chords");
        await WaitForResponseAsync();

        // Assert - Context indicator should appear
        var contextSummary = await GetContextSummaryAsync();
        Assert.That(contextSummary, Is.Not.Null, "Context indicator should be visible");
        Assert.That(contextSummary, Is.Not.Empty, "Context summary should not be empty");
    }

    [Test]
    public async Task Context_ShouldTrackRecentChords()
    {
        // Arrange & Act
        await SendMessageAsync("Tell me about Cmaj7");
        await WaitForResponseAsync();

        await SendMessageAsync("Now tell me about Dm7");
        await WaitForResponseAsync();

        // Assert - Context should show recent chord
        var contextSummary = await GetContextSummaryAsync();
        Assert.That(contextSummary, Is.Not.Null, "Context should be tracked");

        // Context might contain chord names or "Last chord" indicator
        if (contextSummary != null)
        {
            Assert.That(contextSummary.Length, Is.GreaterThan(0),
                "Context summary should contain information");
        }
    }

    [Test]
    public async Task Context_ShouldClearOnNewChat()
    {
        // Arrange
        await SendMessageAsync("Tell me about C major");
        await WaitForResponseAsync();

        // Verify context exists
        var contextBefore = await GetContextSummaryAsync();
        Assert.That(contextBefore, Is.Not.Null, "Context should exist before reset");

        // Act - Click new chat
        await ClickNewChatAsync();

        // Assert - Context should be cleared
        var contextAfter = await GetContextSummaryAsync();
        Assert.That(contextAfter, Is.Null.Or.Empty, "Context should be cleared after new chat");

        // Messages should be cleared
        var userMessages = await GetUserMessagesAsync();
        Assert.That(userMessages, Is.Empty, "User messages should be cleared");
    }

    [Test]
    public async Task Context_ShouldHandleMultipleReferences()
    {
        // Arrange & Act - Build up context
        await SendMessageAsync("What is a Cmaj7 chord?");
        await WaitForResponseAsync();

        await SendMessageAsync("What about Dm7?");
        await WaitForResponseAsync();

        await SendMessageAsync("How do these two chords work together?");
        var response = await WaitForResponseAsync();

        // Assert - AI should understand "these two chords" refers to Cmaj7 and Dm7
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Not.Contain("which chords"),
            "AI should understand the context");
    }

    [Test]
    public async Task Context_ShouldTrackMusicTheoryConcepts()
    {
        // Arrange & Act
        await SendMessageAsync("Explain the circle of fifths");
        await WaitForResponseAsync();

        await SendMessageAsync("How does that relate to chord progressions?");
        var response = await WaitForResponseAsync();

        // Assert - AI should understand "that" refers to circle of fifths
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Not.Contain("what are you referring to"),
            "AI should maintain concept context");
    }

    [Test]
    public async Task Context_ShouldPersistDuringLongConversation()
    {
        // Arrange & Act - Simulate a longer conversation
        await SendMessageAsync("Tell me about jazz chord progressions");
        await WaitForResponseAsync();

        await SendMessageAsync("What's a ii-V-I progression?");
        await WaitForResponseAsync();

        await SendMessageAsync("Give me an example in C major");
        await WaitForResponseAsync();

        await SendMessageAsync("What chords would that be?");
        var finalResponse = await WaitForResponseAsync();

        // Assert - Context should persist through multiple exchanges
        Assert.That(finalResponse, Is.Not.Empty, "Should maintain context through conversation");

        // Should still have context indicator
        var contextSummary = await GetContextSummaryAsync();
        Assert.That(contextSummary, Is.Not.Null, "Context should persist");
    }

    [Test]
    public async Task Context_ShouldShowRecentTopics()
    {
        // Arrange & Act
        await SendMessageAsync("Tell me about modes");
        await WaitForResponseAsync();

        await SendMessageAsync("What about chord voicings?");
        await WaitForResponseAsync();

        // Assert - Context should show recent topics
        var contextSummary = await GetContextSummaryAsync();
        Assert.That(contextSummary, Is.Not.Null, "Context should track topics");

        if (contextSummary != null)
        {
            // Context might show "Recent topics" or similar
            Assert.That(contextSummary.Length, Is.GreaterThan(10),
                "Context summary should contain meaningful information");
        }
    }

    [Test]
    public async Task Context_ShouldHandleAmbiguousReferences()
    {
        // Arrange & Act
        await SendMessageAsync("What is a C chord?");
        await WaitForResponseAsync();

        // Ambiguous reference
        await SendMessageAsync("Tell me more about it");
        var response = await WaitForResponseAsync();

        // Assert - Should handle "it" referring to C chord
        Assert.That(response, Is.Not.Empty, "Should handle ambiguous references");
        Assert.That(response.ToLower(), Does.Not.Contain("what do you mean"),
            "Should understand 'it' from context");
    }

    [Test]
    public async Task Context_ShouldUpdateAfterEachMessage()
    {
        // Arrange
        await SendMessageAsync("Tell me about Cmaj7");
        await WaitForResponseAsync();
        var context1 = await GetContextSummaryAsync();

        // Act
        await SendMessageAsync("Now tell me about Dm7");
        await WaitForResponseAsync();
        var context2 = await GetContextSummaryAsync();

        // Assert - Context should change
        if (context1 != null && context2 != null)
        {
            Assert.That(context2, Is.Not.EqualTo(context1),
                "Context should update with new information");
        }
    }

    [Test]
    public async Task Context_ShouldMaintainConversationHistory()
    {
        // Arrange & Act - Have a conversation
        await SendMessageAsync("What are jazz chords?");
        await WaitForResponseAsync();

        await SendMessageAsync("Give me examples");
        await WaitForResponseAsync();

        await SendMessageAsync("How do I play them on guitar?");
        await WaitForResponseAsync();

        // Assert - All messages should be visible
        var userMessages = await GetUserMessagesAsync();
        Assert.That(userMessages.Count, Is.EqualTo(3), "Should maintain all user messages");

        var assistantMessages = await GetAssistantMessagesAsync();
        Assert.That(assistantMessages.Count, Is.GreaterThanOrEqualTo(3),
            "Should maintain all assistant messages");
    }
}
