namespace GaMcpServer.Tools;

using GA.Business.Web.Services;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool wrapper for RSS/Atom feed reading (delegates to shared service)
/// </summary>
[McpServerToolType]
public class FeedReaderToolWrapper(FeedReaderService service)
{
    [McpServerTool]
    [Description("Read latest articles from an RSS or Atom feed")]
    public async Task<string> ReadFeed(
        [Description("Feed URL or known feed name (musictheory, justinguitar, guitarnoise, teoria)")]
        string feedSource,
        [Description("Maximum number of items to return")]
        int maxItems = 10)
    {
        return await service.ReadFeedAsync(feedSource, maxItems);
    }

    [McpServerTool]
    [Description("List all available known feeds")]
    public string ListKnownFeeds()
    {
        return service.ListKnownFeeds();
    }

    [McpServerTool]
    [Description("Search feed items by keyword")]
    public async Task<string> SearchFeed(
        [Description("Feed URL or known feed name")]
        string feedSource,
        [Description("Keyword to search for in titles and descriptions")]
        string keyword,
        [Description("Maximum number of matching items to return")]
        int maxItems = 10)
    {
        return await service.SearchFeedAsync(feedSource, keyword, maxItems);
    }

    [McpServerTool]
    [Description("Get feed items from a specific date range")]
    public async Task<string> GetFeedByDateRange(
        [Description("Feed URL or known feed name")]
        string feedSource,
        [Description("Start date (YYYY-MM-DD)")]
        string startDate,
        [Description("End date (YYYY-MM-DD)")] string endDate,
        [Description("Maximum number of items to return")]
        int maxItems = 20)
    {
        return await service.GetFeedByDateRangeAsync(feedSource, startDate, endDate, maxItems);
    }
}
