# MongoDB AI/LLM Integration Plan for Guitar Alchemist

## Executive Summary

This document outlines a comprehensive plan for integrating MongoDB Atlas with AI/LLM capabilities to enhance the Guitar Alchemist chord data (427,254 chords). The integration will enable semantic search, natural language queries, and intelligent chord recommendations using MongoDB's Vector Search and RAG (Retrieval-Augmented Generation) capabilities.

## 1. MongoDB AI/LLM Capabilities Review

Based on MongoDB's documentation and the article on Large Language Models, MongoDB Atlas provides several key AI capabilities:

### 1.1 Atlas Vector Search
- **Semantic Similarity Search**: Store and search vector embeddings alongside source data
- **Integration with Popular LLMs**: Works with OpenAI, Hugging Face, Cohere
- **Unified Database**: Eliminates need for separate vector and operational databases
- **Cosine Similarity**: Find semantically similar items based on vector embeddings

### 1.2 Retrieval-Augmented Generation (RAG)
- **Long-term Memory for LLMs**: Provide business context from proprietary data
- **Reduce Hallucinations**: Ground LLM responses in actual data
- **Framework Integration**: Works with LangChain, LlamaIndex, Semantic Kernel
- **Personalized Responses**: Learn from user interactions over time

### 1.3 Atlas Search
- **Full-Text Search**: Search across chord names and descriptions
- **Faceted Search**: Filter by quality, extension, stacking type
- **Autocomplete**: Suggest chords as users type
- **Relevance Scoring**: Rank results by relevance

### 1.4 Aggregation Pipelines
- **Complex Queries**: Combine multiple operations
- **Data Transformation**: Reshape data for specific use cases
- **Statistical Analysis**: Analyze chord usage patterns
- **Relationship Discovery**: Find chord progressions and substitutions

## 2. Current Export Format Assessment

### 2.1 Existing JSON Structure
The current GaDataCLI exports two formats:

**all-chords.json** (376.59 MB, 427,254 chords):
```json
{
  "Id": 1,
  "Name": "C Major 7th",
  "Quality": "Major",
  "Extension": "Seventh",
  "StackingType": "Tertian",
  "NoteCount": 4,
  "Intervals": [
    {"Semitones": 0, "Function": "Root", "IsEssential": true},
    {"Semitones": 4, "Function": "MajorThird", "IsEssential": true},
    {"Semitones": 7, "Function": "PerfectFifth", "IsEssential": true},
    {"Semitones": 11, "Function": "MajorSeventh", "IsEssential": true}
  ],
  "PitchClassSet": [0, 4, 7, 11],
  "ParentScale": "Major",
  "ScaleDegree": 1,
  "Description": "...",
  "ConstructionType": "..."
}
```

**chord-templates.json** (277.47 MB, 1,486 unique pitch class sets):
- Groups chords by pitch class set
- Useful for finding enharmonic equivalents
- Reduces redundancy for template-based queries

### 2.2 MongoDB Compatibility
✅ **Already Compatible:**
- Valid JSON structure
- Works with `mongoimport --jsonArray`
- All fields properly typed
- Searchable structure

✅ **Good for Basic Queries:**
- Name, quality, extension fields for filtering
- Intervals array for precise matching
- Pitch class set for exact matching
- Parent scale and degree for harmonic context

⚠️ **Missing for AI Optimization:**
- No vector embeddings for semantic search
- No LLM-optimized context descriptions
- No metadata (difficulty, genre, common usage)
- No relationship data (substitutions, voice leading)
- No harmonic function classification

**Verdict**: Current format is excellent for immediate MongoDB import and basic querying. AI features require enhanced export format.

## 3. MongoDB Features for AI Integration

### 3.1 Priority 1: Vector Search
**Use Case**: Semantic similarity search for chords

**Implementation**:
1. Generate vector embeddings from chord characteristics
2. Create Atlas Vector Search index
3. Enable queries like "find chords similar to Cmaj7"

**Embedding Strategy**:
- Combine intervals, quality, pitch class set into text description
- Use OpenAI `text-embedding-3-small` (384 dimensions) or `text-embedding-3-large` (1536 dimensions)
- Store embeddings in `embedding` field

