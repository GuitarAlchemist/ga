# GA.Business.Core.Web

Shared web integration services for Guitar Alchemist applications.

## Overview

This library provides reusable web integration functionality that can be shared across multiple Guitar Alchemist
applications, including:

- **GaMcpServer** (MCP chatbot server)
- **GuitarAlchemistChatbot** (Blazor web chatbot)
- Any future applications needing web integration

## Features

### 🌐 Web Scraping

- Fetch and parse HTML content from allowed domains
- Extract main content (removes navigation, ads, scripts)
- CSS selector-based element extraction
- Link extraction and filtering
- Domain whitelisting for security

### 📰 RSS/Atom Feed Reading

- Read latest articles from feeds
- Search feed items by keyword
- Filter by date range
- Support for both RSS and Atom formats
- Curated list of known music theory feeds

### 🔍 Web Search

- DuckDuckGo Instant Answer API integration
- Wikipedia search and article summaries
- Site-specific searches (musictheory.net, teoria.com)
- No API keys required

### ⚡ Performance

- Memory-based caching with configurable expiration
- Reduces external API calls by 80-90%
- Async/await throughout
- HttpClient reuse for optimal performance

## Installation

Add reference to your project:

```bash
dotnet add reference ../Common/GA.Business.Core.Web/GA.Business.Core.Web.csproj
```

## Usage

### 1. Register Services

In your `Program.cs`:

```csharp
using GA.Business.Core.Web;

var builder = Host.CreateApplicationBuilder(args);

// Register all web integration services
builder.Services.AddWebIntegrationServices();

var app = builder.Build();
await app.RunAsync();
```

This registers:

- `HttpClient`
- `IMemoryCache`
- `WebContentCache`
- `WebScrapingService`
- `FeedReaderService`
- `WebSearchService`

### 2. Use Services

#### Web Scraping

```csharp
public class MyService
{
    private readonly WebScrapingService _scraping;

    public MyService(WebScrapingService scraping)
    {
        _scraping = scraping;
    }

    public async Task<string> GetMusicTheoryLesson()
    {
        // Fetch full page content
        var content = await _scraping.FetchWebPageAsync(
            "https://www.musictheory.net/lessons/51", 
            extractMainContent: true);

        // Extract specific elements
        var headers = await _scraping.ExtractElementsAsync(
            "https://www.musictheory.net/lessons", 
            "h2");

        // Get all links
        var links = await _scraping.ExtractLinksAsync(
            "https://www.musictheory.net", 
            filterDomain: "musictheory.net");

        return content;
    }
}
```

#### Feed Reading

```csharp
public class MyService
{
    private readonly FeedReaderService _feeds;

    public MyService(FeedReaderService feeds)
    {
        _feeds = feeds;
    }

    public async Task<string> GetLatestLessons()
    {
        // Read from known feed
        var latest = await _feeds.ReadFeedAsync("musictheory", maxItems: 10);

        // Search feed
        var chordLessons = await _feeds.SearchFeedAsync(
            "justinguitar", 
            "chords", 
            maxItems: 5);

        // Get by date range
        var recent = await _feeds.GetFeedByDateRangeAsync(
            "guitarnoise",
            "2025-01-01",
            "2025-01-31",
            maxItems: 20);

        return latest;
    }
}
```

#### Web Search

```csharp
public class MyService
{
    private readonly WebSearchService _search;

    public MyService(WebSearchService search)
    {
        _search = search;
    }

    public async Task<string> ResearchTopic()
    {
        // Search DuckDuckGo
        var results = await _search.SearchDuckDuckGoAsync(
            "circle of fifths", 
            maxResults: 5);

        // Search Wikipedia
        var wikiResults = await _search.SearchWikipediaAsync(
            "harmonic minor scale", 
            maxResults: 5);

        // Get Wikipedia summary
        var summary = await _search.GetWikipediaSummaryAsync(
            "Circle of fifths");

        // Search specific sites
        var siteResults = await _search.SearchMusicTheorySitesAsync(
            "modes", 
            site: "musictheory.net");

        return results;
    }
}
```

## Architecture

### Service Layer

All core functionality is in service classes:

