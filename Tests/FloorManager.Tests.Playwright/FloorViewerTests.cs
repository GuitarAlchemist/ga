namespace FloorManager.Tests.Playwright;

using System.Text.RegularExpressions;

/// <summary>
///     Tests for FloorViewer.razor (/floors) - Master/Detail view
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class FloorViewerTests : FloorManagerTestBase
{
    [Test]
    public async Task FloorViewer_ShouldLoadSuccessfully()
    {
        // Arrange & Act
        await NavigateToFloorsAsync();

        // Assert
        var title = await Page.TitleAsync();
        Assert.That(title, Does.Contain("Multi-Floor Dungeon Manager"));

        var header = await GetTextAsync("h1");
        Assert.That(header, Does.Contain("Multi-Floor Dungeon Manager"));
    }

    [Test]
    public async Task FloorViewer_ShouldShowFloorButtons()
    {
        // Arrange & Act
        await NavigateToFloorsAsync();

        // Assert
        var floorButtons = await GetCountAsync(".floor-btn");
        Assert.That(floorButtons, Is.EqualTo(6), "Should show 6 floor buttons (Floor 0-5)");
    }

    [Test]
    public async Task FloorViewer_ShouldGenerateFloorWithMusicItems()
    {
        // Arrange
        await NavigateToFloorsAsync();

        // Act - Click Floor 0 button
        await Page.ClickAsync("button:has-text('Floor 0')");

        // Wait for generation
        await WaitForFloorDataAsync();

        // Assert - Check statistics
        var statsText = await GetTextAsync(".floor-info .stats");

        // Extract music items count
        var musicItemsMatch = Regex.Match(statsText, @"🎵 Music Items: (\d+)");
        Assert.That(musicItemsMatch.Success, Is.True, "Should show music items count");

        var musicItemsCount = int.Parse(musicItemsMatch.Groups[1].Value);
        Assert.That(musicItemsCount, Is.GreaterThan(0), "Should have music items");

        // Extract rooms with items count
        var roomsWithItemsMatch = Regex.Match(statsText, @"🏠 Rooms with Items: (\d+)");
        Assert.That(roomsWithItemsMatch.Success, Is.True, "Should show rooms with items count");

        var roomsWithItemsCount = int.Parse(roomsWithItemsMatch.Groups[1].Value);
        Assert.That(roomsWithItemsCount, Is.GreaterThan(0), "Should have rooms with items");
    }

    [Test]
    public async Task FloorViewer_ShouldShowRoomListWithCategories()
    {
        // Arrange
        await NavigateToFloorsAsync();
        await Page.ClickAsync("button:has-text('Floor 0')");
        await WaitForFloorDataAsync();

        // Act
        var roomCards = Page.Locator(".room-card");
        var roomCount = await roomCards.CountAsync();

        // Assert
        Assert.That(roomCount, Is.GreaterThan(0), "Should show room cards");

        // Check first room card has category and item count
        if (roomCount > 0)
        {
            var firstCard = roomCards.First;
            var cardText = await firstCard.TextContentAsync();

            Assert.That(cardText, Is.Not.Null.And.Not.Empty, "Room card should have content");
            Assert.That(cardText, Does.Contain("Room #"), "Should show room number");
            Assert.That(cardText, Does.Match(@"\d+ item\(s\)"), "Should show item count");
        }
    }

    [Test]
    public async Task FloorViewer_ShouldShowRoomDetailsOnClick()
    {
        // Arrange
        await NavigateToFloorsAsync();
        await Page.ClickAsync("button:has-text('Floor 0')");
        await WaitForFloorDataAsync();

        // Act - Click first room card
        await ClickRoomCardAsync();

        // Assert - Check detail panel appears
        var detailPanel = Page.Locator(".room-details-panel");
        var isVisible = await detailPanel.IsVisibleAsync();
        Assert.That(isVisible, Is.True, "Detail panel should be visible");

        var detailText = await detailPanel.TextContentAsync();
        Assert.That(detailText, Does.Contain("Room #"), "Should show room number");
        Assert.That(detailText, Does.Contain("Position:"), "Should show position");
        Assert.That(detailText, Does.Contain("Size:"), "Should show size");
        Assert.That(detailText, Does.Contain("Category:"), "Should show category");
    }

    [Test]
    public async Task FloorViewer_ShouldDisplayAllMusicItemsInDetailPanel()
    {
        // Arrange
        await NavigateToFloorsAsync();
        await Page.ClickAsync("button:has-text('Floor 0')");
        await WaitForFloorDataAsync();

        // Act - Click first room card
        await ClickRoomCardAsync();

        // Assert - Check music items section
        var musicItemsSection = Page.Locator(".music-items-section");
        var hasMusicItems = await musicItemsSection.CountAsync() > 0;

        if (hasMusicItems)
        {
            var sectionText = await musicItemsSection.TextContentAsync();
            Assert.That(sectionText, Does.Contain("Music Items"), "Should have music items header");

            // Check for item entries
            var itemEntries = Page.Locator(".item-entry");
            var itemCount = await itemEntries.CountAsync();
            Assert.That(itemCount, Is.GreaterThan(0), "Should display music item entries");

            // Verify items are displayed with bullet points
            var firstItem = await itemEntries.First.TextContentAsync();
            Assert.That(firstItem, Is.Not.Null.And.Not.Empty, "Item should have content");
        }
    }

    [Test]
    public async Task FloorViewer_ShouldHighlightSelectedRoom()
    {
        // Arrange
        await NavigateToFloorsAsync();
        await Page.ClickAsync("button:has-text('Floor 0')");
        await WaitForFloorDataAsync();

        // Act - Click first room card
        var roomCards = Page.Locator(".room-card");
        await roomCards.First.ClickAsync();
        await Task.Delay(300);

        // Assert - Check if first card has selected class
        var firstCardClass = await roomCards.First.GetAttributeAsync("class");
        Assert.That(firstCardClass, Does.Contain("selected"), "Selected room should have 'selected' class");
    }

    [Test]
    public async Task FloorViewer_ShouldShowEmptyStateWhenNoRoomSelected()
    {
        // Arrange
        await NavigateToFloorsAsync();
        await Page.ClickAsync("button:has-text('Floor 0')");
        await WaitForFloorDataAsync();

        // Act - Don't click any room

        // Assert - Check for empty state or hint
        var emptyPanel = Page.Locator(".room-details-panel.empty");
        var hasEmptyState = await emptyPanel.CountAsync() > 0;

        if (hasEmptyState)
        {
            var hintText = await emptyPanel.TextContentAsync();
            Assert.That(hintText, Does.Contain("Select a room"), "Should show hint to select a room");
        }
    }

    [Test]
    public async Task FloorViewer_ShouldSwitchBetweenFloors()
    {
        // Arrange
        await NavigateToFloorsAsync();

        // Act - Generate Floor 0
        await Page.ClickAsync("button:has-text('Floor 0')");
        await WaitForFloorDataAsync();
        var floor0Stats = await GetTextAsync(".floor-info h2");

        // Act - Switch to Floor 3
        await Page.ClickAsync("button:has-text('Floor 3')");
        await WaitForFloorDataAsync();
        var floor3Stats = await GetTextAsync(".floor-info h2");

        // Assert
        Assert.That(floor0Stats, Does.Contain("Floor 0"), "Should show Floor 0");
        Assert.That(floor3Stats, Does.Contain("Floor 3"), "Should show Floor 3");
        Assert.That(floor0Stats, Is.Not.EqualTo(floor3Stats), "Floor headers should be different");
    }

    [Test]
    public async Task FloorViewer_ShouldShow3DViewLink()
    {
        // Arrange
        await NavigateToFloorsAsync();
        await Page.ClickAsync("button:has-text('Floor 0')");
        await WaitForFloorDataAsync();

        // Act
        var viewLinks = Page.Locator("a.view-3d-link");
        var linkCount = await viewLinks.CountAsync();

        // Assert
        Assert.That(linkCount, Is.GreaterThan(0), "Should show 3D view links for generated floors");

        var firstLink = await viewLinks.First.GetAttributeAsync("href");
        Assert.That(firstLink, Does.Match(@"/floor/\d+"), "Link should point to floor viewer");
    }
}