**Example Query**:
```javascript
db.chords.aggregate([
  {
    $vectorSearch: {
      index: "chord_vector_index",
      path: "embedding",
      queryVector: [0.123, -0.456, ...], // Cmaj7 embedding
      numCandidates: 100,
      limit: 10
    }
  }
])
```

### 3.2 Priority 2: Atlas Search
**Use Case**: Full-text search and faceted filtering

**Implementation**:
1. Create Atlas Search index on name, description fields
2. Add facets for quality, extension, stacking type
3. Enable autocomplete on chord names

**Example Search Index**:
```json
{
  "mappings": {
    "dynamic": false,
    "fields": {
      "name": {
        "type": "string",
        "analyzer": "lucene.standard"
      },
      "description": {
        "type": "string",
        "analyzer": "lucene.english"
      },
      "quality": {
        "type": "stringFacet"
      },
      "extension": {
        "type": "stringFacet"
      },
      "stackingType": {
        "type": "stringFacet"
      }
    }
  }
}
```

### 3.3 Priority 3: Aggregation Pipelines
**Use Case**: Complex queries and chord progression discovery

**Example - Find ii-V-I Progression**:
```javascript
db.chords.aggregate([
  {
    $match: {
      parentScale: "Major",
      scaleDegree: { $in: [2, 5, 1] }
    }
  },
  {
    $group: {
      _id: "$scaleDegree",
      chords: { $push: "$$ROOT" }
    }
  }
])
```

### 3.4 Priority 4: Document Structure Optimization
**Use Case**: Efficient LLM context windows

**Strategy**:
- Keep essential fields compact
- Add rich `llmContext` field for RAG
- Nest related data logically
- Use references for large relationships

## 4. AI-Optimized Schema Design

### 4.1 Enhanced Document Structure
```json
{
  "_id": ObjectId("..."),
  "chordId": 1,
  "name": "C Major 7th",
  "shortName": "Cmaj7",
  "aliases": ["CM7", "CΔ7", "Cmaj7"],
  
  // Core musical properties
  "quality": "Major",
  "extension": "Seventh",
  "stackingType": "Tertian",
  "noteCount": 4,
  
  // Intervals for precise matching
  "intervals": [
    {
      "semitones": 0,
      "function": "Root",
      "isEssential": true,
      "intervalName": "Unison"
    },
    {
      "semitones": 4,
      "function": "MajorThird",
      "isEssential": true,
      "intervalName": "Major Third"
    },
    {
      "semitones": 7,
      "function": "PerfectFifth",
      "isEssential": true,
      "intervalName": "Perfect Fifth"
    },
    {
      "semitones": 11,
      "function": "MajorSeventh",
      "isEssential": true,
      "intervalName": "Major Seventh"
    }
  ],
  
  // Pitch class set for exact matching
  "pitchClassSet": [0, 4, 7, 11],
  "pitchClassSetString": "0,4,7,11",
  
  // Parent scale context
  "parentScale": "Major",
  "scaleDegree": 1,
  "scaleMode": "Ionian",
  
  // Original description
  "description": "A major seventh chord built on tertian harmony with root, major third, perfect fifth, and major seventh",
  
  // AI-optimized context for RAG
  "llmContext": "The major seventh chord (Cmaj7) is a four-note chord with a bright, sophisticated sound. It consists of a root (C), major third (E), perfect fifth (G), and major seventh (B). This chord is fundamental in jazz, bossa nova, and contemporary music. It creates a sense of resolution and elegance, often used as a tonic chord in major keys. The major seventh interval gives it a dreamy, floating quality that distinguishes it from a dominant seventh chord.",
  
  // Vector embedding for semantic search (384 or 1536 dimensions)
  "embedding": [0.123, -0.456, 0.789, ...],
  
  // Metadata for filtering and AI context
  "metadata": {
    "difficulty": "intermediate",
    "commonUsage": ["jazz", "bossa nova", "r&b", "neo-soul"],
    "harmonicFunction": "tonic",
    "tension": "low",
    "color": "bright",
    "stability": "stable",
    "jazziness": "high",
    "popularity": "very-common"
  },
  
  // Relationships (can be computed or manually curated)
  "relationships": {
    "substitutions": [
      {"chordId": 42, "name": "C6", "reason": "Similar function, interchangeable"},
      {"chordId": 156, "name": "Cmaj9", "reason": "Extension of Cmaj7"}
    ],
    "voiceLeading": [
      {"chordId": 234, "name": "Dm7", "direction": "up", "commonTones": 2},
      {"chordId": 567, "name": "G7", "direction": "down", "commonTones": 1}
    ],
    "parallelChords": [
      {"chordId": 789, "name": "Cm7", "type": "parallel-minor"},
      {"chordId": 890, "name": "Caug7", "type": "altered"}
    ]
  },
  
  // Indexing and search optimization
  "searchText": "C Major 7th Cmaj7 CM7 CΔ7 major seventh tonic jazz",
  "tags": ["major", "seventh", "tertian", "jazz", "tonic"],
  
  // Timestamps
  "createdAt": ISODate("2025-10-03T18:00:00Z"),
  "updatedAt": ISODate("2025-10-03T18:00:00Z")
}
```

