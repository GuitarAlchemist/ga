# Redis for AI Integration Plan

## Overview

Redis for AI provides vector similarity search, real-time caching, and semantic search capabilities that are perfect for Guitar Alchemist's AI-powered features.

**Current Status**: Redis is already in the stack (used for basic caching)  
**Opportunity**: Upgrade to Redis Stack with vector search capabilities

## Why Redis for AI?

### Current Challenges
1. **Shape Graph Search**: Finding similar fretboard shapes requires expensive graph traversal
2. **Chord Recommendations**: Real-time "next chord" suggestions need fast similarity search
3. **Pattern Recognition**: Discovering common shapes across 4096 pitch-class sets is slow
4. **Heat Map Generation**: Computing probabilities for 6×24 fretboard grid takes ~50ms
5. **Semantic Search**: Finding shapes by description ("easy box shape in C major") requires complex queries

### Redis for AI Solutions
1. **Vector Similarity Search**: Store ICV embeddings, find similar chords in < 1ms
2. **Real-time Caching**: Cache Markov walk results, heat maps, practice paths
3. **Semantic Caching**: Cache LLM responses for music theory questions
4. **Session Management**: Track user preferences, learning progress
5. **Fast Indexing**: Index shapes by multiple dimensions (diagness, ergonomics, span)

## Architecture

### Current Stack
```
MongoDB (persistent storage)
  ↓
Redis (basic caching)
  ↓
GaApi (REST API)
  ↓
React Frontend
```

### Enhanced Stack with Redis for AI
```
MongoDB (persistent storage)
  ↓
Redis Stack (vector DB + cache + search)
  ├─ Vector Search (ICV embeddings, shape embeddings)
  ├─ Full-Text Search (chord names, scale names)
  ├─ JSON Documents (shape metadata, user sessions)
  └─ Time Series (practice history, usage metrics)
  ↓
GaApi (REST API)
  ↓
React Frontend
```

## Use Cases

### 1. Vector Similarity Search for Chords

**Problem**: Finding harmonically similar chords is slow (requires computing deltas for all 4096 sets)

**Solution**: Store ICV as 6-dimensional vectors in Redis

```csharp
// Store ICV as vector
await redis.FT.CreateAsync("idx:icv", new FTCreateParams()
    .AddVectorField("icv_vector", VectorField.VectorAlgo.FLAT, new Dictionary<string, object>
    {
        ["TYPE"] = "FLOAT32",
        ["DIM"] = 6,
        ["DISTANCE_METRIC"] = "L2"
    })
);

// Index all pitch-class sets
foreach (var pcs in PitchClassSet.Items)
{
    var icv = pcs.IntervalClassVector;
    var vector = new float[] { icv.Ic1, icv.Ic2, icv.Ic3, icv.Ic4, icv.Ic5, icv.Ic6 };
    
    await redis.JSON.SetAsync($"pcs:{pcs.Id}", "$", new
    {
        id = pcs.Id,
        name = pcs.ToString(),
        icv_vector = vector,
        cardinality = pcs.Cardinality
    });
}

// Find similar chords (< 1ms)
var query = "*=>[KNN 10 @icv_vector $vec AS score]";
var results = await redis.FT.SearchAsync("idx:icv", query, new FTSearchParams()
    .AddParam("vec", cMajorVector)
    .Dialect(2)
);
```

**Performance**: 4096 sets searched in < 1ms (vs ~50ms with current approach)

### 2. Fretboard Shape Embeddings

**Problem**: Finding similar fretboard shapes requires graph traversal

**Solution**: Create multi-dimensional embeddings combining ICV + physical properties

