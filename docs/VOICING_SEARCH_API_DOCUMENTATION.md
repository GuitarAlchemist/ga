# Voicing Search API Documentation

## Overview

The Voicing Search API provides semantic search capabilities for guitar voicings using GPU-accelerated vector similarity search.

**Base URL**: `http://localhost:5232`

---

## Endpoints

### 1. Search Voicings

Search for voicings using natural language queries.

**Endpoint**: `GET /api/voicings/search`

**Parameters**:
| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `q` | string | Yes | Search query | "easy jazz chord" |
| `limit` | integer | No | Max results (default: 10) | 5 |
| `difficulty` | string | No | Filter by difficulty | "Beginner", "Intermediate", "Advanced" |
| `position` | string | No | Filter by position | "Open", "Closed", "Drop2", "Drop3" |

**Example Requests**:

```bash
# Basic search
curl "http://localhost:5232/api/voicings/search?q=easy+jazz+chord&limit=5"

# Search with difficulty filter
curl "http://localhost:5232/api/voicings/search?q=major+chord&difficulty=Beginner&limit=10"

# Search with position filter
curl "http://localhost:5232/api/voicings/search?q=chord&position=Open&limit=5"

# Complex query
curl "http://localhost:5232/api/voicings/search?q=seventh+chord&difficulty=Intermediate&position=Drop2&limit=5"
```

**Example Response**:

```json
{
  "results": [
    {
      "document": {
        "id": "voicing_12345",
        "searchableText": "diagram: Voicing { Positions = [...], Notes = [...] }",
        "metadata": {
          "noteCount": 3,
          "difficulty": "Beginner",
          "position": "Open",
          "tags": ["major", "triad", "open-position"]
        }
      },
      "score": 0.8542
    },
    {
      "document": {
        "id": "voicing_67890",
        "searchableText": "diagram: Voicing { Positions = [...], Notes = [...] }",
        "metadata": {
          "noteCount": 4,
          "difficulty": "Intermediate",
          "position": "Closed",
          "tags": ["seventh", "jazz", "closed-position"]
        }
      },
      "score": 0.7891
    }
  ],
  "totalResults": 2,
  "query": "easy jazz chord",
  "limit": 5
}
```

---

### 2. Find Similar Voicings

Find voicings similar to a specific voicing.

**Endpoint**: `GET /api/voicings/similar/{id}`

**Parameters**:
| Parameter | Type | Required | Description | Example |
|-----------|------|----------|-------------|---------|
| `id` | string | Yes | Voicing ID | "voicing_12345" |
| `limit` | integer | No | Max results (default: 10) | 5 |

**Example Requests**:

```bash
# Find similar voicings
curl "http://localhost:5232/api/voicings/similar/voicing_12345?limit=5"
```

**Example Response**:

```json
{
  "results": [
    {
      "document": {
        "id": "voicing_67890",
        "searchableText": "diagram: Voicing { Positions = [...], Notes = [...] }",
        "metadata": {
          "noteCount": 3,
          "difficulty": "Beginner",
          "position": "Open"
        }
      },
      "score": 0.9234
    }
  ],
  "totalResults": 1,
  "sourceVoicingId": "voicing_12345",
  "limit": 5
}
```

**Error Response** (404):

```json
{
  "error": "Voicing with ID 'voicing_12345' not found"
}
```

---

### 3. Get Search Statistics

Get statistics about the voicing search index.

**Endpoint**: `GET /api/voicings/stats`

**Parameters**: None

**Example Request**:

```bash
curl "http://localhost:5232/api/voicings/stats"
```

**Example Response**:

```json
{
  "totalVoicings": 1000,
  "memoryUsageMB": 85.4,
  "averageSearchTimeMs": 8.86,
  "totalSearches": 42,
  "strategyName": "ILGPU",
  "gpuInfo": {
    "acceleratorType": "Cuda",
    "deviceName": "NVIDIA GeForce RTX 3070",
    "memorySize": "8GB"
  }
}
```

---

### 4. Health Check

Check if the voicing search service is initialized and ready.

**Endpoint**: `GET /api/voicings/health`

**Parameters**: None

**Example Request**:

```bash
curl "http://localhost:5232/api/voicings/health"
```

**Example Response** (Initialized):

```json
{
  "status": "healthy",
  "initialized": true,
  "voicingCount": 1000,
  "message": "Voicing search service is ready"
}
```

**Example Response** (Not Initialized):

```json
{
  "status": "initializing",
  "initialized": false,
  "voicingCount": 0,
  "message": "Voicing search service is still initializing"
}
```

---

## Code Examples

### C# / .NET

```csharp
using System.Net.Http;
using System.Text.Json;

public class VoicingSearchClient
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "http://localhost:5232";
    
    public VoicingSearchClient()
    {
        _httpClient = new HttpClient { BaseAddress = new Uri(BaseUrl) };
    }
    
    public async Task<VoicingSearchResponse> SearchAsync(
        string query,
        int limit = 10,
        string? difficulty = null,
        string? position = null)
    {
        var url = $"/api/voicings/search?q={Uri.EscapeDataString(query)}&limit={limit}";
        if (difficulty != null) url += $"&difficulty={difficulty}";
        if (position != null) url += $"&position={position}";
        
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<VoicingSearchResponse>(json)!;
    }
    
    public async Task<VoicingSearchStats> GetStatsAsync()
    {
        var response = await _httpClient.GetAsync("/api/voicings/stats");
        response.EnsureSuccessStatusCode();
        
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<VoicingSearchStats>(json)!;
    }
}

// Usage
var client = new VoicingSearchClient();
var results = await client.SearchAsync("easy jazz chord", limit: 5);
Console.WriteLine($"Found {results.TotalResults} voicings");

var stats = await client.GetStatsAsync();
Console.WriteLine($"Index contains {stats.TotalVoicings} voicings");
```

