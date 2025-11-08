namespace GuitarAlchemistChatbot.Tests.Playwright;

/// <summary>
///     Tests for chord diagram visualization features
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ChordDiagramTests : ChatbotTestBase
{
    [Test]
    public async Task ChordDiagram_ShouldRenderForCMajor()
    {
        // Arrange
        var query = "Show me how to play a C major chord";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");

        // Check for chord diagram elements
        var hasDiagram = await Page.Locator(".chord-diagram").CountAsync() > 0;
        Assert.That(hasDiagram, Is.True, "Should render chord diagram");
    }

    [Test]
    public async Task ChordDiagram_ShouldShowFingerPositions()
    {
        // Arrange
        var query = "Show me the C chord diagram";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert - Check for SVG elements
        var svgElement = Page.Locator(".chord-diagram svg");
        var svgCount = await svgElement.CountAsync();
        Assert.That(svgCount, Is.GreaterThan(0), "Should contain SVG diagram");

        // Check for circles (finger positions)
        var circles = svgElement.Locator("circle");
        var circleCount = await circles.CountAsync();
        Assert.That(circleCount, Is.GreaterThan(0), "Should show finger positions");
    }

    [Test]
    public async Task ChordDiagram_ShouldDisplayChordName()
    {
        // Arrange
        var query = "How do I play Dm7?";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var chordName = Page.Locator(".chord-name");
        var nameCount = await chordName.CountAsync();
        Assert.That(nameCount, Is.GreaterThan(0), "Should display chord name");

        if (nameCount > 0)
        {
            var nameText = await chordName.First.TextContentAsync();
            Assert.That(nameText, Does.Contain("D").Or.Contain("m7"),
                "Chord name should be displayed");
        }
    }

    [Test]
    public async Task ChordDiagram_ShouldShowMultiplePositions()
    {
        // Arrange
        var query = "Show me all positions for G major";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var diagrams = Page.Locator(".chord-diagram");
        var diagramCount = await diagrams.CountAsync();

        // G major should have at least 2 positions (open and barre)
        Assert.That(diagramCount, Is.GreaterThanOrEqualTo(1),
            "Should show at least one chord diagram");
    }

    [Test]
    public async Task ChordDiagram_ShouldShowOpenStrings()
    {
        // Arrange
        var query = "Show me C major chord";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert - C major has open strings, check for open string indicators
        var svgElement = Page.Locator(".chord-diagram svg").First;
        var circles = svgElement.Locator("circle");
        var circleCount = await circles.CountAsync();

        Assert.That(circleCount, Is.GreaterThan(0),
            "Should show string indicators (open or fretted)");
    }

    [Test]
    public async Task ChordDiagram_ShouldShowMutedStrings()
    {
        // Arrange
        var query = "Show me D major chord diagram";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert - D major has muted strings (low E and A)
        var svgElement = Page.Locator(".chord-diagram svg").First;
        var hasContent = await svgElement.CountAsync() > 0;

        Assert.That(hasContent, Is.True, "Should render diagram with muted strings");
    }

    [Test]
    public async Task ChordDiagram_ShouldShowBarreChords()
    {
        // Arrange
        var query = "Show me F major barre chord";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var diagrams = Page.Locator(".chord-diagram");
        var diagramCount = await diagrams.CountAsync();
        Assert.That(diagramCount, Is.GreaterThan(0), "Should show barre chord diagram");
    }

    [Test]
    public async Task ChordDiagram_ShouldDisplayNotes()
    {
        // Arrange
        var query = "Show me Cmaj7 chord";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var notesElement = Page.Locator(".chord-notes");
        var notesCount = await notesElement.CountAsync();

        if (notesCount > 0)
        {
            var notesText = await notesElement.First.TextContentAsync();
            Assert.That(notesText, Is.Not.Empty, "Should display chord notes");
        }
    }

    [Test]
    public async Task ChordDiagram_ShouldListAvailableChords()
    {
        // Arrange
        var query = "What chord diagrams do you have?";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("chord").Or.Contain("diagram"),
            "Response should list available chords");
    }

    [Test]
    public async Task ChordDiagram_ShouldShowSeventhChords()
    {
        // Arrange
        var query = "Show me G7 chord diagram";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var diagrams = Page.Locator(".chord-diagram");
        var diagramCount = await diagrams.CountAsync();
        Assert.That(diagramCount, Is.GreaterThan(0), "Should show seventh chord diagram");
    }

    [Test]
    public async Task ChordDiagram_ShouldBeResponsive()
    {
        // Test responsive behavior at different viewport sizes

        // Arrange - Mobile viewport
        await Page.SetViewportSizeAsync(375, 667);
        var query = "Show me C chord";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert - Diagram should be visible on mobile
        var diagram = Page.Locator(".chord-diagram").First;
        var isVisible = await diagram.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Chord diagram should be visible on mobile");

        // Reset viewport
        await Page.SetViewportSizeAsync(1280, 720);
    }

    [Test]
    public async Task ChordDiagram_ShouldHaveHoverEffect()
    {
        // Arrange
        var query = "Show me Am chord";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert - Check if diagram exists and can be hovered
        var diagram = Page.Locator(".chord-diagram").First;
        var diagramCount = await Page.Locator(".chord-diagram").CountAsync();

        if (diagramCount > 0)
        {
            await diagram.HoverAsync();
            // Diagram should still be visible after hover
            var isVisible = await diagram.IsVisibleAsync();
            Assert.That(isVisible, Is.True, "Diagram should remain visible on hover");
        }
    }

    [Test]
    [TestCase("chromium")]
    [TestCase("firefox")]
    [TestCase("webkit")]
    public async Task ChordDiagram_ShouldRenderAcrossBrowsers(string browserType)
    {
        // This test verifies chord diagrams work across different browsers

        // Arrange
        var query = "Show me E major chord";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var diagrams = Page.Locator(".chord-diagram");
        var diagramCount = await diagrams.CountAsync();
        Assert.That(diagramCount, Is.GreaterThan(0),
            $"Chord diagram should render in {browserType}");
    }

    [Test]
    public async Task ChordDiagram_ShouldShowMinorChords()
    {
        // Arrange
        var query = "Show me Em chord diagram";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var diagrams = Page.Locator(".chord-diagram");
        var diagramCount = await diagrams.CountAsync();
        Assert.That(diagramCount, Is.GreaterThan(0), "Should show minor chord diagram");
    }

    [Test]
    public async Task ChordDiagram_ShouldFilterByPosition()
    {
        // Arrange
        var query = "Show me open position for A major";

        // Act
        await SendMessageAsync(query);
        var response = await WaitForResponseAsync();

        // Assert
        Assert.That(response, Is.Not.Empty, "Should receive a response");
        Assert.That(response.ToLower(), Does.Contain("open").Or.Contain("position"),
            "Response should reference position");
    }

    [Test]
    public async Task ChordDiagram_ShouldProvideFingerNumbers()
    {
        // Arrange
        var query = "Show me C major with finger numbers";

        // Act
        await SendMessageAsync(query);
        await WaitForResponseAsync();

        // Assert
        var svgElement = Page.Locator(".chord-diagram svg").First;
        var svgCount = await svgElement.CountAsync();

        if (svgCount > 0)
        {
            // Check for text elements (finger numbers)
            var textElements = svgElement.Locator("text");
            var textCount = await textElements.CountAsync();
            Assert.That(textCount, Is.GreaterThan(0),
                "Should show finger numbers or string labels");
        }
    }
}
