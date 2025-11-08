namespace GA.Business.Web.Services;

using System.ServiceModel.Syndication;
using System.Text;
using System.Xml;
using Microsoft.Extensions.Logging;

/// <summary>
///     Service for reading RSS and Atom feeds
/// </summary>
public class FeedReaderService(
    HttpClient httpClient,
    WebContentCache cache,
    ILogger<FeedReaderService> logger)
{
    private static readonly Dictionary<string, string> _knownFeeds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["musictheory"] = "https://www.musictheory.net/rss",
        ["justinguitar"] = "https://www.justinguitar.com/feed",
        ["guitarnoise"] = "https://www.guitarnoise.com/feed/",
        ["teoria"] = "https://www.teoria.com/rss"
    };

    /// <summary>
    ///     Read latest articles from an RSS/Atom feed
    /// </summary>
    public async Task<string> ReadFeedAsync(string feedSource, int maxItems = 10)
    {
        try
        {
            var feedUrl = GetFeedUrl(feedSource);
            logger.LogInformation("Reading feed: {FeedUrl}", feedUrl);

            var cacheKey = $"feed:{feedUrl}:{maxItems}";
            var content = await cache.GetOrCreateAsync(
                cacheKey,
                async () => await FetchFeedAsync(feedUrl, maxItems),
                TimeSpan.FromMinutes(30));

            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading feed: {FeedSource}", feedSource);
            return $"Error reading feed: {ex.Message}";
        }
    }

    /// <summary>
    ///     List all known feeds
    /// </summary>
    public string ListKnownFeeds()
    {
        var sb = new StringBuilder();
        sb.AppendLine("?? Known RSS Feeds:");
        sb.AppendLine();

        foreach (var feed in _knownFeeds)
        {
            sb.AppendLine($"� {feed.Key}");
            sb.AppendLine($"  {feed.Value}");
            sb.AppendLine();
        }

        sb.AppendLine("You can use the feed name (e.g., 'musictheory') or the full URL.");
        return sb.ToString();
    }

    /// <summary>
    ///     Search feed items by keyword
    /// </summary>
    public async Task<string> SearchFeedAsync(string feedSource, string keyword, int maxItems = 10)
    {
        try
        {
            var feedUrl = GetFeedUrl(feedSource);
            logger.LogInformation("Searching feed {FeedUrl} for keyword: {Keyword}", feedUrl, keyword);

            var allItems = await FetchFeedItemsAsync(feedUrl);
            var matchingItems = allItems
                .Where(item =>
                    (item.Title?.Text?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.Summary?.Text?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false))
                .Take(maxItems)
                .ToList();

            if (!matchingItems.Any())
            {
                return $"No items found matching keyword: {keyword}";
            }

            return FormatFeedItems(matchingItems, $"Search results for '{keyword}'");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error searching feed: {FeedSource}", feedSource);
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    ///     Get feed items from a specific date range
    /// </summary>
    public async Task<string> GetFeedByDateRangeAsync(
        string feedSource,
        string startDate,
        string endDate,
        int maxItems = 20)
    {
        try
        {
            var feedUrl = GetFeedUrl(feedSource);
            var start = DateTime.Parse(startDate);
            var end = DateTime.Parse(endDate);

            logger.LogInformation("Getting feed items from {Start} to {End}", start, end);

            var allItems = await FetchFeedItemsAsync(feedUrl);
            var matchingItems = allItems
                .Where(item => item.PublishDate >= start && item.PublishDate <= end)
                .Take(maxItems)
                .ToList();

            if (!matchingItems.Any())
            {
                return $"No items found between {startDate} and {endDate}";
            }

            return FormatFeedItems(matchingItems, $"Items from {startDate} to {endDate}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting feed by date range");
            return $"Error: {ex.Message}";
        }
    }

    private string GetFeedUrl(string feedSource)
    {
        if (_knownFeeds.TryGetValue(feedSource, out var url))
        {
            return url;
        }

        if (Uri.TryCreate(feedSource, UriKind.Absolute, out _))
        {
            return feedSource;
        }

        throw new ArgumentException($"Unknown feed source: {feedSource}. Use ListKnownFeeds() to see available feeds.");
    }

    private async Task<string> FetchFeedAsync(string feedUrl, int maxItems)
    {
        var items = await FetchFeedItemsAsync(feedUrl);
        var limitedItems = items.Take(maxItems).ToList();

        if (!limitedItems.Any())
        {
            return "No items found in feed.";
        }

        return FormatFeedItems(limitedItems, $"Latest from {feedUrl}");
    }

    private async Task<List<SyndicationItem>> FetchFeedItemsAsync(string feedUrl)
    {
        var xml = await httpClient.GetStringAsync(feedUrl);

        using var stringReader = new StringReader(xml);
        using var xmlReader = XmlReader.Create(stringReader);

        var feed = SyndicationFeed.Load(xmlReader);
        return feed.Items.ToList();
    }

    private string FormatFeedItems(List<SyndicationItem> items, string title)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"?? {title}");
        sb.AppendLine();

        foreach (var item in items)
        {
            sb.AppendLine($"?? {item.Title?.Text ?? "No title"}");

            if (item.PublishDate != DateTimeOffset.MinValue)
            {
                sb.AppendLine($"   ?? {item.PublishDate:yyyy-MM-dd HH:mm}");
            }

            if (item.Summary != null && !string.IsNullOrWhiteSpace(item.Summary.Text))
            {
                var summary = item.Summary.Text;
                if (summary.Length > 200)
                {
                    summary = summary.Substring(0, 200) + "...";
                }

                sb.AppendLine($"   {summary}");
            }

            var link = item.Links.FirstOrDefault()?.Uri?.ToString();
            if (!string.IsNullOrEmpty(link))
            {
                sb.AppendLine($"   ?? {link}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }
}
