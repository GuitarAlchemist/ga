---
status: pending
priority: p3
issue_id: "020"
tags: [code-review, performance, chatbot, embeddings]
dependencies: []
---

# P3: Chord comparison generates two sequential embedding calls — should be Task.WhenAll

## Problem Statement

`SpectralRagOrchestrator.HandleChordComparisonAsync` generates embeddings for two chords sequentially:
```csharp
var v1 = await generator.GenerateEmbeddingAsync(CreateVirtualDoc(c1));
var v2 = await generator.GenerateEmbeddingAsync(CreateVirtualDoc(c2));
```
These have no data dependency and can be parallelised. Estimated saving: 50% of comparison-path embedding time (~100-500ms depending on backend).

## Proposed Solutions

### Option A: Task.WhenAll (Recommended)
```csharp
var (v1, v2) = await (
    generator.GenerateEmbeddingAsync(CreateVirtualDoc(c1)),
    generator.GenerateEmbeddingAsync(CreateVirtualDoc(c2))
).WhenAll();
// or:
var results = await Task.WhenAll(
    generator.GenerateEmbeddingAsync(CreateVirtualDoc(c1)),
    generator.GenerateEmbeddingAsync(CreateVirtualDoc(c2)));
var (v1, v2) = (results[0], results[1]);
```
- **Effort**: Trivial (2 lines)
- **Risk**: None

## Acceptance Criteria
- [ ] Two embedding calls in `HandleChordComparisonAsync` run concurrently
- [ ] Comparison query latency decreases by ~50% for embedding step

## Work Log
- 2026-03-03: Identified by performance-oracle (P3-F)

## Resources
- Source: `Apps/GaChatbot/Services/SpectralRagOrchestrator.cs` (HandleChordComparisonAsync)
