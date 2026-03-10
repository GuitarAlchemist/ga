---
status: complete
priority: p2
issue_id: "037"
tags: [performance, embeddings, semantic-router, caching, code-review]
dependencies: []
---

# 037 — `SemanticRouter` Generates Query Embedding on Every Request with No Cache

## Problem Statement

`SemanticRouteAsync` calls `textEmbeddings!.GenerateAsync([query])` on every request that reaches the semantic routing path. Agent embeddings are correctly cached after first initialization. The query embedding is not. For a text embedding model this is a remote call (~30–80 ms on a warm Ollama instance) on every request. Guitar Alchemist users frequently repeat similar queries in a session ("what key is this?", "what key does this sound like?") — a shallow cache would eliminate most duplicate embedding calls.

## Findings

`Common/GA.Business.ML/Agents/SemanticRouter.cs` lines 267–268:
```csharp
var queryEmbedding = await textEmbeddings!.GenerateAsync([query]);
var queryVector = queryEmbedding.ToArray();   // no cache
```

Agent embeddings at lines 226–259: correctly guarded with `_agentEmbeddingsInitialized` double-check lock and cached in `_agentEmbeddings`.

## Proposed Solutions

### Option A — Short-TTL `MemoryCache` (Recommended)
```csharp
private readonly MemoryCache _queryCache = new(new MemoryCacheOptions { SizeLimit = 256 });

private async Task<float[]> GetQueryEmbeddingAsync(string query)
{
    var key = query.Trim().ToLowerInvariant();
    if (_queryCache.TryGetValue(key, out float[]? cached)) return cached!;
    var embedding = (await textEmbeddings!.GenerateAsync([query])).ToArray();
    _queryCache.Set(key, embedding, new MemoryCacheEntryOptions
        { Size = 1, SlidingExpiration = TimeSpan.FromMinutes(5) });
    return embedding;
}
```
- **Effort:** Small.
- **Risk:** Low — embeddings are deterministic for the same input.

### Option B — `ConcurrentDictionary` LRU with eviction
Use a bounded `ConcurrentDictionary` with count-based eviction.
- **Effort:** Small.

### Option C — No cache (accept current behaviour)
- **Risk:** 30–80 ms overhead per request on the semantic routing path.

## Recommended Action
Option A.

## Acceptance Criteria

- [ ] Identical query strings within a 5-minute window do not trigger a second embedding API call
- [ ] Cache is bounded (max 256 entries) to avoid memory growth
- [ ] Embedding API call count is observable via telemetry (existing Activity tags)

## Work Log

- 2026-03-10: Identified during performance review agent for PR #8
