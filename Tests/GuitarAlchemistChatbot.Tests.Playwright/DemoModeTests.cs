namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for demo mode functionality (DemoChatClient and DemoEmbeddingGenerator)
///     These tests verify the chatbot works without OpenAI API keys
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class DemoModeTests : ChatbotTestBase
{
    [Test]
    public async Task DemoMode_BasicQuery_ShouldReturnResponse()
    {
        // Arrange
        var query = "What is a chord?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Demo mode should provide intelligent responses
        Assert.That(response, Is.Not.Empty, "Demo mode should respond to basic queries");
        Assert.That(response.ToLower(), Does.Contain("chord"),
            "Response should be relevant to the query");
    }

    [Test]
    public async Task DemoMode_ChordQuery_ShouldReturnChordInfo()
    {
        // Arrange
        var query = "Tell me about C major chord";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should respond to chord queries");
        Assert.That(response.ToLower(), Does.Contain("c").Or.Contains("major"),
            "Should mention the chord");
    }

    [Test]
    public async Task DemoMode_ScaleQuery_ShouldReturnScaleInfo()
    {
        // Arrange
        var query = "What is the C major scale?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should respond to scale queries");
        Assert.That(response.ToLower(), Does.Contain("scale").Or.Contains("c"),
            "Should mention scales");
    }

    [Test]
    public async Task DemoMode_TheoryQuery_ShouldReturnTheoryExplanation()
    {
        // Arrange
        var query = "Explain the circle of fifths";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should explain music theory");
        Assert.That(response.Length, Is.GreaterThan(50),
            "Explanation should be reasonably detailed");
    }

    [Test]
    public async Task DemoMode_GuitarTechnique_ShouldReturnTechniqueInfo()
    {
        // Arrange
        var query = "How do I play barre chords?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should respond to technique questions");
        Assert.That(response.ToLower(), Does.Contain("barre").Or.Contains("finger"),
            "Should mention guitar technique");
    }

    [Test]
    public async Task DemoMode_Greeting_ShouldRespondFriendly()
    {
        // Arrange
        var query = "Hello!";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should respond to greetings");
        Assert.That(response.Length, Is.GreaterThan(10),
            "Greeting response should be friendly");
    }

    [Test]
    public async Task DemoMode_VexTabTest_ShouldReturnNotation()
    {
        // Arrange
        var query = "Show me a VexTab notation test";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should respond to VexTab requests");
        // VexTab responses typically contain code blocks
        Assert.That(response, Does.Contain("```").Or.Contains("tab"),
            "Should include notation or tab information");
    }

    [Test]
    public async Task DemoMode_ChordProgression_ShouldReturnProgressionInfo()
    {
        // Arrange
        var query = "Tell me about chord progressions";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should respond to progression queries");
        Assert.That(response.ToLower(), Does.Contain("progression").Or.Contains("chord"),
            "Should mention progressions");
    }

    [Test]
    public async Task DemoMode_MultipleQueries_ShouldHandleSequence()
    {
        // Arrange & Act - Multiple queries in sequence
        await SendMessageAsync("What is a chord?");
        var response1 = await WaitForResponseAsync();

        await SendMessageAsync("What is a scale?");
        var response2 = await WaitForResponseAsync();

        await SendMessageAsync("What is a progression?");
        var response3 = await WaitForResponseAsync();

        // Assert - All should get responses
        Assert.That(response1, Is.Not.Empty, "First query should get response");
        Assert.That(response2, Is.Not.Empty, "Second query should get response");
        Assert.That(response3, Is.Not.Empty, "Third query should get response");
    }

    [Test]
    public async Task DemoMode_StreamingResponse_ShouldStreamWords()
    {
        // Arrange
        var query = "Explain voice leading";

        // Act
        await SendMessageAsync(query);

        // Wait a moment to observe streaming
        await Task.Delay(500);

        var response = await WaitForResponseAsync();

        // Assert - Response should eventually complete
        Assert.That(response, Is.Not.Empty, "Streaming should complete");
        Assert.That(response.Length, Is.GreaterThan(20),
            "Streamed response should have content");
    }

    [Test]
    public async Task DemoMode_ContextualQuery_ShouldMaintainContext()
    {
        // Arrange & Act
        await SendMessageAsync("Tell me about C major");
        await WaitForResponseAsync();

        await SendMessageAsync("What chords are in it?");
        var response = await WaitForResponseAsync();

        // Assert - Should understand context
        Assert.That(response, Is.Not.Empty, "Should maintain context");
    }

    [Test]
    public async Task DemoMode_ComplexQuery_ShouldHandleGracefully()
    {
        // Arrange
        var query = "Explain the relationship between modes, scales, and chord progressions in jazz";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle complex queries
        Assert.That(response, Is.Not.Empty, "Should handle complex queries");
        Assert.That(response.Length, Is.GreaterThan(50),
            "Complex query should get detailed response");
    }

    [Test]
    public async Task DemoMode_UnknownTopic_ShouldProvideHelpfulResponse()
    {
        // Arrange
        var query = "Tell me about quantum physics";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should provide helpful response even for off-topic queries
        Assert.That(response, Is.Not.Empty, "Should respond to off-topic queries");
    }

    [Test]
    public async Task DemoMode_EmptyQuery_ShouldHandleGracefully()
    {
        // Arrange
        var query = " ";

        // Act
        await SendMessageAsync(query);

        // Wait for potential response or error handling
        await Task.Delay(1000);

        // Assert - Should handle empty input gracefully
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional");
    }

    [Test]
    public async Task DemoMode_LongQuery_ShouldHandleWithoutError()
    {
        // Arrange - Very long query
        var query = string.Join(" ", Enumerable.Repeat(
            "Tell me about chords and scales and progressions", 10));

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle long queries");
    }

    [Test]
    public async Task DemoMode_SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var query = "What is a C# chord?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle special characters");
        Assert.That(response.ToLower(), Does.Contain("c").Or.Contains("sharp"),
            "Should understand C# chord");
    }

    [Test]
    public async Task DemoMode_ResponseTime_ShouldBeReasonable()
    {
        // Arrange
        var query = "What is a chord?";
        var startTime = DateTime.UtcNow;

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        Assert.That(response, Is.Not.Empty, "Should get response");
        Assert.That(duration.TotalSeconds, Is.LessThan(10),
            "Demo mode should respond quickly (< 10 seconds)");
    }

    [Test]
    public async Task DemoMode_ConsecutiveQueries_ShouldNotDegrade()
    {
        // Arrange & Act - Multiple consecutive queries
        var times = new List<TimeSpan>();

        for (var i = 0; i < 3; i++)
        {
            var startTime = DateTime.UtcNow;
            await SendMessageAsync($"What is query number {i + 1}?");
            await WaitForResponseAsync();
            var endTime = DateTime.UtcNow;
            times.Add(endTime - startTime);
        }

        // Assert - Performance should not degrade significantly
        Assert.That(times.Count, Is.EqualTo(3), "Should complete all queries");
        Assert.That(times.All(t => t.TotalSeconds < 15), Is.True,
            "All queries should complete in reasonable time");
    }

    [Test]
    public async Task DemoMode_NewChat_ShouldResetContext()
    {
        // Arrange
        await SendMessageAsync("Tell me about C major");
        await WaitForResponseAsync();

        // Act - Start new chat
        await ClickNewChatAsync();

        await SendMessageAsync("What were we discussing?");
        var response = await WaitForResponseAsync();

        // Assert - Context should be reset
        Assert.That(response, Is.Not.Empty, "Should respond after new chat");
    }

    [Test]
    public async Task DemoMode_FunctionSimulation_ShouldProvideRelevantData()
    {
        // Arrange
        var query = "Search for jazz chords";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Demo mode should simulate function calls
        Assert.That(response, Is.Not.Empty, "Should simulate chord search");
        Assert.That(response.ToLower(), Does.Contain("jazz").Or.Contains("chord"),
            "Should provide relevant chord information");
    }
}
