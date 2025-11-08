# Cache System Documentation

## Overview

The Guitar Alchemist API implements a comprehensive caching strategy with multiple layers:

- **Regular Cache**: For general-purpose data with 15-minute TTL
- **Semantic Cache**: For AI/embedding-related data with 5-minute TTL
- **Cache Metrics**: Real-time monitoring and performance tracking
- **Cache Invalidation**: Manual, pattern-based, and tag-based invalidation
- **Cache Warming**: Automatic preloading of frequently accessed data on startup

## Cache Types

### 1. Regular Cache

**Purpose**: General-purpose caching for frequently accessed data

**Configuration**:

- **TTL (Time To Live)**: 15 minutes
- **Size Limit**: 1000 items
- **Eviction Policy**: LRU (Least Recently Used)

**Use Cases**:

- Pitch class sets
- Scale definitions
- Chord templates
- Music theory data
- Fretboard positions

**Example Usage**:

```csharp
var data = await cachingService.GetOrCreateRegularAsync(
    "pitchclasssets:all",
    async () => await LoadPitchClassSetsFromDatabase());
```

### 2. Semantic Cache

**Purpose**: Caching for AI embeddings and semantic search results

**Configuration**:

- **TTL (Time To Live)**: 5 minutes
- **Size Limit**: 100 items
- **Eviction Policy**: LRU (Least Recently Used)

**Use Cases**:

- Vector embeddings
- Semantic search results
- AI-generated content
- Similarity calculations

**Example Usage**:

```csharp
var embedding = await cachingService.GetOrCreateSemanticAsync(
    $"embedding:{query}",
    async () => await GenerateEmbedding(query));
```

## Cache Keys

All cache keys are centralized in `CacheKeys.cs` to eliminate magic strings and ensure consistency.

### Key Patterns

| Pattern            | Description          | Example               |
|--------------------|----------------------|-----------------------|
| `pitchclasssets:*` | Pitch class set data | `pitchclasssets:all`  |
| `scale:*`          | Scale definitions    | `scale:Major`         |
| `chord:*`          | Chord templates      | `chord:Major7`        |
| `fretboard:*`      | Fretboard positions  | `fretboard:C:Major`   |
| `embedding:*`      | Vector embeddings    | `embedding:query123`  |
| `search:*`         | Search results       | `search:chords:Cmaj7` |

### Key Naming Conventions

1. **Use lowercase** for consistency
2. **Use colons** (`:`) as separators
3. **Include entity type** as prefix
4. **Include identifiers** as suffix
5. **Use descriptive names** for clarity

**Good Examples**:

```
pitchclasssets:all
scale:Major
chord:Dominant7
fretboard:C:Major:standard
```

**Bad Examples**:

```
pcs_all          // Unclear abbreviation
Scale-Major      // Inconsistent separator
CHORD_DOM7       // Uppercase
data123          // Non-descriptive
```

## Cache Metrics

### Available Metrics

The cache metrics service tracks the following for each cache type:

- **Total Hits**: Number of successful cache retrievals
- **Total Misses**: Number of cache misses (data not in cache)
- **Hit Rate**: Percentage of hits vs total requests
- **Total Requests**: Sum of hits and misses
- **Operation Durations**: Min, max, and average duration for Get and GetOrCreate operations
- **First/Last Request Time**: Timestamps for monitoring

### API Endpoints

#### Get All Metrics

```http
GET /api/cachemetrics
```

Returns metrics for all cache types.

#### Get Metrics for Specific Cache Type

```http
GET /api/cachemetrics/{cacheType}
```

Example: `GET /api/cachemetrics/Regular`

#### Get Summary Statistics

```http
GET /api/cachemetrics/summary
```

Returns aggregated statistics across all cache types.

#### Reset Metrics

```http
POST /api/cachemetrics/reset
```

Resets all cache metrics to zero.

### Monitoring Best Practices

1. **Monitor hit rates**: Aim for >80% hit rate for regular cache
2. **Track operation durations**: Identify slow cache operations
3. **Review cache misses**: Optimize cache warming for frequently missed keys
4. **Set up alerts**: Alert on low hit rates or high operation durations

## Cache Invalidation

### Invalidation Strategies

#### 1. Manual Invalidation

Invalidate a specific cache entry:

```http
DELETE /api/cachemetrics/invalidate/{key}
```

Example: `DELETE /api/cachemetrics/invalidate/scale:Major`

#### 2. Pattern-Based Invalidation

Invalidate all entries matching a pattern (supports `*` wildcard):

```http
DELETE /api/cachemetrics/invalidate/pattern/{pattern}
```

Examples:

- `DELETE /api/cachemetrics/invalidate/pattern/scale:*` - Invalidate all scales
- `DELETE /api/cachemetrics/invalidate/pattern/chord:*` - Invalidate all chords
- `DELETE /api/cachemetrics/invalidate/pattern/*` - Invalidate everything

#### 3. Tag-Based Invalidation

Invalidate all entries with a specific tag:

```http
DELETE /api/cachemetrics/invalidate/tag/{tag}
```

Example: `DELETE /api/cachemetrics/invalidate/tag/music-theory`

**Registering Tags**:

```csharp
invalidationService.RegisterEntry("scale:Major", "music-theory", "scales");
invalidationService.RegisterEntry("chord:Major7", "music-theory", "chords");

// Later, invalidate all music theory data
invalidationService.InvalidateByTag("music-theory");
```

