# Graphiti-Powered Autonomous Retroaction Loop

## Overview

The **Graphiti-Powered Autonomous Retroaction Loop** is a self-improving knowledge system that uses a temporal knowledge graph (Graphiti) to guide intelligent YouTube video curation and knowledge extraction for Guitar Alchemist.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Graphiti Knowledge Graph                      │
│  (Temporal graph tracking what we know and what's missing)      │
└────────────┬────────────────────────────────────┬───────────────┘
             │                                    │
             ▼                                    ▼
┌────────────────────────┐          ┌────────────────────────────┐
│  Knowledge Gap         │          │  Retroaction Loop          │
│  Analyzer              │          │  Orchestrator              │
│                        │          │                            │
│  • Analyzes graph      │          │  • Teacher/Student         │
│  • Identifies gaps     │          │    dialogue                │
│  • Prioritizes needs   │          │  • Gap identification      │
└────────┬───────────────┘          │  • Knowledge refinement    │
         │                          └────────┬───────────────────┘
         ▼                                   │
┌────────────────────────┐                  │
│  Autonomous Curation   │◄─────────────────┘
│  Orchestrator          │
│                        │
│  1. Gap Analysis       │
│  2. YouTube Search     │
│  3. Quality Evaluation │
│  4. Decision Making    │
│  5. Processing         │
│  6. Graph Update       │
└────────┬───────────────┘
         │
         ▼
┌────────────────────────────────────────────────────────────────┐
│                    YouTube Video Pipeline                       │
│                                                                 │
│  Search → Evaluate → Accept/Reject → Extract → Update Graph    │
└─────────────────────────────────────────────────────────────────┘
```

## Components

### 1. Knowledge Gap Analyzer (`KnowledgeGapAnalyzer.cs`)

**Purpose**: Analyzes the current knowledge base and identifies gaps

**Key Features**:
- Analyzes chord progression coverage
- Analyzes scale coverage
- Analyzes technique coverage
- Analyzes music theory concept coverage
- Uses Ollama to prioritize gaps by importance

**Output**: `KnowledgeGapAnalysis` with prioritized list of gaps

**Example Gap**:
```json
{
  "category": "ChordProgression",
  "topic": "ii-V-I",
  "description": "Missing coverage of essential chord progression: ii-V-I",
  "priority": "High",
  "suggestedSearchQuery": "guitar ii-V-I chord progression tutorial"
}
```

### 2. YouTube Search Service (`YouTubeSearchService.cs`)

**Purpose**: Search YouTube for videos without API keys

**Key Features**:
- Uses **Invidious** public instances (free, no API key)
- Fallback to multiple instances for reliability
- Returns video metadata (title, description, views, duration, etc.)

**Invidious Instances**:
- `https://invidious.snopyta.org`
- `https://yewtu.be`
- `https://invidious.kavin.rocks`
- `https://vid.puffyan.us`

### 3. Video Quality Evaluator (`VideoQualityEvaluator.cs`)

**Purpose**: Evaluate videos for quality, relevance, and educational value

**Evaluation Criteria**:
1. **Engagement Score** (0-1):
   - View count (logarithmic scale)
   - Recency (newer is better)
   - Duration (5-20 minutes ideal)

2. **Relevance Score** (0-1):
   - Ollama analyzes title, description, transcript
   - Keyword matching as fallback

3. **Educational Value Score** (0-1):
   - Ollama evaluates teaching quality
   - Clear explanations, structured content, practical examples

4. **Overall Quality Score** (0-1):
   - Weighted average: Relevance (40%) + Educational (40%) + Engagement (20%)

**Decision Logic**:
- **Accept**: Quality ≥ 0.7 AND Relevance ≥ 0.6
- **Needs Review**: Quality ≥ 0.5
- **Reject**: Quality < 0.5

### 4. Autonomous Curation Orchestrator (`AutonomousCurationOrchestrator.cs`)

**Purpose**: Orchestrate the entire autonomous curation process

**Workflow**:
1. **Analyze Gaps**: Get prioritized list of knowledge gaps
2. **Filter Gaps**: Apply user criteria (categories, priorities)
3. **For Each Gap**:
   - Search YouTube for relevant videos
   - Evaluate each video
   - Make curation decision
   - If accepted, process through retroaction loop
   - Update Graphiti knowledge graph
4. **Return Results**: Summary of accepted/rejected videos

**Configuration**:
```json
{
  "maxVideosPerGap": 3,
  "maxTotalVideos": 10,
  "minQualityScore": 0.7,
  "focusCategories": ["ChordProgression", "Scale"],
  "focusPriorities": ["Critical", "High"]
}
```

### 5. Retroaction Loop Integration

The autonomous curation system integrates with the existing **Retroaction Loop Orchestrator** to process accepted videos:

**Teacher/Student Pattern**:
1. **Teacher Perspective**: Ollama generates conversational explanation
2. **Student Perspective**: Ollama critiques from learner viewpoint
3. **Gap Analysis**: Compare perspectives to identify missing knowledge
4. **Refinement**: Generate new content addressing gaps
5. **Iteration**: Repeat until convergence

### 6. Graphiti Knowledge Graph Integration

**TODO**: Full Graphiti integration (currently stubbed)

**Planned Features**:
- Add videos as learning episodes
- Add extracted knowledge (chords, scales, techniques) as nodes
- Create relationships between concepts
- Update user learning progress
- Mark knowledge gaps as filled
- Track temporal evolution of knowledge

## API Endpoints

### Knowledge Gap Analysis

```http
GET /api/autonomous-curation/analyze-gaps
```

Returns complete knowledge gap analysis.

```http
GET /api/autonomous-curation/gaps/by-priority/{priority}
```

Get gaps by priority (Critical, High, Medium, Low).

```http
GET /api/autonomous-curation/gaps/by-category/{category}
```

Get gaps by category (ChordProgression, Scale, Technique, Theory).

```http
GET /api/autonomous-curation/stats
```

Get curation statistics.

### Autonomous Curation

```http
POST /api/autonomous-curation/start
Content-Type: application/json

{
  "maxVideosPerGap": 3,
  "maxTotalVideos": 10,
  "minQualityScore": 0.7,
  "focusCategories": ["ChordProgression"],
  "focusPriorities": ["High"]
}
```

Start autonomous curation with custom settings.

```http
POST /api/autonomous-curation/start/quick
```

Start quick curation with default settings (high-priority gaps only).

## Usage Example

### 1. Analyze Knowledge Gaps

```bash
curl http://localhost:5000/api/autonomous-curation/analyze-gaps
```

**Response**:
```json
{
  "status": "success",
  "message": "Found 25 knowledge gaps",
  "data": {
    "analysisDate": "2025-01-09T12:00:00Z",
    "gaps": [
      {
        "category": "ChordProgression",
        "topic": "ii-V-I",
        "priority": "High",
        "suggestedSearchQuery": "guitar ii-V-I chord progression tutorial"
      }
    ]
  }
}
```

### 2. Start Autonomous Curation

```bash
curl -X POST http://localhost:5000/api/autonomous-curation/start/quick
```

**Response**:
```json
{
  "status": "success",
  "message": "Curation complete: 3 videos accepted, 2 rejected",
  "data": {
    "startTime": "2025-01-09T12:00:00Z",
    "endTime": "2025-01-09T12:05:00Z",
    "status": "Completed",
    "gapsAnalyzed": 25,
    "videosFound": 15,
    "videosEvaluated": 15,
    "videosAccepted": 3,
    "videosRejected": 2,
    "decisions": [...]
  }
}
```

## Benefits

1. **Autonomous Learning**: System identifies and fills its own knowledge gaps
2. **Quality Control**: Ollama-powered evaluation ensures high-quality content
3. **Free**: No YouTube API key required (uses Invidious)
4. **Intelligent**: Uses LLM to understand relevance and educational value
5. **Scalable**: Can process multiple gaps in parallel
6. **Traceable**: All decisions logged with reasoning
7. **Graph-Driven**: Graphiti tracks knowledge evolution over time

## Future Enhancements

1. **Full Graphiti Integration**: Complete knowledge graph updates
2. **User Feedback Loop**: Learn from user ratings of curated content
3. **Multi-Source**: Expand beyond YouTube (Coursera, Udemy, etc.)
4. **Collaborative Filtering**: Learn from what similar users found valuable
5. **Adaptive Thresholds**: Adjust quality thresholds based on gap priority
6. **Scheduled Curation**: Run autonomous curation on a schedule
7. **Notification System**: Alert users when new high-quality content is found

## Files Created

- `Services/KnowledgeGapAnalyzer.cs` - Gap analysis service
- `Services/YouTubeSearchService.cs` - YouTube search (Invidious)
- `Services/VideoQualityEvaluator.cs` - Video quality evaluation
- `Services/AutonomousCurationOrchestrator.cs` - Main orchestrator
- `Controllers/AutonomousCurationController.cs` - API endpoints
- `Models/KnowledgeGapModels.cs` - Data models

## Integration Points

- **MongoDB**: Store processed documents, gap analyses, curation results
- **Ollama**: LLM for evaluation, prioritization, knowledge extraction
- **Graphiti**: Temporal knowledge graph (TODO: full integration)
- **Retroaction Loop**: Process accepted videos through teacher/student pattern
- **YouTube Transcript Service**: Extract transcripts for deeper analysis

## Next Steps

1. Move files to `Apps/ga-server/GaApi` directory
2. Register services in `GaApi/Program.cs`
3. Test autonomous curation end-to-end
4. Implement full Graphiti integration
5. Add monitoring dashboard
6. Deploy to production

