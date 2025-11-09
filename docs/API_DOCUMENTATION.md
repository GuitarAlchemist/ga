# API Documentation

## Overview

Guitar Alchemist provides a comprehensive REST API for music theory operations, chord search, and semantic analysis.

**Base URL**: `https://localhost:7001/api`

## Authentication

Currently, the API uses no authentication. In production, JWT tokens will be required.

## Endpoints

### Chords

#### Get All Chords

```http
GET /api/chords
```

**Query Parameters**:
- `skip` (int): Number of records to skip (default: 0)
- `take` (int): Number of records to return (default: 100)

**Response**:
```json
{
  "total": 427000,
  "chords": [
    {
      "id": "c-major",
      "name": "C Major",
      "root": "C",
      "quality": "Major",
      "intervals": ["Perfect Unison", "Major Third", "Perfect Fifth"],
      "notes": ["C", "E", "G"]
    }
  ]
}
```

#### Get Chord by ID

```http
GET /api/chords/{id}
```

**Response**:
```json
{
  "id": "c-major",
  "name": "C Major",
  "root": "C",
  "quality": "Major",
  "intervals": ["Perfect Unison", "Major Third", "Perfect Fifth"],
  "notes": ["C", "E", "G"],
  "voicings": [
    {
      "frets": [0, 3, 2],
      "strings": [6, 5, 4],
      "difficulty": "Easy"
    }
  ]
}
```

#### Search Chords

```http
GET /api/chords/search?query=major
```

**Query Parameters**:
- `query` (string): Search term
- `limit` (int): Maximum results (default: 50)

**Response**:
```json
{
  "results": [
    {
      "id": "c-major",
      "name": "C Major",
      "score": 0.95
    }
  ]
}
```

### Scales

#### Get All Scales

```http
GET /api/scales
```

**Response**:
```json
{
  "scales": [
    {
      "id": "c-major",
      "name": "C Major",
      "root": "C",
      "mode": "Ionian",
      "intervals": ["Perfect Unison", "Major Second", "Major Third", ...],
      "notes": ["C", "D", "E", "F", "G", "A", "B"]
    }
  ]
}
```

#### Get Scale Modes

```http
GET /api/scales/{root}/modes
```

**Response**:
```json
{
  "root": "C",
  "modes": [
    {
      "name": "Ionian (Major)",
      "intervals": ["Perfect Unison", "Major Second", "Major Third", ...]
    },
    {
      "name": "Dorian",
      "intervals": ["Perfect Unison", "Major Second", "Minor Third", ...]
    }
  ]
}
```

### Semantic Search

#### Search by Description

```http
GET /api/semantic-search/search?query=dark%20jazz%20chords
```

**Query Parameters**:
- `query` (string): Natural language description
- `limit` (int): Maximum results (default: 10)

**Response**:
```json
{
  "query": "dark jazz chords",
  "results": [
    {
      "id": "c-minor-7",
      "name": "C Minor 7",
      "similarity": 0.87,
      "description": "A dark, jazzy chord with minor quality"
    }
  ]
}
```

### Fretboard

#### Get Fretboard Positions

```http
GET /api/fretboard/positions?chord=c-major&tuning=standard
```

**Query Parameters**:
- `chord` (string): Chord ID
- `tuning` (string): Guitar tuning (default: standard)
- `maxFret` (int): Maximum fret position (default: 12)

**Response**:
```json
{
  "chord": "C Major",
  "tuning": "Standard (E-A-D-G-B-E)",
  "positions": [
    {
      "frets": [0, 3, 2, 0, 1, 0],
      "strings": [6, 5, 4, 3, 2, 1],
      "difficulty": "Easy",
      "voicing": "Root Position"
    }
  ]
}
```

#### Get Fretboard Analysis

```http
GET /api/fretboard/analysis?chord=c-major
```

**Response**:
```json
{
  "chord": "C Major",
  "analysis": {
    "commonPositions": 5,
    "difficulty": "Easy",
    "biomechanics": {
      "fingerSpread": "Comfortable",
      "stretchRequired": false,
      "barreRequired": false
    }
  }
}
```

