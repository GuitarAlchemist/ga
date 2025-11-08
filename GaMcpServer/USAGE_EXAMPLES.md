# Web Integration Usage Examples

## Quick Start Guide

This document provides practical examples of using the web integration tools in GaMcpServer.

## Prerequisites

1. Build the project:
   ```bash
   cd GaMcpServer
   dotnet build
   ```

2. Run the MCP server:
   ```bash
   dotnet run
   ```

3. Connect your MCP client (e.g., Claude Desktop, Cline, etc.)

## Example Scenarios

### Scenario 1: Learning About Music Theory Concepts

**Goal**: Research the Circle of Fifths

```
Step 1: Search Wikipedia
> SearchWikipedia("circle of fifths", 3)

Step 2: Get detailed summary
> GetWikipediaSummary("Circle of fifths")

Step 3: Find practical examples
> SearchMusicTheorySites("circle of fifths examples", "musictheory.net")

Step 4: Read the full lesson
> FetchWebPage("https://www.musictheory.net/lessons/24", true)
```

**Expected Output**: Comprehensive information about the Circle of Fifths from multiple authoritative sources.

---

### Scenario 2: Discovering New Guitar Techniques

**Goal**: Stay updated with Justin Guitar's latest lessons

```
Step 1: List available feeds
> ListKnownFeeds()

Step 2: Read latest articles
> ReadFeed("justinguitar", 10)

Step 3: Search for specific topic
> SearchFeed("justinguitar", "fingerpicking", 5)

Step 4: Get articles from last month
> GetFeedByDateRange("justinguitar", "2025-09-01", "2025-09-30", 15)
```

**Expected Output**: Latest guitar lessons and techniques from Justin Guitar.

---

### Scenario 3: Understanding Chord Progressions

**Goal**: Learn about common chord progressions in jazz

```
Step 1: General search
> SearchDuckDuckGo("jazz chord progressions ii-V-I", 5)

Step 2: Get Wikipedia overview
> SearchWikipedia("ii-V-I progression", 3)

Step 3: Find detailed lessons
> SearchMusicTheorySites("ii-V-I progression", "all")

Step 4: Extract specific content
> FetchWebPage("https://www.musictheory.net/lessons/57", true)
```

**Expected Output**: Comprehensive understanding of jazz chord progressions.

---

### Scenario 4: Exploring Guitar Scales

**Goal**: Learn about the Dorian mode

```
Step 1: Wikipedia summary
> GetWikipediaSummary("Dorian mode")

Step 2: Search for guitar-specific content
> SearchDuckDuckGo("dorian mode guitar", 5)

Step 3: Find lessons on music theory sites
> SearchMusicTheorySites("dorian mode", "musictheory.net")

Step 4: Read detailed lesson
> FetchWebPage("https://www.musictheory.net/lessons/22", true)
```

**Expected Output**: Complete understanding of the Dorian mode with guitar applications.

---

### Scenario 5: Researching Music History

**Goal**: Learn about the history of the guitar

```
Step 1: Wikipedia search
> SearchWikipedia("classical guitar history", 5)

Step 2: Get main article
> GetWikipediaSummary("Classical guitar")

Step 3: Find related articles
> ExtractLinks("https://en.wikipedia.org/wiki/Classical_guitar", "wikipedia.org")

Step 4: Search for detailed content
> SearchDuckDuckGo("history of classical guitar", 5)
```

**Expected Output**: Historical context and evolution of the classical guitar.

---

### Scenario 6: Finding Chord Voicings

**Goal**: Discover jazz chord voicings

```
Step 1: Search for resources
> SearchDuckDuckGo("jazz guitar chord voicings", 5)

Step 2: Check music theory sites
> SearchMusicTheorySites("chord voicings", "all")

Step 3: Read latest articles
> ReadFeed("guitarnoise", 10)

Step 4: Search feed for voicings
> SearchFeed("guitarnoise", "voicing", 5)
```

**Expected Output**: Various resources on jazz chord voicings.

---

### Scenario 7: Understanding Music Notation

**Goal**: Learn about time signatures

```
Step 1: Wikipedia overview
> GetWikipediaSummary("Time signature")

Step 2: Find lessons
> SearchMusicTheorySites("time signature", "musictheory.net")

Step 3: Get detailed content
> FetchWebPage("https://www.musictheory.net/lessons/12", true)

Step 4: Extract specific elements
> ExtractElements("https://www.musictheory.net/lessons/12", ".example")
```

**Expected Output**: Complete understanding of time signatures with examples.

---

### Scenario 8: Discovering New Music Theory Content

**Goal**: Monitor multiple sources for new content

```
Step 1: Check all known feeds
> ListKnownFeeds()

Step 2: Read from multiple sources
> ReadFeed("musictheory", 5)
> ReadFeed("teoria", 5)
> ReadFeed("guitarnoise", 5)

Step 3: Search for specific topics across feeds
> SearchFeed("musictheory", "harmony", 3)
> SearchFeed("teoria", "harmony", 3)
```

