# Web Integration for GaMcpServer

## Overview

The GaMcpServer now includes comprehensive web integration capabilities, allowing the chatbot to access external
knowledge sources including:

- **Web Scraping**: Fetch and extract content from music theory websites
- **RSS/Atom Feeds**: Subscribe to music theory blogs and guitar learning sites
- **Web Search**: Search DuckDuckGo and Wikipedia for relevant information
- **Caching**: Intelligent caching to reduce API calls and improve performance

## Architecture

### Components

1. **Shared Services** (`Common/GA.Business.Core.Web/Services/`)
    - `WebContentCache.cs` - Memory-based caching for external content
    - `WebScrapingService.cs` - Web page fetching and content extraction
    - `FeedReaderService.cs` - RSS/Atom feed consumption
    - `WebSearchService.cs` - Web search integration

2. **MCP Tool Wrappers** (`GaMcpServer/Tools/`)
    - `WebScrapingToolWrapper.cs` - Delegates to shared `WebScrapingService`
    - `FeedReaderToolWrapper.cs` - Delegates to shared `FeedReaderService`
    - `WebSearchToolWrapper.cs` - Delegates to shared `WebSearchService`

3. **Configuration** (`appsettings.json`)
    - Cache settings
    - Rate limiting
    - Allowed domains
    - Known RSS feeds
    - Search API configuration

## Installation

The required NuGet packages are already installed:

```bash
dotnet add package HtmlAgilityPack
dotnet add package System.ServiceModel.Syndication
dotnet add package Microsoft.Extensions.Caching.Memory
dotnet add package Microsoft.Extensions.Http
```

## Configuration

Edit `GaMcpServer/appsettings.json` to customize:

```json
{
  "WebIntegration": {
    "CacheSettings": {
      "DefaultExpirationMinutes": 60,
      "SlidingExpirationMinutes": 15,
      "MaxCacheSizeMB": 100
    },
    "AllowedDomains": [
      "musictheory.net",
      "wikipedia.org",
      "guitarnoise.com"
    ],
    "RssFeeds": {
      "MusicTheory": [
        "https://www.musictheory.net/rss"
      ]
    }
  }
}
```

## Available Tools

### 1. WebScraping Tool (Wrapper over `WebScrapingService`)

#### FetchWebPage

Fetch and extract text content from a web page.

**Parameters:**

- `url` (string): The URL to fetch
- `extractMainContent` (bool): Extract only main content (default: true)

**Example:**

```
FetchWebPage("https://www.musictheory.net/lessons/51", true)
```

#### ExtractElements

Extract specific elements using CSS selectors.

**Parameters:**

- `url` (string): The URL to fetch
- `cssSelector` (string): CSS selector (e.g., "h1", ".article-content")

**Example:**

```
ExtractElements("https://www.musictheory.net/lessons", "h2")
```

#### ExtractLinks

Get all links from a web page.

**Parameters:**

- `url` (string): The URL to fetch
- `filterDomain` (string, optional): Filter links by domain

**Example:**

```
ExtractLinks("https://www.musictheory.net", "musictheory.net")
```

### 2. FeedReader Tool (Wrapper over `FeedReaderService`)

#### ReadFeed

Get latest articles from an RSS/Atom feed.

**Parameters:**

- `feedSource` (string): Feed URL or known feed name (musictheory, justinguitar, guitarnoise, teoria)
- `maxItems` (int): Maximum items to return (default: 10)

**Example:**

```
ReadFeed("musictheory", 5)
ReadFeed("https://www.justinguitar.com/feed", 10)
```

#### ListKnownFeeds

List all available known feeds.

**Example:**

```
ListKnownFeeds()
```

#### SearchFeed

Search feed items by keyword.

**Parameters:**

- `feedSource` (string): Feed URL or known feed name
- `keyword` (string): Keyword to search for
- `maxItems` (int): Maximum matching items (default: 10)

**Example:**

```
SearchFeed("musictheory", "chord progressions", 5)
```

#### GetFeedByDateRange

Get feed items from a specific date range.

**Parameters:**

- `feedSource` (string): Feed URL or known feed name
- `startDate` (string): Start date (YYYY-MM-DD)
- `endDate` (string): End date (YYYY-MM-DD)
- `maxItems` (int): Maximum items (default: 20)

**Example:**

```
GetFeedByDateRange("musictheory", "2025-01-01", "2025-01-31", 10)
```

### 3. WebSearch Tool (Wrapper over `WebSearchService`)

#### SearchDuckDuckGo

Search DuckDuckGo for music theory content.

**Parameters:**

- `query` (string): Search query
- `maxResults` (int): Maximum results (default: 5)

**Example:**

```
SearchDuckDuckGo("circle of fifths", 5)
```

#### SearchWikipedia

Search Wikipedia for music theory topics.

**Parameters:**

- `query` (string): Search query
- `maxResults` (int): Maximum results (default: 5)

**Example:**

```
SearchWikipedia("harmonic minor scale", 5)
```

#### GetWikipediaSummary

Get a Wikipedia article summary.

**Parameters:**