```csharp
// 10-dimensional embedding: [ic1, ic2, ic3, ic4, ic5, ic6, diagness, ergonomics, span, minFret]
var shapeEmbedding = new float[]
{
    shape.ICV.Ic1, shape.ICV.Ic2, shape.ICV.Ic3,
    shape.ICV.Ic4, shape.ICV.Ic5, shape.ICV.Ic6,
    (float)shape.Diagness,
    (float)shape.Ergonomics,
    shape.Span / 12f,  // Normalize
    shape.MinFret / 24f  // Normalize
};

// Store shape with embedding
await redis.JSON.SetAsync($"shape:{shape.Id}", "$", new
{
    id = shape.Id,
    tuning = shape.TuningId,
    pcs = shape.PitchClassSet.Id,
    embedding = shapeEmbedding,
    positions = shape.Positions,
    tags = shape.Tags
});

// Find similar shapes
var similarShapes = await redis.FT.SearchAsync("idx:shapes", 
    "*=>[KNN 20 @embedding $vec AS score]",
    new FTSearchParams()
        .AddParam("vec", currentShapeEmbedding)
        .Filter("@diagness", 0, 0.5)  // Only box shapes
        .Filter("@span", 0, 5)  // Max 5-fret span
);
```

### 3. Real-Time Heat Map Caching

**Problem**: Heat map generation takes ~50ms, too slow for real-time UI

**Solution**: Cache heat maps with TTL, invalidate on user preference changes

```csharp
// Cache heat map
var cacheKey = $"heatmap:{currentShape.Id}:{options.GetHashCode()}";
var cached = await redis.StringGetAsync(cacheKey);

if (cached.HasValue)
{
    return JsonSerializer.Deserialize<double[,]>(cached);
}

// Generate and cache
var heatMap = walker.GenerateHeatMap(graph, currentShape, options);
await redis.StringSetAsync(cacheKey, JsonSerializer.Serialize(heatMap), TimeSpan.FromMinutes(5));
return heatMap;
```

**Performance**: < 1ms for cached results (vs ~50ms for computation)

### 4. Semantic Search for Shapes

**Problem**: Users want to search "easy C major box shape near 5th fret"

**Solution**: Combine vector search + full-text search + filters

```csharp
// Index shapes with full-text and vector fields
await redis.FT.CreateAsync("idx:shapes_semantic", new FTCreateParams()
    .AddTextField("description")
    .AddTagField("tags")
    .AddNumericField("ergonomics")
    .AddNumericField("span")
    .AddNumericField("min_fret")
    .AddVectorField("embedding", VectorField.VectorAlgo.HNSW, new Dictionary<string, object>
    {
        ["TYPE"] = "FLOAT32",
        ["DIM"] = 10,
        ["DISTANCE_METRIC"] = "COSINE",
        ["M"] = 16,
        ["EF_CONSTRUCTION"] = 200
    })
);

// Search: "easy C major box shape near 5th fret"
var results = await redis.FT.SearchAsync("idx:shapes_semantic",
    "@description:(easy C major box) @min_fret:[3 7]",
    new FTSearchParams()
        .Filter("@ergonomics", 0.7, 1.0)
        .Filter("@span", 0, 3)
        .SortBy("ergonomics", true)
        .Limit(0, 10)
);
```

### 5. Practice Path Personalization

**Problem**: Generic practice paths don't adapt to user skill level

**Solution**: Store user session data, track progress, personalize recommendations

```csharp
// Store user session
await redis.JSON.SetAsync($"session:{userId}", "$", new
{
    user_id = userId,
    skill_level = 0.5,  // 0-1
    preferred_diagness = 0.3,  // Prefers box shapes
    max_comfortable_span = 4,
    practice_history = new[]
    {
        new { shape_id = "abc123", timestamp = DateTime.UtcNow, success_rate = 0.8 }
    },
    learned_shapes = new[] { "abc123", "def456" }
});

// Generate personalized practice path
var session = await redis.JSON.GetAsync($"session:{userId}");
var personalizedOptions = new WalkOptions
{
    Steps = 20,
    Temperature = 1.0 - session.skill_level,  // More exploratory for beginners
    BoxPreference = session.preferred_diagness < 0.5,
    MaxSpan = session.max_comfortable_span
};

var practicePath = walker.GeneratePracticePath(graph, startShape, personalizedOptions);

// Update session with new practice
await redis.JSON.ArrAppendAsync($"session:{userId}", "$.practice_history", new
{
    shape_id = practicePath.Last().Id,
    timestamp = DateTime.UtcNow,
    success_rate = 0.0  // To be updated by user feedback
});
```

