# Caching and Optimization Implementation Plan

## Overview

This document outlines the plan to implement caching and incremental indexing features for the voicing search system.

---

## Feature 1: Query Caching

### Goal
Cache frequently searched queries to avoid redundant embedding generation and similarity calculations.

### Implementation

#### Step 1: Add LRU Cache for Query Results

```csharp
// Add to VoicingSearchServiceExtensions.cs or create new CachingVoicingSearchService.cs

using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

public class CachingVoicingSearchService : IVoicingSearchService
{
    private readonly IVoicingSearchService _innerService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachingVoicingSearchService> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
    
    public CachingVoicingSearchService(
        IVoicingSearchService innerService,
        IMemoryCache cache,
        ILogger<CachingVoicingSearchService> logger)
    {
        _innerService = innerService;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<List<VoicingSearchResult>> SearchAsync(
        string query,
        int limit = 10,
        VoicingDifficulty? difficulty = null,
        VoicingPosition? position = null)
    {
        // Create cache key from query parameters
        var cacheKey = $"search:{query}:{limit}:{difficulty}:{position}";
        
        // Try to get from cache
        if (_cache.TryGetValue<List<VoicingSearchResult>>(cacheKey, out var cachedResults))
        {
            _logger.LogDebug("Cache hit for query: {Query}", query);
            return cachedResults!;
        }
        
        // Cache miss - execute search
        _logger.LogDebug("Cache miss for query: {Query}", query);
        var results = await _innerService.SearchAsync(query, limit, difficulty, position);
        
        // Store in cache
        _cache.Set(cacheKey, results, _cacheExpiration);
        
        return results;
    }
    
    public async Task<List<VoicingSearchResult>> FindSimilarAsync(string voicingId, int limit = 10)
    {
        var cacheKey = $"similar:{voicingId}:{limit}";
        
        if (_cache.TryGetValue<List<VoicingSearchResult>>(cacheKey, out var cachedResults))
        {
            _logger.LogDebug("Cache hit for similar voicings: {VoicingId}", voicingId);
            return cachedResults!;
        }
        
        var results = await _innerService.FindSimilarAsync(voicingId, limit);
        _cache.Set(cacheKey, results, _cacheExpiration);
        
        return results;
    }
    
    public VoicingSearchStats GetStats() => _innerService.GetStats();
}
```

#### Step 2: Register Caching Service

```csharp
// Update VoicingSearchServiceExtensions.cs

public static IServiceCollection AddVoicingSearchServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // ... existing registrations ...
    
    // Add memory cache
    services.AddMemoryCache();
    
    // Register the actual search service
    services.AddSingleton<EnhancedVoicingSearchService>();
    
    // Wrap with caching decorator
    services.AddSingleton<IVoicingSearchService>(sp =>
    {
        var innerService = sp.GetRequiredService<EnhancedVoicingSearchService>();
        var cache = sp.GetRequiredService<IMemoryCache>();
        var logger = sp.GetRequiredService<ILogger<CachingVoicingSearchService>>();
        return new CachingVoicingSearchService(innerService, cache, logger);
    });
    
    return services;
}
```

#### Step 3: Add Cache Statistics

```csharp
public class CachingVoicingSearchService : IVoicingSearchService
{
    private long _cacheHits;
    private long _cacheMisses;
    
    // ... existing code ...
    
    public (long Hits, long Misses, double HitRate) GetCacheStats()
    {
        var total = _cacheHits + _cacheMisses;
        var hitRate = total > 0 ? (double)_cacheHits / total : 0.0;
        return (_cacheHits, _cacheMisses, hitRate);
    }
}

// Add endpoint to VoicingSearchController.cs
[HttpGet("cache-stats")]
public IActionResult GetCacheStats()
{
    if (_searchService is CachingVoicingSearchService cachingService)
    {
        var stats = cachingService.GetCacheStats();
        return Ok(new
        {
            cacheHits = stats.Hits,
            cacheMisses = stats.Misses,
            hitRate = $"{stats.HitRate:P2}"
        });
    }
    return Ok(new { message = "Caching not enabled" });
}
```

---

## Feature 2: Embedding Cache

### Goal
Cache generated embeddings to avoid redundant calls to Ollama.

### Implementation

#### Step 1: Create Embedding Cache Service

