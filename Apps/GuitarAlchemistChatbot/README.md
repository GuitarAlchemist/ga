# Guitar Alchemist Chatbot

An AI-powered chatbot for Guitar Alchemist that provides intelligent chord search, music theory explanations, and guitar
guidance using semantic search over a database of 427,000+ chords.

## Features

- **Semantic Chord Search**: Find chords using natural language descriptions like "dark jazz chords" or "bright major
  chords"
- **Similar Chord Discovery**: Get suggestions for chords similar to any specific chord
- **Music Theory Explanations**: Ask about scales, modes, progressions, and other music theory concepts
- **Interactive Chat Interface**: Modern, responsive chat UI with real-time responses
- **Chord Visualization**: Display chord details including intervals, qualities, and relationships

## Prerequisites

- .NET 9.0 SDK
- MongoDB with Guitar Alchemist chord data and vector embeddings
- OpenAI API key for embeddings and chat

## Configuration

1. **MongoDB Setup**: Ensure MongoDB is running with the Guitar Alchemist database containing chord embeddings
2. **OpenAI API Key**: Set your OpenAI API key in `appsettings.json`:

```json
{
  "OpenAI": {
    "ApiKey": "your-openai-api-key-here"
  }
}
```

3. **MongoDB Connection**: Update the connection string if needed:

```json
{
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  }
}
```

## Running the Application

1. **Build the project**:
   ```bash
   dotnet build
   ```

2. **Run the application**:
   ```bash
   dotnet run
   ```

3. **Open your browser** to `https://localhost:7100` or `http://localhost:5100`

## Usage Examples

Try asking the chatbot:

- "Show me some dark jazz chords"
- "What are the modes of the major scale?"
- "Find chords similar to Cmaj7"
- "Explain voice leading"
- "What makes a chord sound jazzy?"
- "Show me some easy beginner chords"

## Architecture

- **Blazor Server**: Real-time interactive web UI
- **Microsoft.Extensions.AI**: AI integration framework
- **OpenAI GPT-4o-mini**: Chat completion model
- **MongoDB Vector Search**: Semantic search over chord embeddings
- **Function Calling**: AI can call specific functions to search chords and explain theory

## AI Functions

The chatbot has access to these specialized functions:

- `SearchChords`: Find chords using natural language queries
- `FindSimilarChords`: Get chords similar to a specific chord
- `GetChordDetails`: Retrieve detailed information about a chord
- `ExplainMusicTheory`: Provide structured explanations of music theory concepts

## Development

The project structure:

- `Components/Pages/Chat.razor`: Main chat interface
- `Services/ChordSearchService.cs`: MongoDB vector search integration
- `Services/GuitarAlchemistFunctions.cs`: AI function tools
- `Components/Shared/ChordCard.razor`: Chord result visualization

## Dependencies

- Microsoft.Extensions.AI
- Microsoft.Extensions.AI.OpenAI
- MongoDB.Driver
- OpenAI SDK

## Notes

- Requires the Guitar Alchemist MongoDB database with vector embeddings
- Uses OpenAI's text-embedding-3-small model for semantic search
- Designed to work with the existing Guitar Alchemist ecosystem