### 6. LLM Semantic Caching for Music Theory

**Problem**: Repeated music theory questions to LLM are expensive

**Solution**: Cache LLM responses with semantic similarity

```csharp
// Generate embedding for question
var questionEmbedding = await openAI.CreateEmbeddingAsync("What is the Grothendieck delta?");

// Search for similar cached questions
var cachedResponse = await redis.FT.SearchAsync("idx:llm_cache",
    "*=>[KNN 1 @question_embedding $vec AS score]",
    new FTSearchParams()
        .AddParam("vec", questionEmbedding)
        .Filter("@score", 0, 0.1)  // Very similar (cosine distance < 0.1)
);

if (cachedResponse.Documents.Any())
{
    return cachedResponse.Documents[0]["response"];
}

// Call LLM and cache
var response = await openAI.ChatCompletionAsync(question);
await redis.JSON.SetAsync($"llm_cache:{Guid.NewGuid()}", "$", new
{
    question = question,
    question_embedding = questionEmbedding,
    response = response,
    timestamp = DateTime.UtcNow
});
```

## Implementation Plan

### Phase 1: Infrastructure Setup (Week 1)
- [ ] Upgrade Redis to Redis Stack (docker-compose.yml)
- [ ] Add StackExchange.Redis.Search NuGet package
- [ ] Create RedisVectorService abstraction
- [ ] Add health checks for Redis Stack features

### Phase 2: Vector Indexing (Week 2)
- [ ] Index all pitch-class sets with ICV vectors
- [ ] Index fretboard shapes with multi-dimensional embeddings
- [ ] Create search indices with appropriate algorithms (FLAT vs HNSW)
- [ ] Benchmark query performance

### Phase 3: Caching Layer (Week 3)
- [ ] Implement heat map caching
- [ ] Implement practice path caching
- [ ] Add cache invalidation strategies
- [ ] Monitor cache hit rates

### Phase 4: Semantic Search (Week 4)
- [ ] Implement full-text search for shapes
- [ ] Combine vector + text + filters
- [ ] Add natural language query parsing
- [ ] Create search API endpoints

### Phase 5: Personalization (Week 5)
- [ ] Implement user session storage
- [ ] Track practice history
- [ ] Personalize recommendations
- [ ] Add feedback loop

### Phase 6: LLM Integration (Week 6)
- [ ] Add semantic caching for LLM responses
- [ ] Integrate with chatbot
- [ ] Monitor cost savings
- [ ] Optimize cache TTL

## Performance Targets

| Operation | Current | With Redis AI | Improvement |
|-----------|---------|---------------|-------------|
| Find similar chords | ~50ms | < 1ms | 50x faster |
| Find similar shapes | ~100ms | < 1ms | 100x faster |
| Heat map generation | ~50ms | < 1ms (cached) | 50x faster |
| Semantic search | N/A | < 5ms | New feature |
| LLM cache hit | N/A | < 1ms | 100x cost reduction |

## Cost Savings

- **LLM API calls**: 80-90% reduction via semantic caching
- **Compute**: 50% reduction via aggressive caching
- **Latency**: 10-100x improvement for similarity searches

## References

- [Redis for AI](https://redis.io/redis-for-ai/)
- [Redis Vector Similarity Search](https://redis.io/docs/stack/search/reference/vectors/)
- [Redis JSON](https://redis.io/docs/stack/json/)
- [Redis Time Series](https://redis.io/docs/stack/timeseries/)

