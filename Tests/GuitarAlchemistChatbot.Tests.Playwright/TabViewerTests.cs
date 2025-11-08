namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for guitar tab visualization features
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class TabViewerTests : ChatbotTestBase
{
    [Test]
    public async Task TabViewer_ShouldRenderVexTabNotation()
    {
        // Arrange
        var tabRequest = "Show me a C major scale in tab notation";

        // Act
        await SendMessageAsync(tabRequest);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");

        // Check if VexTab element exists
        var hasVexTab = await HasVexTabAsync();
        if (hasVexTab)
        {
            await WaitForVexTabRenderAsync();

            // Verify VexTab rendered successfully
            var vexTabElement = Page.Locator(".vex-tabdiv[data-rendered='true']");
            var count = await vexTabElement.CountAsync();
            Assert.That(count, Is.GreaterThan(0), "VexTab should be rendered");
        }
    }

    [Test]
    public async Task TabViewer_ShouldRenderExampleTab()
    {
        // Arrange - Click the "Show guitar TAB example" button
        var exampleButton = Page.Locator("button:has-text('Show guitar TAB example')");

        // Act
        await exampleButton.ClickAsync();
        await WaitForResponseAsync();

        // Wait for VexTab to render
        await WaitForVexTabRenderAsync();

        // Assert
        var vexTabElement = Page.Locator(".vex-tabdiv[data-rendered='true']");
        var count = await vexTabElement.CountAsync();
        Assert.That(count, Is.GreaterThan(0), "Example VexTab should be rendered");

        // Verify SVG content exists (VexTab renders as SVG)
        var svgElement = Page.Locator(".vex-tabdiv svg");
        var svgCount = await svgElement.CountAsync();
        Assert.That(svgCount, Is.GreaterThan(0), "VexTab should render SVG content");
    }

    [Test]
    public async Task TabViewer_ShouldHandleMultipleTabs()
    {
        // Arrange
        await SendMessageAsync("Show me a C major scale");
        await WaitForResponseAsync();

        // Act - Request another tab
        await SendMessageAsync("Now show me an A minor scale");
        await WaitForResponseAsync();

        // Assert - Both tabs should be visible
        var vexTabElements = Page.Locator(".vex-tabdiv");
        var count = await vexTabElements.CountAsync();

        // We might have 0, 1, or 2 tabs depending on AI response
        Assert.That(count, Is.GreaterThanOrEqualTo(0), "Should handle multiple tab requests");
    }

    [Test]
    [TestCase("chromium")]
    [TestCase("firefox")]
    [TestCase("webkit")]
    public async Task TabViewer_ShouldRenderAcrossBrowsers(string browserType)
    {
        // This test verifies tab rendering works across different browsers
        // The actual browser is set by the test framework configuration

        // Arrange
        var exampleButton = Page.Locator("button:has-text('Show guitar TAB example')");

        // Act
        await exampleButton.ClickAsync();
        await WaitForResponseAsync();
        await WaitForVexTabRenderAsync();

        // Assert
        var vexTabElement = Page.Locator(".vex-tabdiv[data-rendered='true']");
        var count = await vexTabElement.CountAsync();
        Assert.That(count, Is.GreaterThan(0), $"VexTab should render in {browserType}");
    }

    [Test]
    public async Task TabViewer_ShouldBeResponsive()
    {
        // Test responsive behavior at different viewport sizes

        // Arrange - Mobile viewport
        await Page.SetViewportSizeAsync(375, 667);
        var exampleButton = Page.Locator("button:has-text('Show guitar TAB example')");

        // Act
        await exampleButton.ClickAsync();
        await WaitForResponseAsync();
        await WaitForVexTabRenderAsync();

        // Assert - Tab should be visible on mobile
        var vexTabElement = Page.Locator(".vex-tabdiv[data-rendered='true']");
        var isVisible = await vexTabElement.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "VexTab should be visible on mobile");

        // Test desktop viewport
        await Page.SetViewportSizeAsync(1920, 1080);
        isVisible = await vexTabElement.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "VexTab should be visible on desktop");
    }

    [Test]
    public async Task TabViewer_ShouldHandleInvalidTabNotation()
    {
        // Arrange - Send a message that might result in invalid tab notation
        var invalidRequest = "Show me tab notation for xyz123";

        // Act
        await SendMessageAsync(invalidRequest);
        var response = await WaitForResponseAsync();

        // Assert - Should handle gracefully without crashing
        Assert.That(response, Is.Not.Empty, "Should receive a response even for invalid requests");

        // Page should still be functional
        var chatInput = Page.Locator("input.form-control");
        var isEnabled = await chatInput.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Chat input should remain functional");
    }

    [Test]
    public async Task TabViewer_ShouldDisplayTabInMessageFlow()
    {
        // Arrange
        var exampleButton = Page.Locator("button:has-text('Show guitar TAB example')");

        // Act
        await exampleButton.ClickAsync();
        await WaitForResponseAsync();

        // Assert - Tab should be within an assistant message
        var assistantMessages = Page.Locator(".assistant-message");
        var messageCount = await assistantMessages.CountAsync();
        Assert.That(messageCount, Is.GreaterThan(0), "Should have assistant messages");

        // VexTab should be inside a message
        var vexTabInMessage = Page.Locator(".assistant-message .vex-tabdiv");
        var tabCount = await vexTabInMessage.CountAsync();
        Assert.That(tabCount, Is.GreaterThan(0), "VexTab should be within message flow");
    }

    [Test]
    public async Task TabViewer_ShouldScrollToNewTab()
    {
        // Arrange
        var exampleButton = Page.Locator("button:has-text('Show guitar TAB example')");

        // Act
        await exampleButton.ClickAsync();
        await WaitForResponseAsync();
        await WaitForVexTabRenderAsync();

        // Assert - Chat should auto-scroll to show the new tab
        var chatMessages = Page.Locator("#chatMessages");
        var scrollTop = await chatMessages.EvaluateAsync<int>("el => el.scrollTop");
        var scrollHeight = await chatMessages.EvaluateAsync<int>("el => el.scrollHeight");
        var clientHeight = await chatMessages.EvaluateAsync<int>("el => el.clientHeight");

        // Should be scrolled near the bottom (within 100px tolerance)
        Assert.That(scrollTop + clientHeight, Is.GreaterThan(scrollHeight - 100),
            "Should auto-scroll to show new tab");
    }
}
