# Guitar Alchemist × Graphiti Integration

## 🎯 Overview

This service integrates [Graphiti](https://github.com/getzep/graphiti) temporal knowledge graphs into the Guitar Alchemist ecosystem, providing advanced AI-powered music learning capabilities with temporal awareness.

## ✨ Features

### 🧠 Temporal Knowledge Graphs
- **Real-time Learning Tracking**: Capture user practice sessions, progress, and skill development over time
- **Dynamic Music Theory Relationships**: Model complex relationships between chords, scales, progressions, and user interactions
- **Historical Context**: Maintain complete learning history with temporal queries

### 🎵 Music-Specific Entities
- **Chord Entities**: Track chord knowledge, difficulty levels, and fingering patterns
- **Scale Entities**: Model scales, modes, and their relationships
- **User Entities**: Maintain user profiles with skill levels and learning preferences
- **Progression Entities**: Capture chord progressions and their contexts
- **Session Entities**: Record practice sessions with detailed metrics

### 🔍 Advanced Search & Recommendations
- **Hybrid Search**: Combines semantic embeddings, keyword search (BM25), and graph traversal
- **Personalized Recommendations**: AI-powered suggestions based on learning history
- **Context-Aware Queries**: Temporal queries that understand skill progression over time

### 🚀 Performance & Scalability
- **Local LLM Support**: Uses Ollama for cost-effective local processing
- **Graph Database Options**: Supports FalkorDB, Neo4j, and other graph databases
- **Efficient Retrieval**: Sub-second query performance with intelligent caching

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   React Frontend │    │   .NET Backend   │    │ Python Graphiti │
│                 │    │                  │    │    Service      │
│ • Graph Viz     │◄──►│ • REST API       │◄──►│ • Knowledge     │
│ • User Interface│    │ • SignalR Hubs   │    │   Graph         │
│ • Learning UI   │    │ • MongoDB        │    │ • Temporal      │
└─────────────────┘    │ • Vector Search  │    │   Queries       │
                       └──────────────────┘    │ • FalkorDB      │
                                               └─────────────────┘
```

## 🚀 Quick Start

### Prerequisites

1. **Python 3.11+** with pip or uv
2. **Ollama** running locally with models:
   ```bash
   ollama pull qwen2.5-coder:1.5b-base
   ollama pull nomic-embed-text
   ```
3. **FalkorDB** or **Neo4j** (FalkorDB included in Docker Compose)

### Installation

1. **Install Dependencies**:
   ```bash
   pip install -r requirements.txt
   # or
   uv sync
   ```

2. **Configure Environment**:
   ```bash
   cp .env.example .env
   # Edit .env with your configuration
   ```

3. **Start with Docker Compose**:
   ```bash
   # From project root
   docker-compose up falkordb graphiti-service
   ```

4. **Or Run Locally**:
   ```bash
   cd Apps/ga-graphiti-service
   python main.py
   ```

### Verify Installation

```bash
curl http://localhost:8000/health
```

## 📚 API Endpoints

### Episodes
- `POST /episodes` - Add learning episodes
- `GET /users/{user_id}/progress` - Get user progress

### Search & Recommendations
- `POST /search` - Search knowledge graph
- `POST /recommendations` - Get personalized recommendations

### Graph Management
- `GET /graph/stats` - Get graph statistics
- `POST /graph/sync` - Sync from MongoDB

## 🎮 Usage Examples

### Adding a Practice Session

```python
import requests

episode = {
    "user_id": "user123",
    "episode_type": "practice",
    "content": {
        "chord_practiced": "Cmaj7",
        "duration_minutes": 15,
        "accuracy": 0.85,
        "difficulty_level": 4
    }
}

response = requests.post("http://localhost:8000/episodes", json=episode)
```

### Searching the Knowledge Graph

```python
search_request = {
    "query": "jazz chords for beginners",
    "search_type": "hybrid",
    "limit": 10,
    "user_id": "user123"
}

response = requests.post("http://localhost:8000/search", json=search_request)
results = response.json()
```

### Getting Recommendations

```python
rec_request = {
    "user_id": "user123",
    "recommendation_type": "next_chord",
    "context": {
        "current_skill_level": 3.5,
        "recently_practiced": ["C", "G", "Am"]
    }
}

response = requests.post("http://localhost:8000/recommendations", json=rec_request)
recommendations = response.json()
```

## 🔧 Configuration

### Environment Variables

```bash
# Graphiti Configuration
OPENAI_API_KEY=your_openai_api_key_here
GRAPHITI_TELEMETRY_ENABLED=false
SEMAPHORE_LIMIT=10

# Graph Database (FalkorDB)
FALKORDB_HOST=localhost
FALKORDB_PORT=6379
FALKORDB_DATABASE=graphiti

# Ollama Configuration
OLLAMA_BASE_URL=http://localhost:11434/v1
OLLAMA_CHAT_MODEL=qwen2.5-coder:1.5b-base
OLLAMA_EMBEDDING_MODEL=nomic-embed-text

# MongoDB (for data sync)
MONGODB_CONNECTION_STRING=mongodb://localhost:27017
MONGODB_DATABASE_NAME=guitar-alchemist
```

### .NET Integration

The service integrates with the main GA API through HTTP clients:

```csharp
// In Program.cs
builder.Services.Configure<GraphitiOptions>(
    builder.Configuration.GetSection("Graphiti"));

builder.Services.AddHttpClient<IGraphitiService, GraphitiService>();
```

## 🎨 Frontend Integration

### React Components

1. **GraphitiKnowledgeGraph**: Interactive D3.js visualization
2. **GraphitiDemo**: Comprehensive demo interface
3. **Integration with existing GA components**

### Usage in React

```tsx
import { GraphitiKnowledgeGraph } from './components/GraphitiKnowledgeGraph';

<GraphitiKnowledgeGraph
  userId="user123"
  width={800}
  height={600}
  onNodeClick={(node) => console.log('Node clicked:', node)}
  onLinkClick={(link) => console.log('Link clicked:', link)}
/>
```

## 🧪 Testing

### Run Tests

```bash
# Python service tests
pytest tests/

# .NET integration tests
dotnet test Common/GA.Business.Core.Graphiti.Tests/
```

### Manual Testing

1. **Start all services**:
   ```bash
   docker-compose up
   ```

2. **Access the demo**:
   - Frontend: http://localhost:5173
   - Graphiti API: http://localhost:8000
   - FalkorDB Browser: http://localhost:3000

3. **Test the workflow**:
   - Add practice episodes
   - Search the knowledge graph
   - Get recommendations
   - View progress over time

## 🚀 Deployment

### Docker Production

```bash
# Build and deploy
docker-compose -f docker-compose.prod.yml up -d
```

### Environment-Specific Configuration

- **Development**: Uses local Ollama and FalkorDB
- **Production**: Can use cloud LLM APIs and managed graph databases
- **Testing**: Uses in-memory databases and mock services

## 🔍 Monitoring & Debugging

### Health Checks

```bash
# Service health
curl http://localhost:8000/health

# Graph statistics
curl http://localhost:8000/graph/stats
```

### Logs

```bash
# Docker logs
docker-compose logs graphiti-service

# Local logs
tail -f logs/graphiti-service.log
```

## 🤝 Contributing

1. **Fork the repository**
2. **Create a feature branch**
3. **Add tests for new functionality**
4. **Submit a pull request**

### Development Setup

```bash
# Install development dependencies
pip install -r requirements-dev.txt

# Run linting
black . && flake8 .

# Run tests
pytest tests/ -v
```

## 📄 License

This project is part of the Guitar Alchemist ecosystem and follows the same licensing terms.

## 🆘 Support

- **Issues**: Create GitHub issues for bugs and feature requests
- **Documentation**: Check the main GA project documentation
- **Community**: Join the GA Discord for discussions

---

**Built with ❤️ for the Guitar Alchemist community**
