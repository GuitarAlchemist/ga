# Guitar Alchemist Chatbot with Semantic Search

## Overview

This implementation provides a complete chatbot system with:

- **Ollama Integration**: Local LLM (llama3.2:3b) for conversational AI
- **Semantic Search**: Context-aware responses using guitar knowledge base
- **WebSocket Support**: Real-time streaming responses via SignalR
- **REST API**: Alternative HTTP endpoints for chat interactions
- **Embedding Service**: Local embeddings using Ollama (nomic-embed-text)

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Client Applications                      │
│  (WebSocket Client, REST Client, React Frontend)            │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                      GaApi Server                            │
│  ┌──────────────────┐  ┌──────────────────┐                │
│  │  ChatbotHub      │  │ ChatbotController│                │
│  │  (WebSocket)     │  │  (REST API)      │                │
│  └────────┬─────────┘  └────────┬─────────┘                │
│           │                     │                            │
│           └──────────┬──────────┘                            │
│                      ▼                                        │
│  ┌─────────────────────────────────────────────────────┐   │
│  │         OllamaChatService                            │   │
│  │  - Streaming chat responses                          │   │
│  │  - Conversation history management                   │   │
│  └─────────────────────────────────────────────────────┘   │
│                      │                                        │
│                      ▼                                        │
│  ┌─────────────────────────────────────────────────────┐   │
│  │      SemanticSearchService                           │   │
│  │  - Vector similarity search                          │   │
│  │  - Hybrid filtering                                  │   │
│  │  - Context retrieval                                 │   │
│  └─────────────────────────────────────────────────────┘   │
│                      │                                        │
│                      ▼                                        │
│  ┌─────────────────────────────────────────────────────┐   │
│  │      OllamaEmbeddingService                          │   │
│  │  - Generate embeddings for queries                   │   │
│  │  - nomic-embed-text model                            │   │
│  └─────────────────────────────────────────────────────┘   │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                    Ollama Server                             │
│  - llama3.2:3b (Chat Model)                                 │
│  - nomic-embed-text (Embedding Model)                       │
│  - Running on http://localhost:11434                        │
└─────────────────────────────────────────────────────────────┘
```

## Prerequisites

### 1. Install Ollama

**Windows/Mac/Linux:**

```bash
# Download from https://ollama.ai
# Or use package manager:
# Windows: winget install Ollama.Ollama
# Mac: brew install ollama
# Linux: curl https://ollama.ai/install.sh | sh
```

### 2. Pull Required Models

```bash
# Pull the chat model (3B parameters - lightweight)
ollama pull llama3.2:3b

# Pull the embedding model
ollama pull nomic-embed-text

# Verify models are installed
ollama list
```

**Model Sizes:**

- `llama3.2:3b` - ~2GB (recommended for most systems)
- `nomic-embed-text` - ~274MB

**Alternative Models:**

- For more powerful responses: `ollama pull llama3.2:7b` (4.7GB)
- For faster responses: `ollama pull llama3.2:1b` (1.3GB)

### 3. Start Ollama Server

```bash
# Ollama runs as a service by default after installation
# Verify it's running:
curl http://localhost:11434/api/tags

