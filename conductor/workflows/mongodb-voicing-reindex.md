# MongoDB Chord Voicing Reindexing Workflow

**Workflow ID**: mongodb-voicing-reindex  
**Category**: Operations & Maintenance  
**Last Updated**: 2026-01-19

## Overview

This workflow describes how to reindex all chord voicings in MongoDB, including regenerating embeddings and vector search indexes.

## When to Use

Run this workflow when:
- ✅ Adding new voicings to the database
- ✅ Changing embedding model or dimensions
- ✅ Updating voicing generation parameters
- ✅ Migrating to a new MongoDB instance
- ✅ Fixing corrupted indexes
- ✅ Improving search quality

## Prerequisites

- [ ] MongoDB 8.0+ with vector search support
- [ ] Ollama running locally (for embeddings)
- [ ] GaApi service configured
- [ ] Sufficient disk space (~500MB for cache)

---

## Quick Reindex (Automated)

The simplest way to reindex is to **restart the GaApi service** with indexing enabled:

```bash
# 1. Stop GaApi
# (via Docker, systemd, or Ctrl+C if running locally)

# 2. Clear cache (optional, forces full regeneration)
rm -rf cache/indexes/voicings_v1.bin
rm -rf cache/embeddings/voicing_embeddings_*.bin

# 3. Start GaApi
dotnet run --project Apps/ga-server/GaApi

# Watch logs for:
# "Starting voicing index initialization..."
# "Generated {X} total voicings in {Y}s"
# "Indexed {X} voicing documents in {Y}s"
# "Generated embeddings in {Y}s"
# "Voicing search index initialized successfully"
```

**Expected Time**: 30-60 seconds (with cache), 2-5 minutes (without cache)

---

## Manual Reindex (Step-by-Step)

### Step 1: Generate Voicings

The `VoicingIndexInitializationService` runs automatically on startup.

**Configuration** (`appsettings.json`):
```json
{
  "VoicingSearch": {
    "EnableIndexing": true,
    "LazyLoading": false,
    "EnableBinaryCache": true,
    "MaxVoicingsToIndex": 1000,
    "MinPlayedNotes": 2,
    "NoteCountFilter": "ThreeNotes"
  }
}
```

**Process**:
1. Generates all possible voicings from fretboard
2. Filters by criteria (note count, playability)
3. Caches to `cache/indexes/voicings_v1.bin`

### Step 2: Generate Embeddings

Embeddings are generated using Ollama (local) or another embedding service.

**Process**:
1. Extracts unique searchable texts from documents
2. Batches requests (500 per batch)
3. Generates 384-dimensional embeddings (all-MiniLM-L6-v2)
4. Caches to `cache/embeddings/voicing_embeddings_{count}.bin`

**Manual Trigger** (if needed):
```csharp
// In a controller or service
await batchEmbeddingService.GenerateBatchEmbeddingsAsync(texts, cancellationToken);
```

### Step 3: Create MongoDB Vector Index

**Using mongosh** (MongoDB Shell):

```bash
# Run the script
mongosh < Scripts/create-vector-index.js

# Or manually:
mongosh
use guitar-alchemist

# Create index
db.chords.createSearchIndex({
  name: "chord_vector_index",
  type: "vectorSearch",
  definition: {
    fields: [{
      type: "vector",
      path: "Embedding",
      numDimensions: 384,
      similarity: "cosine"
    }]
  }
})

# Verify
db.chords.getIndexes()
```

**Using C#** (programmatically):

```csharp
// In GaApi startup or maintenance endpoint
await MongoVectorSearchIndexes.CreateChordVoicingsIndexAsync(database);
await MongoVectorSearchIndexes.CreateChordTemplatesIndexAsync(database);
```

### Step 4: Verify Indexing

**Check Logs**:
```
info: GaApi.Services.VoicingIndexInitializationService[0]
      Voicing search index initialized successfully with 1000 voicings in 45.2s total

info: GA.Domain.Services.Fretboard.Voicings.Search.EnhancedVoicingSearchService[0]
      Voicing search stats: 1000 voicings, 25.4 MB memory, 12ms avg search time
```

**Test Search**:
```bash
curl http://localhost:5000/api/voicings/search \
  -H "Content-Type: application/json" \
  -d '{"query": "jazz fusion Cmaj7", "limit": 10}'
```

---

## Configuration Options

### VoicingSearch Settings

| Setting | Default | Description |
|---------|---------|-------------|
| `EnableIndexing` | `true` | Enable automatic indexing on startup |
| `LazyLoading` | `false` | Defer indexing until first search |
| `EnableBinaryCache` | `true` | Use binary cache for faster startup |
| `MaxVoicingsToIndex` | `1000` | Maximum voicings to index |
| `MinPlayedNotes` | `2` | Minimum notes in a voicing |
| `NoteCountFilter` | `"ThreeNotes"` | Filter: TwoNotes, ThreeNotes, FourNotes, AllNotes |