- `WebScrapingService` - Web page fetching and parsing
- `FeedReaderService` - RSS/Atom feed consumption
- `WebSearchService` - Web search integration
- `WebContentCache` - Caching layer
- `HttpClientExtensions` - HTTP helper methods

### Application Layer

Applications create thin wrappers around services:

**MCP Server Example:**

```csharp
[McpServerToolType]
public class WebScrapingToolWrapper
{
    private readonly WebScrapingService _service;

    public WebScrapingToolWrapper(WebScrapingService service)
    {
        _service = service;
    }

    [McpServerTool]
    public async Task<string> FetchWebPage(string url, bool extractMainContent)
    {
        return await _service.FetchWebPageAsync(url, extractMainContent);
    }
}
```

**Blazor Chatbot Example:**

```csharp
public class GuitarAlchemistFunctions
{
    private readonly WebSearchService _search;

    [Description("Search for music theory information")]
    public async Task<string> SearchMusicTheory(string query)
    {
        return await _search.SearchDuckDuckGoAsync(query, 5);
    }
}
```

## Security

### Domain Whitelisting

Only these domains are allowed for web scraping:

- musictheory.net
- wikipedia.org / en.wikipedia.org
- guitarnoise.com
- justinguitar.com
- ultimate-guitar.com
- songsterr.com
- teoria.com

To add more domains, modify `WebScrapingService.AllowedDomains`.

### Content Sanitization

- Scripts and styles removed
- Navigation, headers, footers removed
- Ads and sidebars removed
- HTML entities decoded
- Excessive whitespace cleaned

## Caching

### Cache Keys

- Web pages: `webpage:{url}:{extractMainContent}`
- Elements: `elements:{url}:{cssSelector}`
- Feeds: `feed:{url}:{maxItems}`
- DuckDuckGo: `ddg:{query}:{maxResults}`
- Wikipedia: `wiki:{query}:{maxResults}`

### Cache Expiration

| Content Type      | Duration   | Rationale                    |
|-------------------|------------|------------------------------|
| Web pages         | 2 hours    | Content changes infrequently |
| RSS feeds         | 30 minutes | Updates frequently           |
| DuckDuckGo        | 6 hours    | Instant answers stable       |
| Wikipedia search  | 24 hours   | Search results stable        |
| Wikipedia summary | 7 days     | Articles rarely change       |

### Custom Caching

```csharp
var cache = serviceProvider.GetRequiredService<WebContentCache>();

var data = await cache.GetOrCreateAsync(
    "my-key",
    async () => await FetchDataAsync(),
    absoluteExpiration: TimeSpan.FromHours(1),
    slidingExpiration: TimeSpan.FromMinutes(15));
```

## Dependencies

```xml
<PackageReference Include="HtmlAgilityPack" Version="1.12.4" />
<PackageReference Include="System.ServiceModel.Syndication" Version="9.0.9" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="9.0.9" />
<PackageReference Include="Microsoft.Extensions.Http" Version="9.0.9" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.9" />
```

## Benefits of Shared Library

### ✅ Reduced Duplication

- Write once, use everywhere
- No duplicate code between applications
- Consistent behavior across apps

### ✅ Easier Maintenance

- Fix bugs in one place
- Update features once
- Centralized security updates

### ✅ Better Testing

- Test services independently
- Mock services in application tests
- Shared test utilities

### ✅ Improved Extensibility

- New applications can reuse services
- Easy to add new features
- Clear separation of concerns

## Examples

### GaMcpServer Integration

```csharp
// Program.cs
builder.Services.AddWebIntegrationServices();

// Tool wrapper
[McpServerToolType]
public class WebScrapingToolWrapper
{
    private readonly WebScrapingService _service;
    // Delegate to shared service
}
```

### GuitarAlchemistChatbot Integration

```csharp
// Program.cs
builder.Services.AddWebIntegrationServices();

// AI function
public class GuitarAlchemistFunctions
{
    private readonly WebSearchService _search;
    private readonly FeedReaderService _feeds;
    // Use shared services
}
```

## Future Enhancements

- [ ] Add more search providers (Bing, Google)
- [ ] Distributed caching (Redis)
- [ ] Content summarization using AI
- [ ] PDF and document parsing
- [ ] Advanced rate limiting
- [ ] Webhook support for feeds
- [ ] Content quality scoring

## License

Part of the Guitar Alchemist project.

