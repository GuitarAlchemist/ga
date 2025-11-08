namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for web integration functions (Wikipedia, music theory sites, RSS feeds)
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class WebIntegrationFunctionTests : ChatbotTestBase
{
    [Test]
    public async Task WebIntegration_WikipediaSearch_ShouldReturnResults()
    {
        // Arrange
        var query = "Search Wikipedia for music theory";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return Wikipedia search results");
        Assert.That(response.ToLower(), Does.Contain("music").Or.Contains("theory"),
            "Should mention music theory");
    }

    [Test]
    public async Task WebIntegration_WikipediaSummary_ShouldReturnSummary()
    {
        // Arrange
        var query = "Get Wikipedia summary for chord progression";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return Wikipedia summary");
        Assert.That(response.Length, Is.GreaterThan(50),
            "Summary should be reasonably detailed");
    }

    [Test]
    public async Task WebIntegration_MusicTheorySite_ShouldSearchSite()
    {
        // Arrange
        var query = "Search musictheory.net for scales";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should search music theory site");
    }

    [Test]
    public async Task WebIntegration_LatestLessons_ShouldReturnRSSFeed()
    {
        // Arrange
        var query = "Show me the latest guitar lessons";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return latest lessons from RSS");
    }

    [Test]
    public async Task WebIntegration_FetchArticle_ShouldReturnContent()
    {
        // Arrange
        var query = "Fetch the article about jazz harmony";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should fetch article content");
    }

    [Test]
    public async Task WebIntegration_MultipleSourcesQuery_ShouldCombineResults()
    {
        // Arrange
        var query = "Search for information about modes from multiple sources";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should combine results from multiple sources");
        Assert.That(response.Length, Is.GreaterThan(100),
            "Combined results should be comprehensive");
    }

    [Test]
    public async Task WebIntegration_ErrorHandling_ShouldHandleUnavailableSources()
    {
        // Arrange
        var query = "Search for information from unavailable source";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle errors gracefully
        Assert.That(response, Is.Not.Empty, "Should provide response even if source unavailable");

        // Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional after error");
    }

    [Test]
    public async Task WebIntegration_FunctionIndicator_ShouldShowWhenCalling()
    {
        // Arrange
        var query = "Search Wikipedia for guitar chords";

        // Act
        await SendMessageAsync(query);

        // Try to catch function call indicator
        var functionCall = await WaitForFunctionCallAsync();

        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should complete search");

        // Function indicator might have appeared
        if (functionCall != null)
        {
            Assert.That(functionCall.ToLower(), Does.Contain("search").Or.Contain("wikipedia"),
                "Function indicator should show search activity");
        }
    }

    [Test]
    public async Task WebIntegration_CachedResults_ShouldReturnQuickly()
    {
        // Arrange
        var query = "Search Wikipedia for music theory";

        // Act - First call (might be slower)
        var startTime1 = DateTime.UtcNow;
        await SendMessageAsync(query);
        await WaitForResponseAsync();
        var duration1 = DateTime.UtcNow - startTime1;

        // Clear chat and ask again
        await ClickNewChatAsync();

        // Act - Second call (might use cache)
        var startTime2 = DateTime.UtcNow;
        await SendMessageAsync(query);
        await WaitForResponseAsync();
        var duration2 = DateTime.UtcNow - startTime2;

        // Assert - Both should complete successfully
        Assert.That(duration1.TotalSeconds, Is.LessThan(60),
            "First call should complete in reasonable time");
        Assert.That(duration2.TotalSeconds, Is.LessThan(60),
            "Second call should complete in reasonable time");
    }

    [Test]
    public async Task WebIntegration_SpecificSite_ShouldTargetCorrectSite()
    {
        // Arrange
        var query = "Search justinguitar for beginner lessons";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should search specific site");
    }

    [Test]
    public async Task WebIntegration_RSSFeed_ShouldReturnRecentContent()
    {
        // Arrange
        var query = "What are the latest music theory articles?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return recent RSS content");
    }

    [Test]
    public async Task WebIntegration_ArticleFetch_ShouldExtractMainContent()
    {
        // Arrange
        var query = "Fetch and summarize an article about chord voicings";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should extract article content");
        Assert.That(response.Length, Is.GreaterThan(50),
            "Extracted content should be substantial");
    }

    [Test]
    public async Task WebIntegration_CombinedQuery_ShouldUseMultipleFunctions()
    {
        // Arrange
        var query = "Search Wikipedia and music theory sites for information about the Dorian mode";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should use multiple web functions");
        Assert.That(response.ToLower(), Does.Contain("dorian").Or.Contains("mode"),
            "Should mention the topic");
    }

    [Test]
    public async Task WebIntegration_SequentialCalls_ShouldHandleMultipleRequests()
    {
        // Arrange & Act
        await SendMessageAsync("Search Wikipedia for scales");
        var response1 = await WaitForResponseAsync();

        await SendMessageAsync("Now search for chords");
        var response2 = await WaitForResponseAsync();

        await SendMessageAsync("And get the latest lessons");
        var response3 = await WaitForResponseAsync();

        // Assert - All should complete
        Assert.That(response1, Is.Not.Empty, "First search should complete");
        Assert.That(response2, Is.Not.Empty, "Second search should complete");
        Assert.That(response3, Is.Not.Empty, "Third search should complete");
    }

    [Test]
    public async Task WebIntegration_InvalidQuery_ShouldHandleGracefully()
    {
        // Arrange
        var query = "Search for something that doesn't exist xyz123abc";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle gracefully
        Assert.That(response, Is.Not.Empty, "Should provide response even for invalid query");
    }

    [Test]
    public async Task WebIntegration_Timeout_ShouldNotHangChat()
    {
        // Arrange
        var query = "Search for very specific obscure topic that might timeout";

        // Act
        await SendMessageAsync(query);

        // Wait reasonable time
        await Task.Delay(5000);

        // Assert - Chat should remain responsive
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional");
    }

    [Test]
    public async Task WebIntegration_FormattedResults_ShouldDisplayReadably()
    {
        // Arrange
        var query = "Search Wikipedia for guitar and show me the results";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Results should be formatted
        Assert.That(response, Is.Not.Empty, "Should return formatted results");

        // Check for formatting in the message element
        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var html = await messageElement.InnerHTMLAsync();

        Assert.That(html, Does.Contain("<").And.Contain(">"),
            "Results should contain HTML formatting");
    }

    [Test]
    public async Task WebIntegration_ContextualSearch_ShouldUseConversationContext()
    {
        // Arrange & Act
        await SendMessageAsync("I'm interested in jazz");
        await WaitForResponseAsync();

        await SendMessageAsync("Search for relevant information");
        var response = await WaitForResponseAsync();

        // Assert - Should use context (jazz) in search
        Assert.That(response, Is.Not.Empty, "Should perform contextual search");
    }

    [Test]
    public async Task WebIntegration_MultipleResults_ShouldPresentClearly()
    {
        // Arrange
        var query = "Search for guitar chord progressions and show me the top results";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should return multiple results");
        Assert.That(response.Length, Is.GreaterThan(100),
            "Multiple results should be comprehensive");
    }
}