### 4.2 Index Strategy
```javascript
// Compound index for common queries
db.chords.createIndex({ quality: 1, extension: 1, stackingType: 1 })

// Text index for search
db.chords.createIndex({ searchText: "text", name: "text", description: "text" })

// Pitch class set index
db.chords.createIndex({ pitchClassSetString: 1 })

// Parent scale and degree
db.chords.createIndex({ parentScale: 1, scaleDegree: 1 })

// Vector search index (created in Atlas UI)
// Index name: chord_vector_index
// Path: embedding
// Dimensions: 384 or 1536
// Similarity: cosine
```

## 5. Implementation Plan

### Phase 1: Basic MongoDB Import (Week 1)
**Goal**: Get current data into MongoDB Atlas

**Tasks**:
1. ✅ Create MongoDB Atlas cluster (free tier for testing)
2. ✅ Import all-chords.json using mongoimport
   ```bash
   mongoimport --uri "mongodb+srv://..." \
     --db guitar-alchemist \
     --collection chords \
     --file all-chords.json \
     --jsonArray
   ```
3. ✅ Create basic indexes
4. ✅ Test basic queries
5. ✅ Update appsettings.json with MongoDB connection

**Deliverables**:
- MongoDB Atlas cluster with chord data
- Basic query examples
- Connection configuration

### Phase 2: Atlas Search Setup (Week 2)
**Goal**: Enable full-text search and faceted filtering

**Tasks**:
1. Create Atlas Search index in Atlas UI
2. Test text search queries
3. Implement faceted search
4. Add autocomplete functionality
5. Create search API endpoints in GaApi

**Deliverables**:
- Atlas Search index configuration
- Search API endpoints
- Query examples and documentation

### Phase 3: Vector Search Integration (Week 3-4)
**Goal**: Enable semantic similarity search

**Tasks**:
1. Choose embedding model (OpenAI text-embedding-3-small recommended)
2. Generate embeddings for all chords
   - Create batch processing script
   - Handle rate limits
   - Store embeddings in new field
3. Create Atlas Vector Search index
4. Implement similarity search API
5. Test "find similar chords" functionality

**Deliverables**:
- Embedding generation script
- Vector Search index
- Similarity search API
- Performance benchmarks

### Phase 4: LLM Integration with RAG (Week 5-6)
**Goal**: Enable natural language queries

**Tasks**:
1. Set up LangChain or LlamaIndex integration
2. Create prompt templates for chord queries
3. Implement RAG pipeline
   - Query MongoDB for relevant chords
   - Format context for LLM
   - Generate responses
4. Test natural language queries
5. Implement chord progression suggestions
6. Add conversation memory

**Deliverables**:
- RAG pipeline implementation
- Natural language query API
- Chord progression generator
- Example conversations

### Phase 5: Enhanced Export (Week 7)
**Goal**: Update GaDataCLI for AI-optimized export

**Tasks**:
1. Add new export option: `--export chords-ai`
2. Generate LLM context descriptions
3. Add metadata enrichment
4. Optionally generate embeddings during export
5. Create relationships data
6. Update documentation