```csharp
public class CachedEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _innerService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CachedEmbeddingService> _logger;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromHours(24);
    
    public CachedEmbeddingService(
        IEmbeddingService innerService,
        IMemoryCache cache,
        ILogger<CachedEmbeddingService> logger)
    {
        _innerService = innerService;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        // Use hash of text as cache key to save memory
        var cacheKey = $"embedding:{text.GetHashCode()}";
        
        if (_cache.TryGetValue<List<float>>(cacheKey, out var cachedEmbedding))
        {
            _logger.LogDebug("Embedding cache hit for text hash: {Hash}", text.GetHashCode());
            return cachedEmbedding!;
        }
        
        var embedding = await _innerService.GenerateEmbeddingAsync(text);
        _cache.Set(cacheKey, embedding, _cacheExpiration);
        
        return embedding;
    }
}
```

#### Step 2: Persistent Embedding Cache (Optional)

```csharp
// For long-term caching, use file-based cache

public class PersistentEmbeddingCache
{
    private readonly string _cacheDirectory;
    private readonly ILogger<PersistentEmbeddingCache> _logger;
    
    public PersistentEmbeddingCache(string cacheDirectory, ILogger<PersistentEmbeddingCache> logger)
    {
        _cacheDirectory = cacheDirectory;
        _logger = logger;
        Directory.CreateDirectory(_cacheDirectory);
    }
    
    public async Task<List<float>?> TryGetAsync(string text)
    {
        var hash = ComputeHash(text);
        var filePath = Path.Combine(_cacheDirectory, $"{hash}.bin");
        
        if (!File.Exists(filePath))
            return null;
        
        try
        {
            var bytes = await File.ReadAllBytesAsync(filePath);
            var floats = new List<float>(bytes.Length / sizeof(float));
            
            for (int i = 0; i < bytes.Length; i += sizeof(float))
            {
                floats.Add(BitConverter.ToSingle(bytes, i));
            }
            
            _logger.LogDebug("Loaded embedding from disk cache: {Hash}", hash);
            return floats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load embedding from cache: {Hash}", hash);
            return null;
        }
    }
    
    public async Task SetAsync(string text, List<float> embedding)
    {
        var hash = ComputeHash(text);
        var filePath = Path.Combine(_cacheDirectory, $"{hash}.bin");
        
        try
        {
            var bytes = new byte[embedding.Count * sizeof(float)];
            for (int i = 0; i < embedding.Count; i++)
            {
                BitConverter.GetBytes(embedding[i]).CopyTo(bytes, i * sizeof(float));
            }
            
            await File.WriteAllBytesAsync(filePath, bytes);
            _logger.LogDebug("Saved embedding to disk cache: {Hash}", hash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save embedding to cache: {Hash}", hash);
        }
    }
    
    private static string ComputeHash(string text)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
```

---

## Feature 3: Incremental Indexing

### Goal
Add new voicings to the index without rebuilding the entire index.

### Implementation

#### Step 1: Add Incremental Index Method

```csharp
// Add to VoicingIndexingService.cs

public async Task<int> AddVoicingsAsync(
    IEnumerable<DecomposedVoicing> newVoicings,
    CancellationToken cancellationToken = default)
{
    var addedCount = 0;
    
    foreach (var decomposedVoicing in newVoicings)
    {
        if (cancellationToken.IsCancellationRequested)
            break;
        
        try
        {
            // Check if already indexed
            var voicingId = $"voicing_{decomposedVoicing.Voicing}";
            if (_indexedDocuments.Any(d => d.Id == voicingId))
            {
                _logger.LogDebug("Voicing already indexed: {Id}", voicingId);
                continue;
            }
            
            // Analyze and create document
            var analysis = VoicingAnalyzer.AnalyzeEnhanced(decomposedVoicing);
            var document = VoicingDocument.FromAnalysis(
                decomposedVoicing.Voicing,
                analysis,
                decomposedVoicing.PrimeForm?.ToString(),
                0);
            
            _indexedDocuments.Add(document);
            addedCount++;
            
            _logger.LogDebug("Added voicing to index: {Id}", voicingId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding voicing to index");
        }
    }
    
    _logger.LogInformation("Added {Count} new voicings to index", addedCount);
    return addedCount;
}
```

#### Step 2: Add Incremental Update to Search Strategy

