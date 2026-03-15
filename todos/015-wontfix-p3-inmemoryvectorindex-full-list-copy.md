---
status: pending
priority: p3
issue_id: "015"
tags: [code-review, performance, vector-search, chatbot]
dependencies: []
---

# P3: InMemoryVectorIndex copies full document list on every search — GC pressure

## Problem Statement

`InMemoryVectorIndex.Search` does `snapshot = [.. _documents]` inside a lock on every search call. For a 5,000-document index (~500 bytes/doc), each call allocates ~2.5MB short-lived. At 10 concurrent requests, that is 25MB GC pressure per second.

## Proposed Solutions

### Option A: Replace snapshot copy with ReaderWriterLockSlim
```csharp
private readonly ReaderWriterLockSlim _lock = new();
// Search: _lock.EnterReadLock(); try { /* iterate _documents directly */ } finally { _lock.ExitReadLock(); }
// Add: _lock.EnterWriteLock(); try { _documents.Add(doc); } finally { _lock.ExitWriteLock(); }
```
- **Effort**: Small (1-2h)
- **Risk**: Low

### Option B: Document as known debt, fix in follow-up
Add a `// TODO: Performance — replace snapshot copy with ReaderWriterLockSlim` comment.
- **Effort**: Trivial
- **Risk**: Low for current scale (index is small in dev)

## Acceptance Criteria
- [ ] `InMemoryVectorIndex.Search` does not copy the full document list
- [ ] Concurrent reads do not block each other
- [ ] Writes (indexing) block reads briefly but do not deadlock

## Work Log
- 2026-03-03: Identified by performance-oracle (P2-C, rated P3 here for current scale)