# If not running, start it:
ollama serve
```

## Configuration

### appsettings.json

```json
{
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "llama3.2:3b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

**Configuration Options:**

- `BaseUrl`: Ollama server endpoint (default: http://localhost:11434)
- `ChatModel`: Model for chat completions (default: llama3.2:3b)
- `EmbeddingModel`: Model for embeddings (default: nomic-embed-text)

## API Endpoints

### WebSocket (SignalR)

**Hub URL:** `wss://localhost:7001/hubs/chatbot`

**Methods:**

- `SendMessage(string message, bool useSemanticSearch)` - Send a chat message
- `ClearHistory()` - Clear conversation history
- `GetHistory()` - Get conversation history
- `SearchKnowledge(string query, int limit)` - Search guitar knowledge base

**Events:**

- `Connected` - Connection established
- `ReceiveMessageChunk` - Streaming response chunk
- `MessageComplete` - Full response received
- `Error` - Error occurred

### REST API

#### POST /api/chatbot/chat

Send a message and get complete response (non-streaming)

**Request:**

```json
{
  "message": "Show me some easy beginner chords",
  "conversationHistory": [],
  "useSemanticSearch": true
}
```

**Response:**

```json
{
  "message": "Here are some easy beginner chords...",
  "timestamp": "2025-10-26T10:30:00Z"
}
```

#### POST /api/chatbot/chat/stream

Send a message and get streaming response (Server-Sent Events)

**Request:** Same as above

**Response:** Streaming text chunks via SSE

#### GET /api/chatbot/status

Check if chatbot is available

**Response:**

```json
{
  "isAvailable": true,
  "message": "Chatbot is ready",
  "timestamp": "2025-10-26T10:30:00Z"
}
```

#### GET /api/chatbot/examples

Get example queries

**Response:**

```json
[
  "Show me some easy beginner chords",
  "What are the modes of the major scale?",
  "Explain voice leading in jazz",
  ...
]
```

## Usage Examples

### WebSocket Client (JavaScript)

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("https://localhost:7001/hubs/chatbot")
    .withAutomaticReconnect()
    .build();

connection.on("ReceiveMessageChunk", (chunk) => {
    console.log("Chunk:", chunk);
});

connection.on("MessageComplete", (fullMessage) => {
    console.log("Complete:", fullMessage);
});

await connection.start();
await connection.invoke("SendMessage", "Show me some jazz chords", true);
```

### REST Client (cURL)

```bash
# Non-streaming
curl -X POST https://localhost:7001/api/chatbot/chat \
  -H "Content-Type: application/json" \
  -d '{"message":"What is a dominant 7th chord?","useSemanticSearch":true}'

# Streaming
curl -X POST https://localhost:7001/api/chatbot/chat/stream \
  -H "Content-Type: application/json" \
  -d '{"message":"Explain the circle of fifths","useSemanticSearch":true}'

# Check status
curl https://localhost:7001/api/chatbot/status
```

### C# Client

```csharp
using Microsoft.AspNetCore.SignalR.Client;

var connection = new HubConnectionBuilder()
    .WithUrl("https://localhost:7001/hubs/chatbot")
    .WithAutomaticReconnect()
    .Build();

connection.On<string>("ReceiveMessageChunk", chunk =>
{
    Console.Write(chunk);
});

await connection.StartAsync();
await connection.InvokeAsync("SendMessage", "Show me some chords", true);
```

## Demo Client

A complete HTML/JavaScript demo client is available at:
**https://localhost:7001/chatbot-demo.html**

Features:

- Real-time streaming responses
- Conversation history
- Typing indicators
- Beautiful gradient UI
- Mobile responsive

## Semantic Search Integration

The chatbot automatically enhances responses with relevant guitar knowledge:

1. **Query Analysis**: User message is analyzed for intent
2. **Vector Search**: Top 5 relevant documents retrieved from knowledge base
3. **Context Injection**: Top 3 results added to system prompt
4. **Enhanced Response**: LLM generates response with context

**Example:**

```
User: "Show me some dark jazz chords"

Semantic Search Results:
1. Minor 7th chords (score: 0.89)
2. Diminished chords (score: 0.85)
3. Half-diminished chords (score: 0.82)

Response: "For dark jazz sounds, try these chords:
- Cm7 (C-Eb-G-Bb) - Classic minor 7th
- Cdim7 (C-Eb-Gb-Bbb) - Fully diminished
- Cm7b5 (C-Eb-Gb-Bb) - Half-diminished..."
```

## Performance

**Response Times:**

- First token: ~500ms (model loading)
- Subsequent tokens: ~50-100ms each
- Total response (100 tokens): ~5-10 seconds

**Resource Usage:**

- RAM: ~4GB (model + embeddings)
- CPU: Moderate (depends on hardware)
- GPU: Optional (CUDA acceleration supported)

## Troubleshooting

### Ollama Not Running

```bash
# Check if Ollama is running
curl http://localhost:11434/api/tags

# Start Ollama
ollama serve
```

### Model Not Found

```bash
# List installed models
ollama list

# Pull missing model
ollama pull llama3.2:3b
ollama pull nomic-embed-text
```

### Connection Failed

- Ensure API is running: `dotnet run --project Apps/ga-server/GaApi`
- Check CORS settings in Program.cs
- Verify WebSocket support in firewall

### Slow Responses

- Use smaller model: `llama3.2:1b`
- Reduce context window
- Enable GPU acceleration (if available)

## Next Steps

1. **Populate Knowledge Base**: Index chord templates and voicings
2. **Add Function Calling**: Let LLM call specific guitar functions
3. **Implement Caching**: Cache frequent queries
4. **Add Authentication**: Secure WebSocket connections
5. **Deploy to Production**: Use reverse proxy (nginx/caddy)

## License

Part of the Guitar Alchemist project.

