# GA.Business.Intelligence

This project provides high-level analytical and intelligence services for Guitar Alchemist, following the modular architecture Layer 3 (Analysis/Intelligence).

## Overview

This library provides:
- **Semantic Indexing**: Tools and services to index musical concepts for semantic search.
- **Analytics**: Performance and usage analytics for musical applications.
- **BSP (Binary Space Partitioning)**: Room and space generation for musical visualizations.
- **Reasoning**: Advanced logic for musical relationships and classification.

## Architecture

This project follows the Guitar Alchemist modular architecture:
- **Layer 3 (Analysis)**: `GA.Business.Intelligence` (this project)
- **Dependencies**: `GA.Core`, `GA.Business.Core`, `GA.Business.Config`
- **Consumers**: Applications and AI services.

## Services/Features

### Semantic Indexing
Located in `SemanticIndexing/`:
- **ConceptIndexer**: Indexes scales, modes, and chords into vector databases.
- **SearchService**: Provides semantic search capabilities across musical knowledge.

### Analytics
Located in `Analytics/`:
- **MusicalPerformanceAnalytics**: Tracks and analyzes player progress.

### BSP
Located in `BSP/`:
- **RoomGenerator**: Generates 3D rooms for musical visualization based on BSP algorithms.

## Usage

### Indexing a Musical Concept

```csharp
var indexer = serviceProvider.GetRequiredService<IConceptIndexer>();
await indexer.IndexAsync(majorScale);
```

## Authors

Stephane Pareilleux