### MongoDB Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Database | `guitar-alchemist` | MongoDB database name |
| Collection | `chord_voicings` | Voicing collection |
| Index Name | `chord_voicing_vector_index` | Vector search index |
| Dimensions | `384` | Embedding dimensions (all-MiniLM-L6-v2) |
| Similarity | `cosine` | Distance metric |

---

## Cache Management

### Cache Locations

```
cache/
├── indexes/
│   └── voicings_v1.bin          (~50MB, voicing documents)
└── embeddings/
    └── voicing_embeddings_{count}.bin  (~400MB, 384-dim vectors)
```

### Clear Cache

**Force Full Regeneration**:
```bash
# Remove all caches
rm -rf cache/

# Or selectively:
rm cache/indexes/voicings_v1.bin         # Force voicing regeneration
rm cache/embeddings/*.bin               # Force embedding regeneration
```

**Cache Versioning**:
- Cache files are versioned by document count
- Changing `MaxVoicingsToIndex` or `NoteCountFilter` creates new cache
- Old caches are ignored automatically

---

## Troubleshooting

### Issue: "No embeddings found"

**Cause**: Ollama not running or embedding service unavailable

**Fix**:
```bash
# 1. Start Ollama
ollama serve

# 2. Pull model if needed
ollama pull mxbai-embed-large

# 3. Restart GaApi
```

### Issue: "Vector search index error"

**Cause**: MongoDB version < 8.0 or Community Edition not configured for vector search

**Fix**:
```bash
# Check MongoDB version
mongosh --eval "db.version()"

# Should be 8.0.0 or higher
# Upgrade if needed: https://www.mongodb.com/docs/manual/release-notes/8.0/
```

### Issue: "Index build takes too long"

**Cause**: Large number of voicings or slow embedding generation

**Fix**:
```json
{
  "VoicingSearch": {
    "MaxVoicingsToIndex": 500,  // Reduce from 1000
    "NoteCountFilter": "ThreeNotes"  // More selective filter
  }
}
```

### Issue: "Out of memory"

**Cause**: Too many voicings loaded into memory

**Fix**:
```json
{
  "VoicingSearch": {
    "LazyLoading": true,  // Load on demand
    "MaxVoicingsToIndex": 500
  }
}
```

---

## Performance Benchmarks

### Typical Performance (2024 MacBook Pro, M2)

| Phase | Time | Notes |
|-------|------|-------|
| Voicing Generation | 5-10s | ~50,000 raw voicings |
| Filtering & Indexing | 2-5s | Down to 1,000 documents |
| Embedding Generation | 30-120s | Depends on Ollama |
| Search Service Init | <1s | Loading into memory |
| **Total (Cold Start)** | **2-5 min** | Without cache |
| **Total (Warm Start)** | **30-60s** | With cache |

### Cache Benefits

- **Voicing Cache**: 10x faster startup (5s vs 50s)
- **Embedding Cache**: 100x faster startup (1s vs 120s)
- **Total Improvement**: ~4 minutes saved per restart

---

## API Endpoints (Future)

### Planned Maintenance Endpoints

```http
POST /api/admin/voicings/reindex
POST /api/admin/voicings/clear-cache
GET  /api/admin/voicings/stats
```

**Note**: Not yet implemented. Use workflow above for now.

---

## Related Files

### Source Code
- `Apps/ga-server/GaApi/Services/VoicingIndexInitializationService.cs` - Main indexing service
- `Apps/ga-server/GaApi/Services/MongoVectorSearchIndexes.cs` - MongoDB index management
- `GA.Domain.Services.Fretboard.Voicings.Search/VoicingIndexingService.cs` - Voicing indexer

### Scripts
- `Scripts/create-vector-index.js` - MongoDB index creation script

### Configuration
- `Apps/ga-server/GaApi/appsettings.json` - Service configuration
- `Apps/ga-server/GaApi/appsettings.Development.json` - Dev overrides

---

## Monitoring

### Metrics to Track

- **Index Size**: Number of voicings indexed
- **Memory Usage**: MB consumed by search service
- **Search Performance**: Average search time (ms)
- **Cache Hit Rate**: Percentage of cached lookups
- **Index Age**: Time since last reindex

### Logs to Watch

```bash
# Successful indexing
grep "Voicing search index initialized successfully" logs/gaapi.log

# Performance stats
grep "Voicing search stats" logs/gaapi.log

# Errors
grep "ERROR.*voicing" logs/gaapi.log
```

---

## Best Practices

1. ✅ **Use Cache**: Enable `EnableBinaryCache` for production
2. ✅ **Schedule Reindex**: Reindex weekly or after major updates
3. ✅ **Monitor Performance**: Track avg search time and memory
4. ✅ **Version Indexes**: Use semantic versioning for index changes
5. ✅ **Test Searches**: Validate quality after reindexing
6. ✅ **Backup MongoDB**: Before major reindexing operations

---

## Changelog

- **2026-01-19**: Initial workflow documentation created
- **Next**: Automate via admin API endpoints

---

**For Questions**: See [Conductor Index](../../index.md) or [Tech Stack](../../tech-stack.md)
