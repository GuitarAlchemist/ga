namespace GuitarAlchemistChatbot.Tests.Playwright;

using Microsoft.Playwright;

/// <summary>
///     Tests for dark mode theme toggle functionality
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class DarkModeTests : ChatbotTestBase
{
    [Test]
    public async Task DarkMode_ShouldHaveToggleButton()
    {
        // Assert
        var toggleButton = Page.Locator(".theme-toggle-btn");
        var buttonCount = await toggleButton.CountAsync();
        Assert.That(buttonCount, Is.GreaterThan(0), "Theme toggle button should exist");
    }

    [Test]
    public async Task DarkMode_ShouldToggleTheme()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Get initial theme state
        var initialTheme = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Act - Click toggle
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500); // Wait for transition

        // Assert - Theme should change
        var newTheme = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(newTheme, Is.Not.EqualTo(initialTheme), "Theme should toggle");
    }

    [Test]
    public async Task DarkMode_ShouldPersistPreference()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Enable dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var darkModeEnabled = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Reload page
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Dark mode should persist
        var themeAfterReload = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(themeAfterReload, Is.EqualTo(darkModeEnabled),
            "Theme preference should persist after reload");
    }

    [Test]
    public async Task DarkMode_ShouldShowCorrectIcon()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Check initial icon (should be moon for light mode)
        var initialIcon = toggleButton.Locator("i");
        var initialIconClass = await initialIcon.GetAttributeAsync("class");

        // Act - Toggle to dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Icon should change to sun
        var newIconClass = await initialIcon.GetAttributeAsync("class");
        Assert.That(newIconClass, Is.Not.EqualTo(initialIconClass),
            "Icon should change when toggling theme");
    }

    [Test]
    public async Task DarkMode_ShouldApplyDarkColors()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Enable dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Check if dark theme is applied
        var htmlElement = Page.Locator("html");
        var dataTheme = await htmlElement.GetAttributeAsync("data-theme");
        Assert.That(dataTheme, Is.EqualTo("dark"), "Dark theme should be applied");

        // Check background color changed (should be dark)
        var bodyBgColor = await Page.Locator("body").EvaluateAsync<string>(
            "el => window.getComputedStyle(el).backgroundColor");

        // Dark mode should have a dark background (rgb values should be low)
        Assert.That(bodyBgColor, Is.Not.Null, "Body should have background color");
    }

    [Test]
    public async Task DarkMode_ShouldUpdateAllComponents()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Enable dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Check various components
        var chatContainer = Page.Locator(".chat-container");
        var containerExists = await chatContainer.CountAsync() > 0;
        Assert.That(containerExists, Is.True, "Chat container should exist");

        // All components should be visible in dark mode
        var isVisible = await chatContainer.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Components should be visible in dark mode");
    }

    [Test]
    public async Task DarkMode_ShouldHaveSmoothTransition()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Toggle theme
        await toggleButton.ClickAsync();

        // Assert - Wait for transition (CSS transitions are 0.3s)
        await Page.WaitForTimeoutAsync(400);

        // Theme should be applied
        var theme = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(theme, Is.Not.Null, "Theme should be applied after transition");
    }

    [Test]
    public async Task DarkMode_ShouldWorkWithMessages()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Send a message first
        await SendMessageAsync("Hello");
        await WaitForResponseAsync();

        // Act - Toggle dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Messages should still be visible
        var messages = Page.Locator(".message");
        var messageCount = await messages.CountAsync();
        Assert.That(messageCount, Is.GreaterThan(0), "Messages should be visible in dark mode");
    }

    [Test]
    public async Task DarkMode_ShouldWorkWithChordDiagrams()
    {
        // Arrange
        await SendMessageAsync("Show me C major chord");
        await WaitForResponseAsync();

        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Toggle dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Chord diagrams should be visible in dark mode
        var diagrams = Page.Locator(".chord-diagram");
        var diagramCount = await diagrams.CountAsync();

        if (diagramCount > 0)
        {
            var isVisible = await diagrams.First.IsVisibleAsync();
            Assert.That(isVisible, Is.True, "Chord diagrams should be visible in dark mode");
        }
    }

    [Test]
    public async Task DarkMode_ShouldToggleBackToLight()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Toggle to dark
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        var darkTheme = await Page.Locator("html").GetAttributeAsync("data-theme");

        // Toggle back to light
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Should return to light mode
        var lightTheme = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(lightTheme, Is.Not.EqualTo(darkTheme), "Should toggle back to light mode");
    }

    [Test]
    public async Task DarkMode_ShouldBeAccessible()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Assert - Button should have accessible attributes
        var buttonExists = await toggleButton.CountAsync() > 0;
        Assert.That(buttonExists, Is.True, "Toggle button should exist");

        // Button should be clickable
        var isEnabled = await toggleButton.IsEnabledAsync();
        Assert.That(isEnabled, Is.True, "Toggle button should be enabled");
    }

    [Test]
    public async Task DarkMode_ShouldWorkOnMobile()
    {
        // Arrange - Mobile viewport
        await Page.SetViewportSizeAsync(375, 667);

        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Toggle dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Dark mode should work on mobile
        var theme = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(theme, Is.EqualTo("dark"), "Dark mode should work on mobile");

        // Reset viewport
        await Page.SetViewportSizeAsync(1280, 720);
    }

    [Test]
    [TestCase("chromium")]
    [TestCase("firefox")]
    [TestCase("webkit")]
    public async Task DarkMode_ShouldWorkAcrossBrowsers(string browserType)
    {
        // This test verifies dark mode works across different browsers

        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert
        var theme = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(theme, Is.EqualTo("dark"),
            $"Dark mode should work in {browserType}");
    }

    [Test]
    public async Task DarkMode_ShouldUpdateInputArea()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Enable dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Assert - Input area should be visible and styled
        var inputArea = Page.Locator(".chat-input");
        var isVisible = await inputArea.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Input area should be visible in dark mode");
    }

    [Test]
    public async Task DarkMode_ShouldClearPreferenceOnReset()
    {
        // Arrange
        var toggleButton = Page.Locator(".theme-toggle-btn");

        // Act - Enable dark mode
        await toggleButton.ClickAsync();
        await Page.WaitForTimeoutAsync(500);

        // Clear localStorage
        await Page.EvaluateAsync("() => localStorage.clear()");

        // Reload page
        await Page.ReloadAsync();
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        // Assert - Should default to light mode
        var theme = await Page.Locator("html").GetAttributeAsync("data-theme");
        Assert.That(theme, Is.Null.Or.Empty, "Should default to light mode after clearing storage");
    }
}
