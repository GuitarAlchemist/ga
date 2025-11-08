namespace GA.Business.Web.Services;

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

/// <summary>
///     Service for searching the web for music theory and guitar-related content
/// </summary>
public class WebSearchService(
    HttpClient httpClient,
    WebContentCache cache,
    ILogger<WebSearchService> logger)
{
    /// <summary>
    ///     Search DuckDuckGo for music theory and guitar-related information
    /// </summary>
    public async Task<string> SearchDuckDuckGoAsync(string query, int maxResults = 5)
    {
        try
        {
            logger.LogInformation("Searching DuckDuckGo for: {Query}", query);

            var cacheKey = $"ddg:{query}:{maxResults}";
            var results = await cache.GetOrCreateAsync(
                cacheKey,
                async () => await PerformDuckDuckGoSearchAsync(query, maxResults),
                TimeSpan.FromHours(6));

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching DuckDuckGo for: {Query}", query);
            return $"Error performing search: {ex.Message}";
        }
    }

    /// <summary>
    ///     Search Wikipedia for music theory topics
    /// </summary>
    public async Task<string> SearchWikipediaAsync(string query, int maxResults = 5)
    {
        try
        {
            logger.LogInformation("Searching Wikipedia for: {Query}", query);

            var cacheKey = $"wiki:{query}:{maxResults}";
            var results = await cache.GetOrCreateAsync(
                cacheKey,
                async () => await PerformWikipediaSearchAsync(query, maxResults),
                TimeSpan.FromHours(24));

            return results;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching Wikipedia for: {Query}", query);
            return $"Error performing search: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get a Wikipedia article summary
    /// </summary>
    public async Task<string> GetWikipediaSummaryAsync(string title)
    {
        try
        {
            logger.LogInformation("Getting Wikipedia summary for: {Title}", title);

            var cacheKey = $"wiki:summary:{title}";
            var summary = await cache.GetOrCreateAsync(
                cacheKey,
                async () => await FetchWikipediaSummaryAsync(title),
                TimeSpan.FromDays(7));

            return summary;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting Wikipedia summary for: {Title}", title);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    ///     Search for music theory content on specific domains
    /// </summary>
    public async Task<string> SearchMusicTheorySitesAsync(string query, string site = "all")
    {
        try
        {
            var siteQuery = site.ToLowerInvariant() switch
            {
                "musictheory.net" => $"{query} site:musictheory.net",
                "teoria.com" => $"{query} site:teoria.com",
                "all" => $"{query} (site:musictheory.net OR site:teoria.com)",
                _ => query
            };

            logger.LogInformation("Searching music theory sites for: {Query}", siteQuery);

            return await SearchDuckDuckGoAsync(siteQuery);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching music theory sites");
            return $"Error: {ex.Message}";
        }
    }

    private async Task<string> PerformDuckDuckGoSearchAsync(string query, int maxResults)
    {
        // DuckDuckGo Instant Answer API
        var encodedQuery = Uri.EscapeDataString(query);
        var url = $"https://api.duckduckgo.com/?q={encodedQuery}&format=json&no_html=1&skip_disambig=1";

        var json = await httpClient.GetStringAsync(url);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var sb = new StringBuilder();
        sb.AppendLine($"?? Search results for: {query}");
        sb.AppendLine();

        // Get abstract
        if (root.TryGetProperty("Abstract", out var abstractProp) && !string.IsNullOrEmpty(abstractProp.GetString()))
        {
            sb.AppendLine("?? Summary:");
            sb.AppendLine(abstractProp.GetString());
            sb.AppendLine();

            if (root.TryGetProperty("AbstractURL", out var urlProp) && !string.IsNullOrEmpty(urlProp.GetString()))
            {
                sb.AppendLine($"Source: {urlProp.GetString()}");
                sb.AppendLine();
            }
        }

        // Get related topics
        if (root.TryGetProperty("RelatedTopics", out var relatedProp) && relatedProp.ValueKind == JsonValueKind.Array)
        {
            var topics = relatedProp.EnumerateArray().Take(maxResults).ToList();
            if (topics.Any())
            {
                sb.AppendLine("?? Related Topics:");
                sb.AppendLine();

                foreach (var topic in topics)
                {
                    if (topic.TryGetProperty("Text", out var textProp) && !string.IsNullOrEmpty(textProp.GetString()))
                    {
                        sb.AppendLine($"� {textProp.GetString()}");

                        if (topic.TryGetProperty("FirstURL", out var firstUrlProp) &&
                            !string.IsNullOrEmpty(firstUrlProp.GetString()))
                        {
                            sb.AppendLine($"  {firstUrlProp.GetString()}");
                        }

                        sb.AppendLine();
                    }
                }
            }
        }

        var result = sb.ToString();
        return string.IsNullOrWhiteSpace(result)
            ? $"No results found for: {query}. Try a different search term or use SearchWikipedia instead."
            : result;
    }

    private async Task<string> PerformWikipediaSearchAsync(string query, int maxResults)
    {
        // Wikipedia API search
        var encodedQuery = Uri.EscapeDataString(query);
        var url =
            $"https://en.wikipedia.org/w/api.php?action=opensearch&search={encodedQuery}&limit={maxResults}&format=json";

        var json = await httpClient.GetStringAsync(url);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() < 4)
        {
            return $"No Wikipedia results found for: {query}";
        }

        var titles = root[1].EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        var descriptions = root[2].EnumerateArray().Select(e => e.GetString() ?? "").ToList();
        var urls = root[3].EnumerateArray().Select(e => e.GetString() ?? "").ToList();

        if (!titles.Any())
        {
            return $"No Wikipedia results found for: {query}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"?? Wikipedia results for: {query}");
        sb.AppendLine();

        for (var i = 0; i < titles.Count; i++)
        {
            sb.AppendLine($"{i + 1}. {titles[i]}");
            if (!string.IsNullOrEmpty(descriptions[i]))
            {
                sb.AppendLine($"   {descriptions[i]}");
            }

            sb.AppendLine($"   {urls[i]}");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private async Task<string> FetchWikipediaSummaryAsync(string title)
    {
        // Wikipedia API extract
        var encodedTitle = Uri.EscapeDataString(title);
        var url =
            $"https://en.wikipedia.org/w/api.php?action=query&prop=extracts&exintro=1&explaintext=1&titles={encodedTitle}&format=json";

        var json = await httpClient.GetStringAsync(url);
        var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("query", out var query) ||
            !query.TryGetProperty("pages", out var pages))
        {
            return $"Could not find Wikipedia article: {title}";
        }

        var page = pages.EnumerateObject().FirstOrDefault();
        if (page.Value.ValueKind == JsonValueKind.Null)
        {
            return $"Could not find Wikipedia article: {title}";
        }

        var sb = new StringBuilder();

        if (page.Value.TryGetProperty("title", out var titleProp))
        {
            sb.AppendLine($"?? {titleProp.GetString()}");
            sb.AppendLine();
        }

        if (page.Value.TryGetProperty("extract", out var extractProp))
        {
            sb.AppendLine(extractProp.GetString());
            sb.AppendLine();
            sb.AppendLine($"Source: https://en.wikipedia.org/wiki/{Uri.EscapeDataString(title)}");
        }
        else
        {
            return $"No summary available for: {title}";
        }

        return sb.ToString();
    }
}
