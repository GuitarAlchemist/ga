# Track: Project Consolidation & Modernization

## Overview
Status: Active
Owner: Antigravity
Description: Consolidate service registration, stabilize namespaces, modernized code, and complete the microservices transition for Guitar Alchemist.

## Goals
- [ ] Refactor service registration in `GaApi/Program.cs` into dedicated extension methods.
- [ ] Stabilize namespaces and project structure.
- [ ] Implement C# 12+ features (Primary Constructors, Collection Expressions).
- [ ] Progress microservices migration (Theory, etc.).
- [ ] Implement YARP Gateway configuration.
- [ ] Unified caching strategy (Redis + IMemoryCache).
- [ ] MongoDB query optimization.
- [ ] Enhanced vector models.

## Tasks
- [ ] Create `ServiceCollectionExtensions` for Core, Theory, and AI services.
- [ ] Cleanup `GaApi/Program.cs`.
- [ ] Audit `GA.MusicTheory.Service` for missing logic from the monolith.
- [ ] Configure YARP for routing.
- [ ] Modernize service constructors in `GA.Business.ML`.
- [ ] Implement robust `ToEmbeddingString()` for all `RagDocumentBase` types.