**Deliverables**:
- Enhanced GaDataCLI with AI export
- AI-optimized JSON format
- Migration guide
- Updated README

## 6. Use Case Examples

### 6.1 Semantic Chord Search
**User Query**: "Find chords similar to Cmaj7 but with more tension"

**Implementation**:
```javascript
// 1. Get Cmaj7 embedding
const cmaj7 = await db.chords.findOne({ name: "C Major 7th" })

// 2. Vector search with tension filter
const results = await db.chords.aggregate([
  {
    $vectorSearch: {
      index: "chord_vector_index",
      path: "embedding",
      queryVector: cmaj7.embedding,
      numCandidates: 100,
      limit: 10,
      filter: {
        "metadata.tension": { $gt: "low" }
      }
    }
  },
  {
    $project: {
      name: 1,
      quality: 1,
      extension: 1,
      "metadata.tension": 1,
      score: { $meta: "vectorSearchScore" }
    }
  }
])
```

**Expected Results**:
- Cmaj7#11 (similar but more tension)
- Cmaj9 (extension adds color)
- C6/9 (similar function, different voicing)

### 6.2 Interval-Based Search
**User Query**: "Find all chords that contain a major third and a minor seventh"

**Implementation**:
```javascript
db.chords.find({
  intervals: {
    $all: [
      { $elemMatch: { semitones: 4, function: "MajorThird" } },
      { $elemMatch: { semitones: 10, function: "MinorSeventh" } }
    ]
  }
}).project({
  name: 1,
  quality: 1,
  intervals: 1
})
```

**Expected Results**:
- Dominant 7th chords (C7, D7, etc.)
- Dominant 9th chords
- Dominant 13th chords

### 6.3 RAG for Chord Theory
**User Prompt**: "Explain the harmonic function of a ii-V-I progression in jazz"

**RAG Pipeline**:
```javascript
// 1. Retrieve relevant chords
const chords = await db.chords.find({
  parentScale: "Major",
  scaleDegree: { $in: [2, 5, 1] },
  quality: { $in: ["Minor", "Dominant", "Major"] }
}).toArray()

// 2. Format context
const context = chords.map(c => 
  `${c.name}: ${c.llmContext}`
).join("\n\n")

// 3. Create prompt
const prompt = `
Context about chords in a ii-V-I progression:
${context}

Question: Explain the harmonic function of a ii-V-I progression in jazz.

Provide a comprehensive explanation including:
- The role of each chord
- Voice leading principles
- Why this progression is fundamental in jazz
- Common variations and substitutions
`

// 4. Call LLM
const response = await openai.chat.completions.create({
  model: "gpt-4",
  messages: [{ role: "user", content: prompt }]
})
```

**Expected Response**:
"The ii-V-I progression is the cornerstone of jazz harmony. In the key of C major, this would be Dm7-G7-Cmaj7. The ii chord (Dm7) is a minor seventh chord that creates mild tension and sets up the dominant. The V chord (G7) is a dominant seventh chord that creates strong tension through its tritone interval, demanding resolution. The I chord (Cmaj7) is the tonic major seventh that provides resolution and stability..."

### 6.4 Chord Progression Suggestions
**User Query**: "Suggest a chord progression in C major with a jazzy feel"

**Implementation**:
```javascript
// 1. Query jazz-appropriate chords in C major
const jazzChords = await db.chords.find({
  parentScale: "Major",
  "metadata.jazziness": { $in: ["high", "very-high"] },
  "metadata.commonUsage": "jazz"
}).toArray()

// 2. Use aggregation to find common progressions
const progressions = await db.chords.aggregate([
  {
    $match: {
      parentScale: "Major",
      "metadata.jazziness": "high"
    }
  },
  {
    $lookup: {
      from: "chords",
      localField: "relationships.voiceLeading.chordId",
      foreignField: "chordId",
      as: "nextChords"
    }
  }
])

// 3. Format for LLM
const context = `Available jazz chords: ${jazzChords.map(c => c.name).join(", ")}`

// 4. Generate progression with LLM
const prompt = `
${context}

Create a 4-8 bar jazz chord progression in C major.
Include:
- Chord names
- Roman numeral analysis
- Brief explanation of each chord's function
- Voice leading notes
`
```