- `title` (string): Wikipedia article title

**Example:**

```
GetWikipediaSummary("Circle of fifths")
```

#### SearchMusicTheorySites

Search specific music theory websites.

**Parameters:**

- `query` (string): Search query
- `site` (string): Site to search (musictheory.net, teoria.com, or all)

**Example:**

```
SearchMusicTheorySites("modes", "musictheory.net")
```

## Security Features

### Domain Whitelisting

Only allowed domains can be accessed via web scraping:

- musictheory.net
- wikipedia.org
- guitarnoise.com
- justinguitar.com
- ultimate-guitar.com
- songsterr.com
- teoria.com

To add more domains, edit `appsettings.json` or modify the `AllowedDomains` set in
`Common/GA.Business.Core.Web/Services/WebScrapingService.cs`.

### Rate Limiting

Configured in `appsettings.json`:

```json
"RateLimiting": {
  "MaxRequestsPerMinute": 60,
  "MaxRequestsPerHour": 1000
}
```

### Content Sanitization

- HTML tags are stripped
- Scripts and styles are removed
- Excessive whitespace is cleaned
- HTML entities are decoded

## Caching Strategy

### Cache Keys

- Web pages: `webpage:{url}:{extractMainContent}`
- Elements: `elements:{url}:{cssSelector}`
- Feeds: `feed:{url}:{maxItems}`
- Searches: `ddg:{query}:{maxResults}`, `wiki:{query}:{maxResults}`

### Cache Expiration

- Web pages: 2 hours
- RSS feeds: 30 minutes
- Search results: 6 hours (DuckDuckGo), 24 hours (Wikipedia)
- Wikipedia summaries: 7 days

### Cache Invalidation

```csharp
// Invalidate specific cache entry
webContentCache.Invalidate("webpage:https://example.com:true");
```

## Usage Examples

### Example 1: Research a Music Theory Topic

```
1. SearchWikipedia("Dorian mode", 3)
2. GetWikipediaSummary("Dorian mode")
3. SearchMusicTheorySites("dorian mode examples", "musictheory.net")
4. FetchWebPage("https://www.musictheory.net/lessons/22", true)
```

### Example 2: Stay Updated with Guitar Learning Content

```
1. ListKnownFeeds()
2. ReadFeed("justinguitar", 10)
3. SearchFeed("justinguitar", "beginner chords", 5)
```

### Example 3: Deep Dive into Chord Progressions

```
1. SearchDuckDuckGo("jazz chord progressions", 5)
2. FetchWebPage("https://www.musictheory.net/lessons/57", true)
3. ExtractElements("https://www.musictheory.net/lessons", ".lesson-title")
```

## Error Handling

All tools include comprehensive error handling:

- Invalid URLs return error messages
- Blocked domains return security warnings
- HTTP errors are logged and returned as user-friendly messages
- Parsing errors are caught and reported

## Performance Considerations

1. **Caching**: First request fetches from source, subsequent requests use cache
2. **Parallel Requests**: HttpClient is configured for optimal performance
3. **Memory Management**: Cache size is limited to prevent memory issues
4. **Timeouts**: HTTP requests have reasonable timeouts

## Extending the System

### Adding New Allowed Domains

Edit `Common/GA.Business.Core.Web/Services/WebScrapingService.cs`:

```csharp
private static readonly HashSet<string> AllowedDomains = new(StringComparer.OrdinalIgnoreCase)
{
    "musictheory.net",
    "your-new-domain.com"
};
```

### Adding New RSS Feeds

Edit `appsettings.json`:

```json
"RssFeeds": {
  "YourCategory": [
    "https://example.com/feed"
  ]
}
```

Then update `Common/GA.Business.Core.Web/Services/FeedReaderService.cs`:

```csharp
private static readonly Dictionary<string, string> KnownFeeds = new()
{
    ["yourcategory"] = "https://example.com/feed"
};
```

### Adding New Search Providers

Create a new method in `Common/GA.Business.Core.Web/Services/WebSearchService.cs` following the existing patterns.

## Testing

Build and run the MCP server:

```bash
cd GaMcpServer
dotnet build
dotnet run
```

The tools will be automatically discovered and available to MCP clients.

## Troubleshooting

### Issue: "Domain not allowed"

**Solution**: Add the domain to `AllowedDomains` in `WebScrapingService.cs` or `appsettings.json`

### Issue: "No results found"

**Solution**: Try different search terms or use a different search tool

### Issue: Cache not working

**Solution**: Check memory cache configuration in `Program.cs`

### Issue: HTTP timeout

**Solution**: Increase timeout in HttpClient configuration

## Future Enhancements

Potential improvements:

- [ ] Add support for more search engines (Bing, Google Custom Search)
- [ ] Implement distributed caching (Redis)
- [ ] Add content summarization using AI
- [ ] Support for PDF and document parsing
- [ ] Advanced rate limiting with token bucket algorithm
- [ ] Webhook support for real-time feed updates
- [ ] Content quality scoring
- [ ] Automatic domain discovery and validation

## License

Part of the Guitar Alchemist project.
