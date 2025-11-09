# Guitar Alchemist Architecture

## System Overview

Guitar Alchemist is a comprehensive AI-powered music theory and guitar learning platform built with .NET 9, React, and cutting-edge AI technologies.

### Core Components

1. **Backend Services** (`Apps/ga-server/GaApi`)
   - REST/GraphQL API for music theory operations
   - Semantic search with embeddings
   - Chord and scale generation
   - Fretboard analysis and optimization
   - AI orchestration and agent services

2. **Frontend** (`Apps/ga-client`)
   - React-based UI with TypeScript
   - Interactive fretboard visualization
   - Real-time chord and scale exploration
   - Responsive design for desktop and tablet

3. **Chatbot** (`Apps/GuitarAlchemistChatbot`)
   - Blazor-based conversational interface
   - Ollama integration for local LLM
   - Semantic search over chord database
   - Function calling for music theory operations

4. **Data Layer**
   - MongoDB for chord and scale storage
   - Vector embeddings for semantic search
   - Semantic Kernel integration for AI operations

## Layered Architecture

```
┌─────────────────────────────────────────┐
│  Layer 5: Orchestration                 │
│  (GA.Business.Core.Orchestration)       │
│  - High-level workflows                 │
│  - Agent coordination                   │
└─────────────────────────────────────────┘
         ↓ depends on
┌─────────────────────────────────────────┐
│  Layer 4: AI/ML                         │
│  (GA.Business.Core.AI)                  │
│  - Semantic indexing                    │
│  - Vector search                        │
│  - LLM integration                      │
└─────────────────────────────────────────┘
         ↓ depends on
┌─────────────────────────────────────────┐
│  Layer 3: Analysis                      │
│  (GA.Business.Core.Analysis)            │
│  - Spectral analysis                    │
│  - Dynamical systems                    │
│  - Topological analysis                 │
└─────────────────────────────────────────┘
         ↓ depends on
┌─────────────────────────────────────────┐
│  Layer 2: Domain                        │
│  (Harmony, Fretboard, etc.)             │
│  - Chord theory                         │
│  - Scale modes                          │
│  - Fretboard logic                      │
└─────────────────────────────────────────┘
         ↓ depends on
┌─────────────────────────────────────────┐
│  Layer 1: Core                          │
│  (GA.Business.Core)                     │
│  - Note, Interval, PitchClass           │
│  - Fundamental primitives               │
└─────────────────────────────────────────┘
```

## Key Technologies

- **.NET 9 RC2**: Latest .NET runtime with performance improvements
- **Semantic Kernel**: Microsoft's AI orchestration framework
- **Ollama**: Local LLM inference (llama3.2:3b, nomic-embed-text)
- **MongoDB**: Document database for chord/scale storage
- **ILGPU**: GPU acceleration for vector operations
- **React + TypeScript**: Modern frontend framework
- **Blazor**: Server-side UI for chatbot
- **Aspire**: .NET distributed application orchestration

## Data Flow

### Chord Search Pipeline
1. User query → Frontend
2. Frontend → GaApi REST endpoint
3. GaApi → Semantic search service
4. Semantic search → Vector embeddings
5. Vector search → MongoDB
6. Results → Frontend visualization

### Chatbot Pipeline
1. User message → Chatbot UI
2. Chatbot → Ollama LLM
3. LLM → Function calling (chord search, theory explanation)
4. Results → Semantic search
5. Response → User

## Deployment Architecture

- **Development**: Local Aspire orchestration with all services
- **Production**: Containerized services with Kubernetes orchestration
- **Monitoring**: Jaeger for distributed tracing, health checks for service status

## Security Considerations

- API authentication via JWT tokens
- MongoDB connection strings in environment variables
- Ollama runs locally (no external API calls)
- Vector embeddings stored securely in MongoDB
- CORS configured for frontend access

