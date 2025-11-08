namespace FloorManager.Tests.Playwright;

using System.Text.RegularExpressions;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

/// <summary>
///     Base class for FloorManager Playwright tests
/// </summary>
public class FloorManagerTestBase : PageTest
{
    protected const string BaseUrl = "http://localhost:5233";
    protected const int DefaultTimeout = 30000; // 30 seconds

    [SetUp]
    public async Task Setup()
    {
        // Set default timeout
        Page.SetDefaultTimeout(DefaultTimeout);

        // Navigate to the FloorManager home page
        await Page.GotoAsync(BaseUrl);

        // Wait for the page to load
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    ///     Navigate to a specific floor viewer
    /// </summary>
    protected async Task NavigateToFloorAsync(int floorNumber)
    {
        await Page.GotoAsync($"{BaseUrl}/floor/{floorNumber}");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    ///     Navigate to the multi-floor viewer
    /// </summary>
    protected async Task NavigateToFloorsAsync()
    {
        await Page.GotoAsync($"{BaseUrl}/floors");
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);
    }

    /// <summary>
    ///     Wait for floor data to load
    /// </summary>
    protected async Task WaitForFloorDataAsync()
    {
        // Wait for the generation complete indicator
        await Page.WaitForSelectorAsync("text=Generation Complete", new() { Timeout = DefaultTimeout });
    }

    /// <summary>
    ///     Get text content from a selector
    /// </summary>
    protected async Task<string> GetTextAsync(string selector)
    {
        var element = await Page.WaitForSelectorAsync(selector);
        return await element!.TextContentAsync() ?? "";
    }

    /// <summary>
    ///     Get numeric value from text (e.g., "188 Music Items" -> 188)
    /// </summary>
    protected int ExtractNumber(string text)
    {
        var match = Regex.Match(text, @"\d+");
        return match.Success ? int.Parse(match.Value) : 0;
    }

    /// <summary>
    ///     Click a room card by index
    /// </summary>
    protected async Task ClickRoomCardAsync(int index = 0)
    {
        var roomCards = Page.Locator(".room-card");
        var count = await roomCards.CountAsync();

        if (count > index)
        {
            await roomCards.Nth(index).ClickAsync();
            // Wait for details to update
            await Task.Delay(500);
        }
    }

    /// <summary>
    ///     Get count of elements matching selector
    /// </summary>
    protected async Task<int> GetCountAsync(string selector)
    {
        return await Page.Locator(selector).CountAsync();
    }
}
