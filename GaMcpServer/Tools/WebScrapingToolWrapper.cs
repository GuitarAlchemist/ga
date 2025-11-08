namespace GaMcpServer.Tools;

using GA.Business.Web.Services;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool wrapper for web scraping functionality (delegates to shared service)
/// </summary>
[McpServerToolType]
public class WebScrapingToolWrapper(WebScrapingService service)
{
    [McpServerTool]
    [Description("Fetch and extract text content from a web page")]
    public async Task<string> FetchWebPage(
        [Description("URL of the web page to fetch")]
        string url,
        [Description("Extract only main content (removes navigation, ads, etc.)")]
        bool extractMainContent = true)
    {
        return await service.FetchWebPageAsync(url, extractMainContent);
    }

    [McpServerTool]
    [Description("Extract specific elements from a web page using CSS selectors")]
    public async Task<string> ExtractElements(
        [Description("URL of the web page")] string url,
        [Description("CSS selector (e.g., 'h1', '.article-content', '#main')")]
        string cssSelector)
    {
        return await service.ExtractElementsAsync(url, cssSelector);
    }

    [McpServerTool]
    [Description("Extract all links from a web page")]
    public async Task<string> ExtractLinks(
        [Description("URL of the web page")] string url,
        [Description("Optional: filter links by domain")]
        string? filterDomain = null)
    {
        return await service.ExtractLinksAsync(url, filterDomain);
    }
}