**Expected Response**:
```
Suggested Progression:
1. Cmaj7 (I) - Tonic, establishes key
2. Am7 (vi) - Relative minor, smooth voice leading
3. Dm7 (ii) - Subdominant preparation
4. G7 (V) - Dominant tension
5. Cmaj7 (I) - Resolution
6. Em7 (iii) - Color chord
7. A7 (VI7) - Secondary dominant
8. Dm7 (ii) - Back to ii-V-I

Voice leading: Common tones between Cmaj7 and Am7 (C, E). 
Dm7 to G7 moves by fifth. G7 to Cmaj7 is classic resolution.
```

## 7. Configuration Updates

### 7.1 appsettings.json Enhancement
Add MongoDB configuration to `Apps/ga-server/GaApi/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  
  "MongoDB": {
    "ConnectionString": "mongodb+srv://username:password@cluster.mongodb.net/",
    "DatabaseName": "guitar-alchemist",
    "Collections": {
      "Chords": "chords",
      "ChordTemplates": "chord-templates",
      "Scales": "scales",
      "Progressions": "progressions",
      "Users": "users"
    },
    "VectorSearch": {
      "IndexName": "chord_vector_index",
      "EmbeddingDimensions": 384,
      "SimilarityMetric": "cosine",
      "NumCandidates": 100
    },
    "AtlasSearch": {
      "IndexName": "chord_search_index"
    }
  },
  
  "OpenAI": {
    "ApiKey": "sk-...",
    "EmbeddingModel": "text-embedding-3-small",
    "ChatModel": "gpt-4-turbo-preview",
    "MaxTokens": 4096,
    "Temperature": 0.7
  },
  
  "AI": {
    "EnableVectorSearch": true,
    "EnableRAG": true,
    "CacheEmbeddings": true,
    "MaxContextChords": 10
  }
}
```

### 7.2 Environment Variables
For sensitive data, use environment variables:
```bash
MONGODB_CONNECTION_STRING=mongodb+srv://...
OPENAI_API_KEY=sk-...
```

## 8. Next Steps

### Immediate Actions
1. ✅ Review this plan with stakeholders
2. ⬜ Set up MongoDB Atlas free tier cluster
3. ⬜ Import current chord data
4. ⬜ Test basic queries
5. ⬜ Decide on embedding model (OpenAI vs. open-source)

### Short-term Goals (1-2 months)
- Complete Phases 1-3 (Basic import, Search, Vector Search)
- Create API endpoints for search functionality
- Document query patterns and best practices

### Long-term Goals (3-6 months)
- Complete Phases 4-5 (RAG, Enhanced Export)
- Build web UI for chord exploration
- Implement chord progression generator
- Create educational content using RAG

## 9. Cost Considerations

### MongoDB Atlas
- **Free Tier (M0)**: 512 MB storage - Sufficient for testing
- **Shared Tier (M2)**: $9/month - 2 GB storage
- **Dedicated (M10)**: $57/month - 10 GB storage, better performance
- **Vector Search**: Included in all tiers

### OpenAI API
- **Embeddings**: $0.00002 per 1K tokens (text-embedding-3-small)
  - 427,254 chords × ~100 tokens = ~$0.85 one-time cost
- **Chat**: $0.01 per 1K tokens (GPT-4 Turbo)
  - Depends on usage, estimate $10-50/month for moderate use

### Total Estimated Cost
- **Development**: Free tier MongoDB + ~$1 for embeddings
- **Production**: $9-57/month MongoDB + $10-50/month OpenAI

## 10. Conclusion

The Guitar Alchemist chord data is well-positioned for MongoDB AI integration. The current JSON export format is already compatible with MongoDB and suitable for immediate import. By adding vector embeddings, enhanced metadata, and LLM context, we can unlock powerful AI capabilities including:

- Semantic chord similarity search
- Natural language queries
- Intelligent chord progression suggestions
- Educational content generation through RAG

The phased implementation plan allows for incremental value delivery, starting with basic MongoDB functionality and progressively adding AI features. The investment in MongoDB Atlas and OpenAI API is modest and provides significant value for musicians, educators, and developers working with chord theory.

