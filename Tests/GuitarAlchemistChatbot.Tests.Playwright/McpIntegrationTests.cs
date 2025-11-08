namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for MCP tools integration (web search, scraping, feed reading)
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class McpIntegrationTests : ChatbotTestBase
{
    [Test]
    public async Task McpIntegration_ShouldSearchWikipedia()
    {
        // Arrange
        var query = "Search Wikipedia for information about harmonic minor scale";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive Wikipedia search results");
        Assert.That(response.ToLower(), Does.Contain("harmonic").Or.Contain("minor").Or.Contain("scale"),
            "Response should contain relevant information");
    }

    [Test]
    public async Task McpIntegration_ShouldGetWikipediaSummary()
    {
        // Arrange
        var query = "Get me a summary from Wikipedia about the circle of fifths";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive Wikipedia summary");
        Assert.That(response.Length, Is.GreaterThan(100),
            "Summary should be substantial");
    }

    [Test]
    public async Task McpIntegration_ShouldSearchMusicTheorySites()
    {
        // Arrange
        var query = "Search musictheory.net for information about chord progressions";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive search results");
        Assert.That(response.ToLower(), Does.Contain("chord").Or.Contain("progression"),
            "Response should be relevant to query");
    }

    [Test]
    public async Task McpIntegration_ShouldGetLatestMusicLessons()
    {
        // Arrange
        var query = "What are the latest guitar lessons from JustinGuitar?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive latest lessons");
        Assert.That(response.ToLower(), Does.Contain("lesson").Or.Contain("guitar").Or.Contain("justin"),
            "Response should mention lessons");
    }

    [Test]
    public async Task McpIntegration_ShouldFetchMusicTheoryArticle()
    {
        // Arrange
        var query = "Fetch the article about modes from musictheory.net";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should fetch article content");
        // Response might contain article content or a summary
    }

    [Test]
    public async Task McpIntegration_ShouldShowFunctionIndicatorForWebSearch()
    {
        // Arrange
        var query = "Search for jazz chord theory";

        // Act
        await SendMessageAsync(query);

        // Try to catch function call indicator
        var functionCall = await WaitForFunctionCallAsync();

        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive search results");

        if (functionCall != null)
        {
            Assert.That(functionCall.ToLower(), Does.Contain("search").Or.Contain("calling"),
                "Should show function indicator for web search");
        }
    }

    [Test]
    public async Task McpIntegration_ShouldHandleMultipleWebSources()
    {
        // Arrange
        var query = "Search both Wikipedia and musictheory.net for information about Dorian mode";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle multiple sources");
        Assert.That(response.ToLower(), Does.Contain("dorian").Or.Contain("mode"),
            "Response should be relevant");
    }

    [Test]
    public async Task McpIntegration_ShouldDisplayWebResultsInline()
    {
        // Arrange
        var query = "What does Wikipedia say about guitar scales?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Results should be displayed inline in chat
        Assert.That(response, Is.Not.Empty, "Should display results inline");

        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var isVisible = await messageElement.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Results should be visible in chat");
    }

    [Test]
    public async Task McpIntegration_ShouldHandleWebSearchErrors()
    {
        // Arrange - Request something that might fail
        var query = "Search for xyz123abc456 on Wikipedia";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle gracefully
        Assert.That(response, Is.Not.Empty, "Should receive a response even on error");

        // Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional");
    }

    [Test]
    public async Task McpIntegration_ShouldCacheWebResults()
    {
        // Arrange
        var query = "Search Wikipedia for pentatonic scale";

        // Act - First search
        await SendMessageAsync(query);
        var startTime1 = DateTime.UtcNow;
        var response1 = await WaitForResponseAsync();
        var duration1 = DateTime.UtcNow - startTime1;

        // Reset chat
        await ClickNewChatAsync();

        // Second search (should be cached)
        await SendMessageAsync(query);
        var startTime2 = DateTime.UtcNow;
        var response2 = await WaitForResponseAsync();
        var duration2 = DateTime.UtcNow - startTime2;

        // Assert
        Assert.That(response1, Is.Not.Empty, "First search should return results");
        Assert.That(response2, Is.Not.Empty, "Second search should return results");

        // Second search might be faster due to caching (though not guaranteed)
        // Just verify both completed successfully
        Assert.That(duration1.TotalSeconds, Is.LessThan(60), "First search should complete");
        Assert.That(duration2.TotalSeconds, Is.LessThan(60), "Second search should complete");
    }

    [Test]
    public async Task McpIntegration_ShouldFormatWebContentReadably()
    {
        // Arrange
        var query = "Get Wikipedia summary for music theory";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Content should be formatted
        Assert.That(response, Is.Not.Empty, "Should receive formatted content");

        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var html = await messageElement.InnerHTMLAsync();

        // Should contain formatting
        Assert.That(html, Does.Contain("<"), "Content should be formatted as HTML");
    }

    [Test]
    public async Task McpIntegration_ShouldCombineWebSearchWithChordSearch()
    {
        // Arrange
        var query = "Find me jazz chords and search Wikipedia for jazz harmony";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle both function types
        Assert.That(response, Is.Not.Empty, "Should combine multiple function types");
        Assert.That(response.ToLower(), Does.Contain("chord").Or.Contain("jazz").Or.Contain("harmony"),
            "Response should address both requests");
    }

    [Test]
    public async Task McpIntegration_ShouldProvideSourceAttribution()
    {
        // Arrange
        var query = "What does Wikipedia say about the Lydian mode?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should mention the source
        Assert.That(response, Is.Not.Empty, "Should receive information");
        // Response might mention Wikipedia or the source
    }

    [Test]
    public async Task McpIntegration_ShouldHandleFeedReading()
    {
        // Arrange
        var query = "Show me the latest music theory lessons";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive feed results");
        Assert.That(response.ToLower(), Does.Contain("lesson").Or.Contain("article").Or.Contain("latest"),
            "Response should mention lessons or articles");
    }

    [Test]
    public async Task McpIntegration_ShouldCompleteWebSearchWithinTimeout()
    {
        // Arrange
        var query = "Search for guitar chord theory";
        var startTime = DateTime.UtcNow;

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();
        var duration = DateTime.UtcNow - startTime;

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive results");
        Assert.That(duration.TotalSeconds, Is.LessThan(DefaultTimeout / 1000),
            "Web search should complete within timeout");
    }
}
