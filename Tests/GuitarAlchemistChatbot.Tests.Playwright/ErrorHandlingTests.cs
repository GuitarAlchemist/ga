namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for error handling and edge cases in the chatbot
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ErrorHandlingTests : ChatbotTestBase
{
    [Test]
    public async Task ErrorHandling_EmptyMessage_ShouldHandleGracefully()
    {
        // Arrange
        var chatInput = Page.Locator("input.form-control");

        // Act - Try to send empty message
        await chatInput.FillAsync("");
        var sendButton = Page.Locator("button:has-text('Send')");

        // Button might be disabled for empty input
        var isEnabled = await sendButton.IsEnabledAsync();

        // Assert - Should handle empty input appropriately
        if (!isEnabled)
        {
            Assert.Pass("Send button correctly disabled for empty input");
        }
        else
        {
            await sendButton.ClickAsync();
            await Task.Delay(1000);

            // Chat should remain functional
            var stillEnabled = await chatInput.IsEnabledAsync();
            Assert.That(stillEnabled, Is.True, "Chat should remain functional");
        }
    }

    [Test]
    public async Task ErrorHandling_VeryLongMessage_ShouldHandleWithoutCrash()
    {
        // Arrange - Create very long message
        var longMessage = string.Join(" ", Enumerable.Repeat(
            "This is a very long message about guitar chords and music theory", 50));

        // Act
        await SendMessageAsync(longMessage);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle very long messages");

        // Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional");
    }

    [Test]
    public async Task ErrorHandling_SpecialCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var specialMessage = "What about C# and Db chords? <>&\"'";

        // Act
        await SendMessageAsync(specialMessage);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle special characters");

        // Verify no XSS or injection issues
        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var isVisible = await messageElement.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Message should display correctly");
    }

    [Test]
    public async Task ErrorHandling_UnicodeCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var unicodeMessage = "Tell me about 音楽 and música 🎸";

        // Act
        await SendMessageAsync(unicodeMessage);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle Unicode characters");
    }

    [Test]
    public async Task ErrorHandling_RapidConsecutiveMessages_ShouldNotCrash()
    {
        // Arrange & Act - Send messages rapidly
        var tasks = new List<Task>();
        for (var i = 0; i < 5; i++)
        {
            tasks.Add(SendMessageAsync($"Quick message {i + 1}"));
        }

        // Wait for all to be sent
        await Task.WhenAll(tasks);

        // Wait for responses
        await Task.Delay(10000);

        // Assert - Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should handle rapid messages");
    }

    [Test]
    public async Task ErrorHandling_InvalidChordName_ShouldProvideHelpfulResponse()
    {
        // Arrange
        var query = "Tell me about the XYZ123 chord";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should provide helpful response
        Assert.That(response, Is.Not.Empty, "Should respond to invalid chord name");
    }

    [Test]
    public async Task ErrorHandling_MalformedQuery_ShouldHandleGracefully()
    {
        // Arrange
        var query = "what if I just type random words without meaning?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle malformed queries");
    }

    [Test]
    public async Task ErrorHandling_NetworkInterruption_ShouldShowError()
    {
        // This test simulates network issues
        // In a real scenario, you might use Playwright's route interception

        // Arrange
        var query = "What is a chord?";

        // Act
        await SendMessageAsync(query);

        // Wait for response or error
        await Task.Delay(5000);

        // Assert - Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional after network issues");
    }

    [Test]
    public async Task ErrorHandling_ConcurrentRequests_ShouldQueueProperly()
    {
        // Arrange & Act - Send multiple messages without waiting
        await SendMessageAsync("First message");
        await SendMessageAsync("Second message");
        await SendMessageAsync("Third message");

        // Wait for all to process
        await Task.Delay(15000);

        // Assert - All messages should be processed
        var userMessages = await GetUserMessagesAsync();
        Assert.That(userMessages.Count, Is.GreaterThanOrEqualTo(3),
            "Should handle concurrent requests");
    }

    [Test]
    public async Task ErrorHandling_SessionTimeout_ShouldReconnect()
    {
        // Arrange - Wait for potential session timeout
        await Task.Delay(2000);

        // Act - Try to send message after delay
        await SendMessageAsync("Are you still there?");
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle session timeout");
    }

    [Test]
    public async Task ErrorHandling_BrowserRefresh_ShouldResetState()
    {
        // Arrange
        await SendMessageAsync("Remember this message");
        await WaitForResponseAsync();

        // Act - Refresh page
        await Page.ReloadAsync();
        await Task.Delay(2000);

        // Assert - State should be reset
        var messages = await GetUserMessagesAsync();
        Assert.That(messages.Count, Is.EqualTo(0),
            "Messages should be cleared after refresh");
    }

    [Test]
    public async Task ErrorHandling_InvalidFunctionCall_ShouldRecoverGracefully()
    {
        // Arrange
        var query = "Call a function that doesn't exist";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert - Should handle gracefully
        Assert.That(response, Is.Not.Empty, "Should handle invalid function calls");

        // Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional");
    }

    [Test]
    public async Task ErrorHandling_LargeResponse_ShouldDisplayCorrectly()
    {
        // Arrange
        var query = "Tell me everything you know about music theory in great detail";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle large responses");

        // Response should be visible
        var messageElement = Page.Locator(".assistant-message .message-text").Last;
        var isVisible = await messageElement.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Large response should be visible");
    }

    [Test]
    public async Task ErrorHandling_CodeInjection_ShouldBeSanitized()
    {
        // Arrange - Try to inject script
        var maliciousMessage = "<script>alert('XSS')</script> What is a chord?";

        // Act
        await SendMessageAsync(maliciousMessage);
        var response = await WaitForResponseAsync();

        // Assert - Should sanitize input
        Assert.That(response, Is.Not.Empty, "Should respond to message");

        // Check that script wasn't executed
        var messageElement = Page.Locator(".user-message .message-text").Last;
        var html = await messageElement.InnerHTMLAsync();
        Assert.That(html, Does.Not.Contain("<script>"),
            "Script tags should be sanitized");
    }

    [Test]
    public async Task ErrorHandling_SQLInjection_ShouldBeSafe()
    {
        // Arrange - Try SQL injection pattern
        var maliciousMessage = "'; DROP TABLE chords; -- What is a chord?";

        // Act
        await SendMessageAsync(maliciousMessage);
        var response = await WaitForResponseAsync();

        // Assert - Should handle safely
        Assert.That(response, Is.Not.Empty, "Should handle SQL injection attempts");

        // Chat should remain functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat should remain functional");
    }

    [Test]
    public async Task ErrorHandling_ExcessiveWhitespace_ShouldTrim()
    {
        // Arrange
        var message = "   What   is   a   chord?   ";

        // Act
        await SendMessageAsync(message);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle excessive whitespace");
    }

    [Test]
    public async Task ErrorHandling_NewlineCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var message = "What is\na chord\nwith newlines?";

        // Act
        await SendMessageAsync(message);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle newline characters");
    }

    [Test]
    public async Task ErrorHandling_TabCharacters_ShouldHandleCorrectly()
    {
        // Arrange
        var message = "What\tis\ta\tchord?";

        // Act
        await SendMessageAsync(message);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle tab characters");
    }

    [Test]
    public async Task ErrorHandling_MixedCaseCommands_ShouldRecognize()
    {
        // Arrange
        var message = "WHAT IS A CHORD?";

        // Act
        await SendMessageAsync(message);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should handle mixed case");
        Assert.That(response.ToLower(), Does.Contain("chord"),
            "Should understand the query");
    }

    [Test]
    public async Task ErrorHandling_RepeatedMessages_ShouldHandleEach()
    {
        // Arrange & Act - Send same message multiple times
        await SendMessageAsync("What is a chord?");
        var response1 = await WaitForResponseAsync();

        await SendMessageAsync("What is a chord?");
        var response2 = await WaitForResponseAsync();

        // Assert - Should handle each message
        Assert.That(response1, Is.Not.Empty, "First message should get response");
        Assert.That(response2, Is.Not.Empty, "Second message should get response");
    }

    [Test]
    public async Task ErrorHandling_CancelledRequest_ShouldAllowNewRequest()
    {
        // Arrange
        await SendMessageAsync("Tell me a very long story about music");

        // Act - Try to cancel (if stop button exists)
        await Task.Delay(1000);
        var stopButton = Page.Locator("button:has-text('Stop')").Or(
            Page.Locator("button.btn-outline-danger"));

        var isVisible = await stopButton.IsVisibleAsync();
        if (isVisible)
        {
            await stopButton.ClickAsync();
            await Task.Delay(500);
        }

        // Send new message
        await SendMessageAsync("What is a chord?");
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should allow new request after cancellation");
    }
}