### JavaScript / TypeScript

```typescript
class VoicingSearchClient {
  private baseUrl = 'http://localhost:5232';
  
  async search(
    query: string,
    limit: number = 10,
    difficulty?: string,
    position?: string
  ): Promise<VoicingSearchResponse> {
    const params = new URLSearchParams({
      q: query,
      limit: limit.toString()
    });
    
    if (difficulty) params.append('difficulty', difficulty);
    if (position) params.append('position', position);
    
    const response = await fetch(
      `${this.baseUrl}/api/voicings/search?${params}`
    );
    
    if (!response.ok) {
      throw new Error(`Search failed: ${response.statusText}`);
    }
    
    return await response.json();
  }
  
  async getStats(): Promise<VoicingSearchStats> {
    const response = await fetch(`${this.baseUrl}/api/voicings/stats`);
    
    if (!response.ok) {
      throw new Error(`Failed to get stats: ${response.statusText}`);
    }
    
    return await response.json();
  }
}

// Usage
const client = new VoicingSearchClient();
const results = await client.search('easy jazz chord', 5);
console.log(`Found ${results.totalResults} voicings`);

const stats = await client.getStats();
console.log(`Index contains ${stats.totalVoicings} voicings`);
```

### Python

```python
import requests
from typing import Optional, Dict, Any

class VoicingSearchClient:
    def __init__(self, base_url: str = "http://localhost:5232"):
        self.base_url = base_url
    
    def search(
        self,
        query: str,
        limit: int = 10,
        difficulty: Optional[str] = None,
        position: Optional[str] = None
    ) -> Dict[str, Any]:
        params = {
            'q': query,
            'limit': limit
        }
        
        if difficulty:
            params['difficulty'] = difficulty
        if position:
            params['position'] = position
        
        response = requests.get(
            f"{self.base_url}/api/voicings/search",
            params=params
        )
        response.raise_for_status()
        return response.json()
    
    def get_stats(self) -> Dict[str, Any]:
        response = requests.get(f"{self.base_url}/api/voicings/stats")
        response.raise_for_status()
        return response.json()

# Usage
client = VoicingSearchClient()
results = client.search("easy jazz chord", limit=5)
print(f"Found {results['totalResults']} voicings")

stats = client.get_stats()
print(f"Index contains {stats['totalVoicings']} voicings")
```

---

## Performance Characteristics

### Search Performance

| Operation | Average Time | Throughput |
|-----------|--------------|------------|
| Semantic Search | ~8.86ms | ~113 searches/sec |
| Similar Voicings | ~8.86ms | ~113 searches/sec |
| Statistics | <1ms | >1000 requests/sec |
| Health Check | <1ms | >1000 requests/sec |

### Initialization

| Phase | Time | Details |
|-------|------|---------|
| Voicing Generation | 0.6s | 667,125 voicings |
| Indexing | 3.5s | 1,000 voicings (parallel) |
| Embedding Generation | 24.8s | 40.3 embeddings/sec |
| **Total** | **30.2s** | Complete initialization |

---

## Error Handling

### Common Error Codes

| Status Code | Description | Example |
|-------------|-------------|---------|
| 200 | Success | Request completed successfully |
| 400 | Bad Request | Missing required parameter `q` |
| 404 | Not Found | Voicing ID not found |
| 500 | Internal Server Error | GPU initialization failed |
| 503 | Service Unavailable | Index not initialized yet |

### Error Response Format

```json
{
  "error": "Error message describing what went wrong",
  "details": "Additional details (optional)"
}
```

---

## Best Practices

### 1. Query Optimization

- **Use specific queries**: "Cmaj7 drop2 voicing" is better than "chord"
- **Combine filters**: Use difficulty and position filters to narrow results
- **Limit results**: Request only what you need (default: 10)

### 2. Caching

- **Client-side caching**: Cache frequently used queries
- **Batch requests**: Group multiple searches when possible
- **Use similar voicings**: Leverage the similarity endpoint for related searches

### 3. Error Handling

- **Retry on 503**: Service may still be initializing
- **Handle 404**: Voicing IDs may change between index rebuilds
- **Log errors**: Track failures for debugging

---

## Configuration

See `appsettings.json` for configuration options:

```json
{
  "VoicingSearch": {
    "MaxVoicingsToIndex": 1000,
    "MinPlayedNotes": 2,
    "NoteCountFilter": "ThreeNotes",
    "EnableIndexing": true,
    "LazyLoading": false
  }
}
```

---

## Support

For issues or questions:
- Check the logs in the API server console
- Review the implementation summary: `docs/VOICING_SEARCH_COMPLETE_SUMMARY.md`
- See GPU kernel plan: `docs/GPU_KERNEL_IMPLEMENTATION_PLAN.md`
- See optimization plan: `docs/CACHING_AND_OPTIMIZATION_PLAN.md`

