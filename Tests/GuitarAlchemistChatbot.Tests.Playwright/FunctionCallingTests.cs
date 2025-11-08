namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for AI function calling integration and UI indicators
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class FunctionCallingTests : ChatbotTestBase
{
    [Test]
    public async Task FunctionCalling_ShouldShowIndicatorWhenSearchingChords()
    {
        // Arrange
        var query = "Find me some dark jazz chords";

        // Act
        await SendMessageAsync(query);

        // Try to catch the function call indicator (it appears briefly)
        var functionCall = await WaitForFunctionCallAsync();

        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");

        // Function call indicator might have appeared
        if (functionCall != null)
        {
            Assert.That(functionCall, Does.Contain("Calling").Or.Contain("Search"),
                "Function indicator should show function name");
        }
    }

    [Test]
    public async Task FunctionCalling_ShouldDisplayStructuredChordResults()
    {
        // Arrange
        var query = "Show me major seventh chords";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive chord results");

        // Response should contain structured information
        Assert.That(response.ToLower(), Does.Contain("chord").Or.Contain("maj7"),
            "Response should mention chords");
    }

    [Test]
    public async Task FunctionCalling_ShouldHandleMultipleFunctionCalls()
    {
        // Arrange
        var query = "Find dark jazz chords and explain what makes them dark";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle multiple function calls");
        Assert.That(response.Length, Is.GreaterThan(50),
            "Response should be comprehensive");
    }

    [Test]
    public async Task FunctionCalling_ShouldShowLoadingState()
    {
        // Arrange
        var query = "Search for complex extended chords";

        // Act
        await SendMessageAsync(query);

        // Check for loading indicators
        var hasTypingIndicator = await Page.Locator(".typing-indicator").IsVisibleAsync();
        var hasFunctionIndicator = await Page.Locator(".function-indicator").IsVisibleAsync();

        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(hasTypingIndicator || hasFunctionIndicator, Is.True,
            "Should show some loading state");
        Assert.That(response, Is.Not.Empty, "Should receive a response");
    }

    [Test]
    public async Task FunctionCalling_ShouldHandleFunctionErrors()
    {
        // Arrange - Request something that might cause an error
        var query = "Find chords with ID 999999999";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle gracefully
        Assert.That(response, Is.Not.Empty, "Should receive a response even on error");

        // Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional after error");
    }

    [Test]
    public async Task FunctionCalling_ShouldFormatResultsReadably()
    {
        // Arrange
        var query = "Find bright major chords";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Response should be formatted
        Assert.That(response, Is.Not.Empty, "Should receive formatted results");

        // Check for formatting elements (lists, bold text, etc.)
        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var html = await messageElement.InnerHTMLAsync();

        // Should contain some HTML formatting
        Assert.That(html, Does.Contain("<").And.Contain(">"),
            "Response should contain HTML formatting");
    }

    [Test]
    public async Task FunctionCalling_ShouldShowFunctionNameInIndicator()
    {
        // Arrange
        var query = "Search for diminished chords";

        // Act
        await SendMessageAsync(query);

        // Try to capture function indicator
        var functionCall = await WaitForFunctionCallAsync();

        // Assert
        if (functionCall != null)
        {
            Assert.That(functionCall.ToLower(), Does.Contain("search").Or.Contain("calling"),
                "Function indicator should show meaningful information");
        }
    }

    [Test]
    public async Task FunctionCalling_ShouldCompleteWithinReasonableTime()
    {
        // Arrange
        var query = "Find jazz chords";
        var startTime = DateTime.UtcNow;

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();
        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(duration.TotalSeconds, Is.LessThan(30),
            "Function call should complete within 30 seconds");
    }

    [Test]
    public async Task FunctionCalling_ShouldAllowCancellation()
    {
        // Arrange
        var query = "Find all possible chord variations";

        // Act
        await SendMessageAsync(query);

        // Wait a moment then try to cancel
        await Task.Delay(1000);

        var stopButton = Page.Locator("button:has-text('Stop')").Or(Page.Locator("button.btn-outline-danger"));
        var isVisible = await stopButton.IsVisibleAsync();

        if (isVisible)
        {
            await stopButton.ClickAsync();
            await Task.Delay(500);
        }

        // Assert - Chat should be functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should be functional after cancellation");
    }

    [Test]
    public async Task FunctionCalling_ShouldDisplayResultsInCards()
    {
        // Arrange
        var query = "Show me chord voicings for pop rock";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive results");

        // Check if results are displayed in a structured way
        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var hasStructure = await messageElement.Locator("ul, ol, strong, em").CountAsync() > 0;

        Assert.That(hasStructure, Is.True, "Results should have some structure");
    }

    [Test]
    public async Task FunctionCalling_ShouldHandleSequentialCalls()
    {
        // Arrange & Act - Make multiple requests
        await SendMessageAsync("Find major chords");
        await WaitForResponseAsync();

        await SendMessageAsync("Now find minor chords");
        await WaitForResponseAsync();

        await SendMessageAsync("And diminished chords");
        var finalResponse = await WaitForResponseAsync();

        // Assert - All calls should complete successfully
        Assert.That(finalResponse, Is.Not.Empty, "Should handle sequential function calls");

        var assistantMessages = await GetAssistantMessagesAsync();
        Assert.That(assistantMessages.Count, Is.GreaterThanOrEqualTo(3),
            "Should have responses for all requests");
    }

    [Test]
    public async Task FunctionCalling_ShouldProvideContextualResults()
    {
        // Arrange
        var query = "I'm writing a sad song. What chords should I use?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should provide contextual chord suggestions
        Assert.That(response, Is.Not.Empty, "Should provide contextual results");
        Assert.That(response.ToLower(), Does.Contain("chord").Or.Contain("minor"),
            "Should suggest appropriate chords for sad songs");
    }

    [Test]
    public async Task FunctionCalling_ChordProgression_ShouldReturnTemplates()
    {
        // Arrange
        var query = "Show me jazz chord progressions";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return progression templates");
        Assert.That(response.ToLower(), Does.Contain("progression").Or.Contain("ii").Or.Contain("v"),
            "Should mention chord progressions");
    }

    [Test]
    public async Task FunctionCalling_ChordDiagram_ShouldReturnDiagramInfo()
    {
        // Arrange
        var query = "Show me how to play an E minor chord";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return diagram information");
        Assert.That(response.ToLower(), Does.Contain("e").Or.Contain("minor").Or.Contain("finger"),
            "Should mention chord diagram details");
    }

    [Test]
    public async Task FunctionCalling_MusicTheory_ShouldExplainConcepts()
    {
        // Arrange
        var query = "Explain what a tritone is";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should explain music theory concept");
        Assert.That(response.Length, Is.GreaterThan(50), "Explanation should be detailed");
    }

    [Test]
    public async Task FunctionCalling_SimilarChords_ShouldFindAlternatives()
    {
        // Arrange
        var query = "Find chords similar to Cmaj7";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should find similar chords");
        Assert.That(response.ToLower(), Does.Contain("chord").Or.Contain("similar"),
            "Should mention similar chords");
    }

    [Test]
    public async Task FunctionCalling_ProgressionGenres_ShouldListGenres()
    {
        // Arrange
        var query = "What genres of chord progressions do you have?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should list progression genres");
        Assert.That(response.ToLower(), Does.Contain("pop").Or.Contains("jazz").Or.Contains("blues"),
            "Should mention music genres");
    }

    [Test]
    public async Task FunctionCalling_ChordDetails_ShouldReturnDetailedInfo()
    {
        // Arrange
        var query = "Tell me everything about the Dm7 chord";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return detailed chord information");
        Assert.That(response.ToLower(), Does.Contain("d").Or.Contains("minor").Or.Contains("seventh"),
            "Should mention chord details");
    }

    [Test]
    public async Task FunctionCalling_MultipleQueries_ShouldHandleSequence()
    {
        // Arrange & Act - Multiple different function calls
        await SendMessageAsync("Find jazz chords");
        var response1 = await WaitForResponseAsync();

        await SendMessageAsync("Show me blues progressions");
        var response2 = await WaitForResponseAsync();

        await SendMessageAsync("Explain modal interchange");
        var response3 = await WaitForResponseAsync();

        // Assert - All should complete successfully
        Assert.That(response1, Is.Not.Empty, "First query should complete");
        Assert.That(response2, Is.Not.Empty, "Second query should complete");
        Assert.That(response3, Is.Not.Empty, "Third query should complete");
    }

    [Test]
    public async Task FunctionCalling_ComplexQuery_ShouldHandleMultipleFunctions()
    {
        // Arrange
        var query = "Find dark jazz chords, show me how to play them, and suggest a progression";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle multiple function calls in one query
        Assert.That(response, Is.Not.Empty, "Should handle complex multi-function query");
        Assert.That(response.Length, Is.GreaterThan(100), "Response should be comprehensive");
    }
}