```csharp
// Add to ILGPUVoicingSearchStrategy.cs

public async Task AddVoicingsAsync(List<VoicingDocument> newDocuments)
{
    if (_isDisposed)
        throw new ObjectDisposedException(nameof(ILGPUVoicingSearchStrategy));
    
    // Generate embeddings for new documents
    var newEmbeddings = new List<VoicingEmbedding>();
    
    foreach (var doc in newDocuments)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync(doc.SearchableText);
        newEmbeddings.Add(new VoicingEmbedding(doc.Id, embedding));
    }
    
    // Add to existing voicings
    lock (_initLock)
    {
        foreach (var embedding in newEmbeddings)
        {
            _voicings[embedding.Id] = embedding;
        }
        
        // Rebuild GPU cache
        PrepareEmbeddings(_voicings.Values.ToList());
        if (_accelerator != null && _accelerator.AcceleratorType != AcceleratorType.CPU)
        {
            CacheEmbeddingsOnGPU();
        }
    }
    
    _logger.LogInformation("Added {Count} voicings to search index", newEmbeddings.Count);
}
```

#### Step 3: Add API Endpoint for Incremental Updates

```csharp
// Add to VoicingSearchController.cs

[HttpPost("index/add")]
public async Task<IActionResult> AddVoicings([FromBody] List<VoicingDocument> documents)
{
    try
    {
        await _searchService.AddVoicingsAsync(documents);
        return Ok(new { message = $"Added {documents.Count} voicings to index" });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error adding voicings to index");
        return StatusCode(500, new { error = ex.Message });
    }
}
```

---

## Feature 4: Configuration Options

### Add to appsettings.json

```json
{
  "VoicingSearch": {
    "MaxVoicingsToIndex": 1000,
    "MinPlayedNotes": 2,
    "NoteCountFilter": "ThreeNotes",
    "EnableIndexing": true,
    "LazyLoading": false,
    "Caching": {
      "EnableQueryCache": true,
      "QueryCacheExpirationMinutes": 30,
      "EnableEmbeddingCache": true,
      "EmbeddingCacheExpirationHours": 24,
      "EnablePersistentCache": false,
      "PersistentCacheDirectory": "./cache/embeddings"
    }
  }
}
```

### Create Configuration Class

```csharp
public class VoicingSearchCachingOptions
{
    public bool EnableQueryCache { get; set; } = true;
    public int QueryCacheExpirationMinutes { get; set; } = 30;
    public bool EnableEmbeddingCache { get; set; } = true;
    public int EmbeddingCacheExpirationHours { get; set; } = 24;
    public bool EnablePersistentCache { get; set; } = false;
    public string PersistentCacheDirectory { get; set; } = "./cache/embeddings";
}
```

---

## Performance Expectations

### Query Caching
- **Cache Hit**: ~0.1ms (memory lookup)
- **Cache Miss**: ~8.86ms (current search time)
- **Expected Hit Rate**: 30-50% for typical usage
- **Overall Improvement**: 3-5x faster average response time

### Embedding Caching
- **Cache Hit**: ~0ms (instant)
- **Cache Miss**: ~25ms (Ollama embedding generation)
- **Expected Hit Rate**: 80-90% for repeated queries
- **Overall Improvement**: 5-10x faster embedding generation

### Incremental Indexing
- **Full Rebuild**: ~30s for 1000 voicings
- **Incremental Add**: ~30ms per voicing
- **Use Case**: Add new voicings without downtime

---

## Implementation Checklist

- [ ] Query Caching
  - [ ] Implement CachingVoicingSearchService
  - [ ] Add cache statistics endpoint
  - [ ] Test cache hit/miss behavior
  - [ ] Benchmark performance improvement

- [ ] Embedding Caching
  - [ ] Implement CachedEmbeddingService
  - [ ] Add persistent cache (optional)
  - [ ] Test cache effectiveness
  - [ ] Measure embedding generation speedup

- [ ] Incremental Indexing
  - [ ] Add AddVoicingsAsync to indexing service
  - [ ] Add AddVoicingsAsync to search strategy
  - [ ] Create API endpoint
  - [ ] Test incremental updates

- [ ] Configuration
  - [ ] Add caching options to appsettings.json
  - [ ] Create configuration classes
  - [ ] Update service registration
  - [ ] Document configuration options

---

## Next Steps

1. Implement query caching (highest impact)
2. Implement embedding caching
3. Add incremental indexing
4. Create comprehensive tests
5. Document performance improvements