#### 4. Cascading Invalidation

Register dependencies between cache entries:

```csharp
// When fretboard data depends on scale data
invalidationService.RegisterDependency(
    key: "fretboard:C:Major",
    dependsOn: "scale:Major");

// Invalidating scale:Major will also invalidate fretboard:C:Major
invalidationService.Invalidate("scale:Major");
```

#### 5. Bulk Invalidation

Invalidate all cache entries:

```http
DELETE /api/cachemetrics/invalidate/all
```

**⚠️ Warning**: Use with caution - this clears the entire cache!

### When to Invalidate

| Scenario             | Strategy      | Example                        |
|----------------------|---------------|--------------------------------|
| Data updated         | Manual        | User updates scale definition  |
| Related data changed | Cascading     | Scale change affects fretboard |
| Category updated     | Tag-based     | All chord templates updated    |
| Database migration   | Pattern-based | All `chord:*` entries          |
| System maintenance   | Bulk          | Clear all caches               |

## Cache Warming

### Automatic Warming

The `CacheWarmingService` runs on application startup and preloads frequently accessed data:

1. **Pitch Class Sets**: All 4096 pitch class sets
2. **Common Scales**: Major, Minor, Dorian, Phrygian, Lydian, Mixolydian, Locrian
3. **Common Chords**: Major, Minor, Diminished, Augmented, Major7, Minor7, Dominant7

### Warming Strategy

- **Delay**: 5 seconds after startup (allows app to fully initialize)
- **Cancellation**: Supports graceful shutdown
- **Error Handling**: Logs warnings but doesn't fail startup
- **Background**: Runs as a hosted service

### Custom Warming

To add custom cache warming logic, modify `CacheWarmingService.cs`:

```csharp
private async Task WarmCustomData(CancellationToken cancellationToken)
{
    logger.LogDebug("Warming custom data cache...");
    
    foreach (var item in customItems)
    {
        if (cancellationToken.IsCancellationRequested)
            break;
            
        await cachingService.GetOrCreateRegularAsync(
            $"custom:{item.Id}",
            async () => await LoadCustomData(item.Id));
    }
    
    logger.LogDebug("Custom data cache warmed");
}
```

## Best Practices

### 1. Cache Key Design

- ✅ Use centralized `CacheKeys` class
- ✅ Follow naming conventions
- ✅ Include entity type and identifiers
- ❌ Avoid magic strings
- ❌ Don't use user-specific data in keys (unless intentional)

### 2. TTL Selection

| Data Type        | Recommended TTL | Rationale             |
|------------------|-----------------|-----------------------|
| Static data      | 1 hour - 1 day  | Rarely changes        |
| User preferences | 15-30 minutes   | Changes occasionally  |
| Search results   | 5-15 minutes    | May become stale      |
| AI embeddings    | 5-10 minutes    | Expensive to generate |
| Real-time data   | 1-5 minutes     | Frequently updated    |

### 3. Cache Size Limits

- **Regular Cache**: 1000 items (adjust based on memory)
- **Semantic Cache**: 100 items (embeddings are large)
- Monitor memory usage and adjust limits accordingly

### 4. Error Handling

Always handle cache failures gracefully:

```csharp
try
{
    return await cachingService.GetOrCreateRegularAsync(key, factory);
}
catch (Exception ex)
{
    logger.LogWarning(ex, "Cache operation failed, falling back to direct data access");
    return await factory();
}
```

### 5. Monitoring

- Set up dashboards for cache metrics
- Alert on hit rates below 70%
- Review cache performance weekly
- Adjust TTLs and size limits based on metrics

## Troubleshooting

### Low Hit Rate

**Symptoms**: Hit rate below 70%

**Possible Causes**:

1. TTL too short
2. Cache size limit too small
3. Keys not consistent
4. Data changes frequently

**Solutions**:

- Increase TTL for stable data
- Increase cache size limit
- Review key generation logic
- Consider different caching strategy

### High Memory Usage

**Symptoms**: Application memory usage increasing

**Possible Causes**:

1. Cache size limits too high
2. Large objects being cached
3. Memory leaks in cached objects

**Solutions**:

- Reduce cache size limits
- Cache smaller objects or references
- Review object disposal
- Enable cache eviction

### Stale Data

**Symptoms**: Users seeing outdated information

**Possible Causes**:

1. TTL too long
2. Missing invalidation on updates
3. Cascading invalidation not configured

**Solutions**:

- Reduce TTL
- Add invalidation on data updates
- Configure cascading dependencies
- Use tag-based invalidation

## Configuration

### appsettings.json

```json
{
  "Caching": {
    "Regular": {
      "ExpirationMinutes": 15,
      "SizeLimit": 1000
    },
    "Semantic": {
      "ExpirationMinutes": 5,
      "SizeLimit": 100
    }
  }
}
```

### Environment Variables

```bash
Caching__Regular__ExpirationMinutes=15
Caching__Regular__SizeLimit=1000
Caching__Semantic__ExpirationMinutes=5
Caching__Semantic__SizeLimit=100
```

## Future Enhancements

- [ ] Distributed caching with Redis
- [ ] Cache compression for large objects
- [ ] Automatic cache warming based on usage patterns
- [ ] Cache versioning for schema changes
- [ ] Cache preloading from database on startup
- [ ] Cache statistics export to monitoring systems

