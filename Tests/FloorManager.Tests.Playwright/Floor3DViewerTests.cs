namespace FloorManager.Tests.Playwright;

using System.Text.RegularExpressions;

/// <summary>
///     Tests for Floor3DViewer.razor (/floor/{id}) - 3D floor viewer
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class Floor3DViewerTests : FloorManagerTestBase
{
    [Test]
    public async Task Floor3DViewer_ShouldLoadSuccessfully()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);

        // Assert
        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Floor Manager"));

        var header = await GetTextAsync("h2");
        Assert.That(header, Does.Contain("Floor 5"), "Should show floor number in header");
    }

    [Test]
    public async Task Floor3DViewer_ShouldShowCorrectFloorStatistics()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Assert - Check all stat cards
        var musicItemsStat = await GetTextAsync(".stat-card:has-text('Music Items') .stat-value");
        var musicItemsCount = ExtractNumber(musicItemsStat);
        Assert.That(musicItemsCount, Is.GreaterThan(0), "Should show music items count > 0");

        var roomsStat = await GetTextAsync(".stat-card:has-text('Total Rooms') .stat-value");
        var roomsCount = ExtractNumber(roomsStat);
        Assert.That(roomsCount, Is.GreaterThan(0), "Should show rooms count > 0");

        var corridorsStat = await GetTextAsync(".stat-card:has-text('Corridors') .stat-value");
        var corridorsCount = ExtractNumber(corridorsStat);
        Assert.That(corridorsCount, Is.GreaterThanOrEqualTo(0), "Should show corridors count >= 0");
    }

    [Test]
    public async Task Floor3DViewer_ShouldShowRoomListPanel()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Assert
        var roomListPanel = Page.Locator(".room-list-panel");
        var isVisible = await roomListPanel.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Room list panel should be visible");

        var panelHeader = await roomListPanel.Locator("h3").TextContentAsync();
        Assert.That(panelHeader, Does.Contain("Rooms with Music Items"), "Should show room list header");
        Assert.That(panelHeader, Does.Match(@"\(\d+\)"), "Should show count in parentheses");
    }

    [Test]
    public async Task Floor3DViewer_ShouldDisplayRoomsWithItemCounts()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Assert
        var roomCards = Page.Locator(".room-card");
        var roomCount = await roomCards.CountAsync();
        Assert.That(roomCount, Is.GreaterThan(0), "Should show room cards");

        // Check first room card structure
        if (roomCount > 0)
        {
            var firstCard = roomCards.First;
            var cardText = await firstCard.TextContentAsync();

            Assert.That(cardText, Does.Contain("Room #"), "Should show room number");
            Assert.That(cardText, Does.Match(@"\d+ item\(s\)"), "Should show item count");
        }
    }

    [Test]
    public async Task Floor3DViewer_ShouldShowRoomDetailsOnClick()
    {
        // Arrange
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Act - Click first room card
        await ClickRoomCardAsync();

        // Assert
        var detailPanel = Page.Locator(".room-detail-panel");
        var isVisible = await detailPanel.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Room detail panel should be visible");

        var detailText = await detailPanel.TextContentAsync();
        Assert.That(detailText, Does.Contain("Room #"), "Should show room number");
        Assert.That(detailText, Does.Contain("Position:"), "Should show position");
        Assert.That(detailText, Does.Contain("Size:"), "Should show size");
    }

    [Test]
    public async Task Floor3DViewer_ShouldDisplayAllMusicItems()
    {
        // Arrange
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Act - Click first room card
        await ClickRoomCardAsync();

        // Assert - Check music items are displayed
        var itemsList = Page.Locator(".items-list");
        var hasItems = await itemsList.CountAsync() > 0;

        if (hasItems)
        {
            var itemEntries = Page.Locator(".item-entry");
            var itemCount = await itemEntries.CountAsync();
            Assert.That(itemCount, Is.GreaterThan(0), "Should display music items");

            // Verify first item has content
            var firstItem = await itemEntries.First.TextContentAsync();
            Assert.That(firstItem, Is.Not.Null.And.Not.Empty, "Item should have content");

            // Verify items have bullet points
            var hasBullet = await itemEntries.First.Locator(".item-bullet").CountAsync() > 0;
            Assert.That(hasBullet, Is.True, "Items should have bullet points");
        }
    }

    [Test]
    public async Task Floor3DViewer_ShouldNotShowZeroItems()
    {
        // Arrange
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Act - Click first room card
        await ClickRoomCardAsync();

        // Assert - Verify items count is not showing "0 items"
        var detailPanel = Page.Locator(".room-detail-panel");
        var detailText = await detailPanel.TextContentAsync();

        // Should NOT contain "0 item(s)" or "0 Music Items"
        Assert.That(detailText, Does.Not.Match(@"0 item\(s\)"), "Should not show 0 items");
        Assert.That(detailText, Does.Not.Contain("(0)"), "Should not show (0) items");
    }

    [Test]
    public async Task Floor3DViewer_ShouldShowCategoryForRoom()
    {
        // Arrange
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Act - Click first room card
        await ClickRoomCardAsync();

        // Assert
        var detailPanel = Page.Locator(".room-detail-panel");
        var detailText = await detailPanel.TextContentAsync();
        Assert.That(detailText, Does.Contain("Category:"), "Should show category label");

        // Category should not be empty
        var categoryMatch = Regex.Match(detailText, @"Category:\s*(.+?)(?:\n|$)");
        if (categoryMatch.Success)
        {
            var category = categoryMatch.Groups[1].Value.Trim();
            Assert.That(category, Is.Not.Empty, "Category should have a value");
        }
    }

    [Test]
    public async Task Floor3DViewer_ShouldHaveRegenerateButton()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Assert
        var regenerateButton = Page.Locator("button:has-text('Regenerate')");
        var buttonCount = await regenerateButton.CountAsync();
        Assert.That(buttonCount, Is.GreaterThan(0), "Should have regenerate button");
    }

    [Test]
    public async Task Floor3DViewer_ShouldHaveBackToFloorsLink()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);

        // Assert
        var backLink = Page.Locator("a:has-text('Back to All Floors')");
        var linkCount = await backLink.CountAsync();
        Assert.That(linkCount, Is.GreaterThan(0), "Should have back to floors link");

        var href = await backLink.First.GetAttributeAsync("href");
        Assert.That(href, Does.Contain("/floors"), "Link should point to /floors");
    }

    [Test]
    public async Task Floor3DViewer_ShouldShowGenerationSummary()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Assert - Check for generation summary
        var summarySection = Page.Locator(".generation-summary");
        var hasSummary = await summarySection.CountAsync() > 0;

        if (hasSummary)
        {
            var summaryText = await summarySection.TextContentAsync();
            Assert.That(summaryText, Does.Contain("Generation Time"), "Should show generation time");
            Assert.That(summaryText, Does.Contain("Coverage"), "Should show coverage percentage");
        }
    }

    [Test]
    [TestCase(0, "Set Classes")]
    [TestCase(1, "Forte")]
    [TestCase(3, "Chord")]
    [TestCase(5, "Voicing")]
    public async Task Floor3DViewer_ShouldLoadDifferentFloors(int floorNumber, string expectedContent)
    {
        // Arrange & Act
        await NavigateToFloorAsync(floorNumber);
        await WaitForFloorDataAsync();

        // Assert
        var header = await GetTextAsync("h2");
        Assert.That(header, Does.Contain($"Floor {floorNumber}"), $"Should show Floor {floorNumber}");

        var pageContent = await Page.ContentAsync();
        Assert.That(pageContent, Does.Contain(expectedContent).IgnoreCase,
            $"Floor {floorNumber} should contain '{expectedContent}'");
    }

    [Test]
    public async Task Floor3DViewer_ShouldShowViewModeToggle()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Assert - Check for 2D/3D view toggle buttons
        var view2DButton = Page.Locator("button:has-text('2D View')");
        var view3DButton = Page.Locator("button:has-text('3D View')");

        var has2DButton = await view2DButton.CountAsync() > 0;
        var has3DButton = await view3DButton.CountAsync() > 0;

        Assert.That(has2DButton || has3DButton, Is.True, "Should have view mode toggle buttons");
    }

    [Test]
    public async Task Floor3DViewer_RoomListShouldMatchStatistics()
    {
        // Arrange & Act
        await NavigateToFloorAsync(5);
        await WaitForFloorDataAsync();

        // Get count from statistics
        var roomListHeader = await GetTextAsync(".room-list-panel h3");
        var headerCountMatch = Regex.Match(roomListHeader, @"\((\d+)\)");
        Assert.That(headerCountMatch.Success, Is.True, "Header should show count");
        var headerCount = int.Parse(headerCountMatch.Groups[1].Value);

        // Get actual room card count
        var roomCards = Page.Locator(".room-card");
        var actualCount = await roomCards.CountAsync();

        // Assert
        Assert.That(actualCount, Is.EqualTo(headerCount),
            "Room card count should match header count");
    }
}
