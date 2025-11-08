namespace GaMcpServer.Tools;

using GA.Business.Web.Services;
using ModelContextProtocol.Server;

/// <summary>
///     MCP tool wrapper for web search functionality (delegates to shared service)
/// </summary>
[McpServerToolType]
public class WebSearchToolWrapper(WebSearchService service)
{
    [McpServerTool]
    [Description("Search DuckDuckGo for music theory and guitar-related information")]
    public async Task<string> SearchDuckDuckGo(
        [Description("Search query (e.g., 'circle of fifths', 'guitar chord progressions')")]
        string query,
        [Description("Maximum number of results to return")]
        int maxResults = 5)
    {
        return await service.SearchDuckDuckGoAsync(query, maxResults);
    }

    [McpServerTool]
    [Description("Search Wikipedia for music theory topics")]
    public async Task<string> SearchWikipedia(
        [Description("Search query for Wikipedia")]
        string query,
        [Description("Maximum number of results to return")]
        int maxResults = 5)
    {
        return await service.SearchWikipediaAsync(query, maxResults);
    }

    [McpServerTool]
    [Description("Get a Wikipedia article summary")]
    public async Task<string> GetWikipediaSummary(
        [Description("Wikipedia article title")]
        string title)
    {
        return await service.GetWikipediaSummaryAsync(title);
    }

    [McpServerTool]
    [Description("Search for music theory content on specific domains")]
    public async Task<string> SearchMusicTheorySites(
        [Description("Search query")] string query,
        [Description("Site to search (musictheory.net, teoria.com, or all)")]
        string site = "all")
    {
        return await service.SearchMusicTheorySitesAsync(query, site);
    }
}
