# Semantic Fretboard Indexing and Natural Language Querying

## Overview

The Guitar Alchemist Semantic Fretboard system provides end-to-end semantic indexing of guitar voicings with natural language querying powered by real LLM models. Musicians can ask questions in plain English and get intelligent responses with relevant chord voicings and practical playing advice.

## Features

- **Complete Fretboard Analysis**: Indexes all possible chord voicings for any instrument/tuning
- **Rich Musical Metadata**: Includes chord templates, pitch class sets, modal families, biomechanical analysis
- **Vector Embeddings**: Uses real embedding models (nomic-embed-text) for semantic similarity
- **LLM Integration**: Powered by Llama 3.2 for natural language understanding and response generation
- **Performance Optimized**: Efficient indexing and sub-second query response times
- **Comprehensive Testing**: Full integration tests with real Ollama models

## Quick Start

### Prerequisites

1. **Install Ollama**
   ```bash
   # Visit https://ollama.ai and install for your platform
   # Then start the service
   ollama serve
   ```

2. **Build the Solution**
   ```bash
   dotnet build AllProjects.sln
   ```

### Running the Demo

```bash
# Run the complete demo (includes indexing and sample queries)
./Scripts/demo-semantic-fretboard.ps1

# Or run individual components
./Scripts/test-semantic-fretboard.ps1 -TestType Integration
```

### CLI Usage

```bash
# Index voicings and run interactive mode
dotnet run --project GaCLI -- semantic-fretboard --index --interactive

# Process a single query
dotnet run --project GaCLI -- semantic-fretboard --query "easy major chords for beginners"

# Index with specific parameters
dotnet run --project GaCLI -- semantic-fretboard --index --tuning standard --max-fret 12
```

## Architecture

### Core Components

1. **SemanticFretboardService**: Main orchestration service
2. **OllamaLlmService**: LLM integration with automatic model management
3. **SemanticSearchService**: Vector search with embedding generation
4. **FretboardChordsGenerator**: Generates all possible voicings
5. **FretboardChordAnalyzer**: Analyzes voicings for musical characteristics

### Data Flow

```
Guitar Voicings → Musical Analysis → Vector Embeddings → Semantic Index
                                                              ↓
Natural Language Query → LLM Processing ← Semantic Search Results
                                ↓
                    Intelligent Response with Recommendations
```

## Example Queries

The system handles a wide range of natural language queries:

### Beginner Queries
- "What are some easy open chords for a beginner?"
- "Show me simple major chords that don't require barre"
- "I need basic chord shapes for folk music"

### Advanced Queries
- "Find me jazz voicings for Dm7 with extensions"
- "What are some quartal harmony chords for modern music?"
- "Show me chord voicings that work well in DADGAD tuning"

### Contextual Queries
- "I need bright sounding chords for fingerpicking"
- "Find warm, mellow voicings for acoustic ballads"
- "What power chords work best for metal in drop D?"

### Technical Queries
- "Show me chord shapes that avoid using the pinky finger"
- "Find voicings with good voice leading for chord progressions"
- "What are some ergonomic chord shapes for small hands?"

## Testing

### Integration Tests

Run real integration tests with Ollama:

```bash
# Run all integration tests
./Scripts/test-semantic-fretboard.ps1 -TestType Integration

# Run performance tests
./Scripts/test-semantic-fretboard.ps1 -TestType Performance

# Run specific test categories
dotnet test --filter "TestCategory=Ollama&TestCategory=RealLLM"
```

### Test Categories

- **Integration**: Tests with real Ollama models
- **Performance**: Benchmarks indexing and query performance
- **RealLLM**: Tests actual LLM responses and quality
- **Unit**: Fast unit tests with mocks

## Performance Characteristics

### Indexing Performance
- **Standard Guitar (12 frets)**: ~5,000-15,000 voicings
- **Indexing Rate**: 50-200 voicings/second (depending on hardware)
- **Memory Usage**: ~500MB-2GB for full index
- **Success Rate**: >95% with real embedding models

### Query Performance
- **Average Response Time**: 2-5 seconds
- **Search Time**: <100ms for semantic search
- **LLM Response Time**: 1-4 seconds (model dependent)
- **Concurrent Queries**: Supports multiple simultaneous queries

## Configuration

### Ollama Models

The system automatically downloads and manages these models:

- **Embedding**: `nomic-embed-text` (768 dimensions)
- **LLM**: `llama3.2:latest` (preferred) with fallbacks to smaller models

### Tuning Options

Supported guitar tunings:
- Standard (E-A-D-G-B-E)
- Drop D (D-A-D-G-B-E)
- DADGAD (D-A-D-G-A-D)
- Custom tunings via API

## API Reference

### SemanticFretboardService

```csharp
// Index voicings
var result = await service.IndexFretboardVoicingsAsync(
    tuning: Tuning.StandardGuitar,
    instrumentName: "Guitar",
    maxFret: 12,
    includeBiomechanicalAnalysis: true);

// Process natural language query
var queryResult = await service.ProcessNaturalLanguageQueryAsync(
    "easy major chords",
    maxResults: 10);
```

### Query Results

```csharp
public record QueryResult(
    string Query,                                    // Original query
    List<SearchResult> SearchResults,               // Matching voicings
    string LlmInterpretation,                       // LLM response
    TimeSpan ElapsedTime,                           // Response time
    string ModelUsed);                              // LLM model used
```

## Troubleshooting

### Common Issues

1. **Ollama Not Running**
   ```
   Error: Ollama is not running at http://localhost:11434
   Solution: Start Ollama with `ollama serve`
   ```

2. **Model Download Fails**
   ```
   Error: Failed to download model
   Solution: Check internet connection and Ollama installation
   ```

3. **Slow Indexing**
   ```
   Issue: Indexing takes too long
   Solution: Reduce maxFret parameter or disable biomechanical analysis
   ```

4. **Poor Query Results**
   ```
   Issue: LLM responses are not relevant
   Solution: Ensure proper model is downloaded and index is populated
   ```

### Performance Tuning

- **Faster Indexing**: Set `includeBiomechanicalAnalysis: false`
- **Smaller Index**: Reduce `maxFret` parameter
- **Better Responses**: Use larger LLM models (llama3.1:8b vs phi3:mini)
- **Memory Optimization**: Clear index between different tunings

## Contributing

### Adding New Features

1. **New Query Types**: Extend `SemanticDocumentGenerator` for new metadata
2. **Additional Models**: Update `OllamaLlmService.PreferredModels`
3. **New Tunings**: Add to `Tuning` class and update CLI options
4. **Performance Improvements**: Focus on embedding generation and vector search

### Testing Guidelines

- Add integration tests for new query types
- Include performance benchmarks for significant changes
- Test with multiple LLM models for compatibility
- Verify memory usage doesn't grow significantly

## License

This project is part of the Guitar Alchemist suite. See the main repository LICENSE file for details.