**Expected Output**: Latest content from multiple music theory sources.

---

### Scenario 9: Deep Dive into Harmonic Analysis

**Goal**: Understand harmonic analysis techniques

```
Step 1: Wikipedia foundation
> SearchWikipedia("harmonic analysis music", 3)
> GetWikipediaSummary("Harmonic analysis")

Step 2: Find academic resources
> SearchDuckDuckGo("harmonic analysis music theory", 5)

Step 3: Get specific lessons
> SearchMusicTheorySites("harmonic analysis", "all")

Step 4: Extract detailed content
> FetchWebPage("https://www.musictheory.net/lessons/51", true)
```

**Expected Output**: Comprehensive understanding of harmonic analysis.

---

### Scenario 10: Learning About Guitar Effects

**Goal**: Research guitar effects and pedals

```
Step 1: General search
> SearchDuckDuckGo("guitar effects pedals guide", 5)

Step 2: Check guitar learning sites
> ReadFeed("guitarnoise", 10)
> SearchFeed("guitarnoise", "effects", 5)

Step 3: Find specific content
> SearchDuckDuckGo("delay pedal settings", 5)
```

**Expected Output**: Information about guitar effects and how to use them.

---

## Advanced Usage Patterns

### Pattern 1: Multi-Source Research

Combine multiple tools for comprehensive research:

```
1. SearchWikipedia() - Get overview
2. GetWikipediaSummary() - Get details
3. SearchMusicTheorySites() - Find lessons
4. FetchWebPage() - Read full content
5. ReadFeed() - Stay updated
```

### Pattern 2: Content Discovery

Find new content across multiple sources:

```
1. ListKnownFeeds() - See available sources
2. ReadFeed() for each source - Get latest
3. SearchFeed() - Find specific topics
4. GetFeedByDateRange() - Historical content
```

### Pattern 3: Deep Learning

Thoroughly understand a topic:

```
1. SearchDuckDuckGo() - Find resources
2. SearchWikipedia() - Get foundation
3. GetWikipediaSummary() - Read details
4. SearchMusicTheorySites() - Find lessons
5. FetchWebPage() - Read full lessons
6. ExtractElements() - Get specific parts
```

## Tips and Best Practices

### 1. Start Broad, Then Narrow

- Begin with general searches (SearchWikipedia, SearchDuckDuckGo)
- Then move to specific sites (SearchMusicTheorySites)
- Finally, read full content (FetchWebPage)

### 2. Use Caching Effectively

- Repeated queries are cached automatically
- First query may be slower, subsequent ones are instant
- Cache expires after configured time (see README)

### 3. Combine Tools

- Use search tools to find URLs
- Use FetchWebPage to read full content
- Use ExtractElements for specific parts

### 4. Monitor Multiple Sources

- Use ReadFeed for multiple sources
- Use SearchFeed to find specific topics
- Use GetFeedByDateRange for historical content

### 5. Respect Rate Limits

- Tools have built-in rate limiting
- Caching reduces external requests
- Don't make excessive requests

## Common Use Cases

### For Students

- Research music theory concepts
- Find learning resources
- Stay updated with lessons
- Understand musical notation

### For Teachers

- Find teaching materials
- Discover new pedagogical approaches
- Stay current with music education
- Find examples and exercises

### For Musicians

- Learn new techniques
- Understand theory behind music
- Discover chord progressions
- Research scales and modes

### For Developers

- Integrate music theory knowledge
- Build educational applications
- Create music analysis tools
- Develop practice aids

## Troubleshooting Common Issues

### Issue: No results found

**Try:**

- Different search terms
- Alternative search tools
- Broader queries
- Check spelling

### Issue: Content not loading

**Try:**

- Check if domain is allowed
- Verify URL is correct
- Try again (might be temporary)
- Check internet connection

### Issue: Too much content

**Try:**

- Reduce maxResults parameter
- Use more specific queries
- Use ExtractElements for specific parts
- Filter by date range

### Issue: Outdated content

**Try:**

- Clear cache (restart server)
- Use GetFeedByDateRange for recent content
- Check feed directly
- Try different source

## Next Steps

1. **Explore**: Try different combinations of tools
2. **Experiment**: Test with various queries
3. **Integrate**: Use in your applications
4. **Extend**: Add new sources and tools
5. **Share**: Contribute improvements back

## Support

For issues or questions:

1. Check README_WEB_INTEGRATION.md
2. Review this usage guide
3. Check tool descriptions
4. Review source code
5. Open an issue on GitHub

## Contributing

To add new examples:

1. Test the scenario thoroughly
2. Document expected outputs
3. Include error cases
4. Submit a pull request

---

**Happy Learning! 🎸🎵**

