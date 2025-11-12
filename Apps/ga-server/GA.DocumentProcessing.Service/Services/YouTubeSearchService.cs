namespace GA.DocumentProcessing.Service.Services;

using System.Text.Json;
using System.Text.RegularExpressions;
using Models;

/// <summary>
/// Service for searching YouTube videos without API key
/// Uses Invidious public instances for free YouTube search
/// </summary>
public class YouTubeSearchService
{
    private readonly ILogger<YouTubeSearchService> _logger;
    private readonly HttpClient _httpClient;

    // Public Invidious instances (free, no API key needed)
    private readonly string[] _invidiousInstances = new[]
    {
        "https://invidious.snopyta.org",
        "https://yewtu.be",
        "https://invidious.kavin.rocks",
        "https://vid.puffyan.us"
    };

    public YouTubeSearchService(ILogger<YouTubeSearchService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Search YouTube for videos matching the query
    /// </summary>
    public async Task<List<YouTubeSearchResult>> SearchAsync(
        string query,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching YouTube for: {Query} (max {MaxResults} results)", query, maxResults);

        // Try each Invidious instance until one works
        foreach (var instance in _invidiousInstances)
        {
            try
            {
                var results = await SearchWithInvidiousAsync(instance, query, maxResults, cancellationToken);
                if (results.Any())
                {
                    _logger.LogInformation("Found {Count} results using {Instance}", results.Count, instance);
                    return results;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search with instance {Instance}", instance);
            }
        }

        _logger.LogWarning("All Invidious instances failed, returning empty results");
        return new List<YouTubeSearchResult>();
    }

    /// <summary>
    /// Search using a specific Invidious instance
    /// </summary>
    private async Task<List<YouTubeSearchResult>> SearchWithInvidiousAsync(
        string instance,
        string query,
        int maxResults,
        CancellationToken cancellationToken)
    {
        var results = new List<YouTubeSearchResult>();

        try
        {
            // Invidious API endpoint: /api/v1/search?q=query&type=video
            var url = $"{instance}/api/v1/search?q={Uri.EscapeDataString(query)}&type=video&max_results={maxResults}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var searchResults = JsonSerializer.Deserialize<List<InvidiousSearchResult>>(json);

            if (searchResults != null)
            {
                foreach (var item in searchResults.Take(maxResults))
                {
                    results.Add(new YouTubeSearchResult
                    {
                        VideoId = item.VideoId ?? string.Empty,
                        Title = item.Title ?? string.Empty,
                        Description = item.Description ?? string.Empty,
                        Url = $"https://www.youtube.com/watch?v={item.VideoId}",
                        ChannelName = item.Author ?? string.Empty,
                        ViewCount = item.ViewCount,
                        Duration = TimeSpan.FromSeconds(item.LengthSeconds),
                        PublishedAt = DateTimeOffset.FromUnixTimeSeconds(item.Published).DateTime,
                        ThumbnailUrl = item.VideoThumbnails?.FirstOrDefault()?.Url ?? string.Empty
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching with Invidious instance {Instance}", instance);
            throw;
        }

        return results;
    }
}

/// <summary>
/// Invidious API search result model
/// </summary>
internal class InvidiousSearchResult
{
    public string? VideoId { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
    public long ViewCount { get; set; }
    public long Published { get; set; }
    public int LengthSeconds { get; set; }
    public List<InvidiousThumbnail>? VideoThumbnails { get; set; }
}

/// <summary>
/// Invidious thumbnail model
/// </summary>
internal class InvidiousThumbnail
{
    public string? Url { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}

/// <summary>
/// YouTube search result
/// </summary>
public class YouTubeSearchResult
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    public TimeSpan Duration { get; set; }
    public DateTime PublishedAt { get; set; }
    public string ThumbnailUrl { get; set; } = string.Empty;
}