### Progressions

#### Generate Progression

```http
POST /api/progressions/generate
```

**Request Body**:
```json
{
  "key": "C",
  "mode": "Major",
  "length": 4,
  "style": "Jazz",
  "constraints": {
    "avoidBarres": false,
    "maxDifficulty": "Intermediate"
  }
}
```

**Response**:
```json
{
  "progression": ["C Major", "A Minor", "D Minor", "G Dominant 7"],
  "analysis": {
    "voiceLeading": "Smooth",
    "harmonyQuality": "Excellent",
    "playability": "Good"
  }
}
```

### Music Theory

#### Explain Concept

```http
GET /api/theory/explain?concept=circle-of-fifths
```

**Response**:
```json
{
  "concept": "Circle of Fifths",
  "explanation": "The circle of fifths is a visual representation of the relationships among the twelve tones of the chromatic scale...",
  "examples": [
    {
      "key": "C Major",
      "relativeMinor": "A Minor"
    }
  ]
}
```

#### Get Interval Information

```http
GET /api/theory/intervals/{interval}
```

**Response**:
```json
{
  "interval": "Major Third",
  "semitones": 4,
  "ratio": "5:4",
  "quality": "Consonant",
  "examples": ["C to E", "D to F#"]
}
```

## Error Responses

### 400 Bad Request

```json
{
  "error": "Invalid query parameter",
  "message": "The 'limit' parameter must be between 1 and 1000",
  "statusCode": 400
}
```

### 404 Not Found

```json
{
  "error": "Chord not found",
  "message": "No chord with ID 'invalid-chord' exists",
  "statusCode": 404
}
```

### 500 Internal Server Error

```json
{
  "error": "Internal server error",
  "message": "An unexpected error occurred",
  "statusCode": 500
}
```

## Rate Limiting

- **Limit**: 1000 requests per minute per IP
- **Headers**: 
  - `X-RateLimit-Limit`: 1000
  - `X-RateLimit-Remaining`: 999
  - `X-RateLimit-Reset`: 1699000000

## Pagination

Use `skip` and `take` parameters:

```http
GET /api/chords?skip=0&take=50
GET /api/chords?skip=50&take=50
GET /api/chords?skip=100&take=50
```

## Filtering

Most endpoints support filtering:

```http
GET /api/chords?root=C&quality=Major
GET /api/scales?mode=Dorian
```

## Sorting

```http
GET /api/chords?sort=name&order=asc
GET /api/chords?sort=difficulty&order=desc
```

## Response Format

All responses follow this format:

```json
{
  "success": true,
  "data": { /* endpoint-specific data */ },
  "timestamp": "2025-11-09T10:30:00Z",
  "requestId": "req-12345"
}
```

## WebSocket Endpoints

### Chatbot Hub

**URL**: `wss://localhost:7001/hubs/chatbot`

**Methods**:
- `SendMessage(message, useSemanticSearch)`
- `ClearHistory()`
- `GetHistory()`

**Events**:
- `Connected`
- `ReceiveMessageChunk`
- `MessageComplete`
- `Error`

## Examples

### cURL

```bash
# Get all chords
curl https://localhost:7001/api/chords

# Search chords
curl "https://localhost:7001/api/chords/search?query=major"

# Semantic search
curl "https://localhost:7001/api/semantic-search/search?query=dark%20jazz"
```

### JavaScript/Fetch

```javascript
// Get chords
const response = await fetch('https://localhost:7001/api/chords');
const data = await response.json();

// Semantic search
const searchResponse = await fetch(
  'https://localhost:7001/api/semantic-search/search?query=dark%20jazz'
);
const searchData = await searchResponse.json();
```

### C# / HttpClient

```csharp
using var client = new HttpClient();
var response = await client.GetAsync("https://localhost:7001/api/chords");
var content = await response.Content.ReadAsStringAsync();
```

