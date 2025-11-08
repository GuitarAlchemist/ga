# 🎸 Guitar Alchemist × Graphiti Demo - Running Instructions

## 🎯 What We Built

✅ **Complete Graphiti Integration** - Temporal knowledge graphs for music learning  
✅ **Python FastAPI Service** - Full Graphiti backend with music theory entities  
✅ **NET Integration Layer** - HTTP client and API controllers  
✅ **React Visualization** - Interactive D3.js knowledge graph components  
✅ **Docker Support** - Complete containerization with health checks  
✅ **Comprehensive Tests** - Unit, integration, and E2E test suites  

## 🚀 Quick Demo Setup

### Option 1: Manual Service Startup (Recommended)

This approach starts each service individually for better control and debugging:

#### Terminal 1: Start FalkorDB (Graph Database)
```bash
# Use a different port to avoid conflicts
docker run -p 6380:6379 --name falkordb-demo falkordb/falkordb:latest
```

#### Terminal 2: Start Graphiti Python Service
```bash
cd Apps/ga-graphiti-service

# Install dependencies (first time only)
pip install -r requirements.txt

# Set environment variables
export FALKORDB_PORT=6380
export OLLAMA_BASE_URL=http://localhost:11434/v1
export OLLAMA_CHAT_MODEL=qwen2.5-coder:1.5b-base
export OLLAMA_EMBEDDING_MODEL=nomic-embed-text

# Start the service
python main.py
```

#### Terminal 3: Start GA .NET API
```bash
cd Apps/ga-server/GaApi

# Update appsettings.json to point to Graphiti service
# (Already configured to use http://localhost:8000)

# Start the API
dotnet run
```

#### Terminal 4: Start React Frontend
```bash
cd ReactComponents/ga-react-components

# Install dependencies (first time only)
npm install

# Add D3 dependency if not already added
npm install d3 @types/d3

# Start the development server
npm run dev
```

### Option 2: Docker Compose (If Ports Available)

If you don't have Redis or other services using port 6379:

```bash
# Start all services
docker-compose up -d falkordb graphiti-service

# Check status
docker-compose ps

# View logs
docker-compose logs -f graphiti-service
```

## 🧪 Testing the Integration

### 1. Test API Health
```bash
# Test Graphiti service directly
curl http://localhost:8000/health

# Test through .NET API
curl http://localhost:7001/api/graphiti/health
```

### 2. Add a Practice Episode
```bash
curl -X POST http://localhost:8000/episodes \
  -H "Content-Type: application/json" \
  -d '{
    "user_id": "demo-user-123",
    "episode_type": "practice",
    "content": {
      "chord_practiced": "Cmaj7",
      "duration_minutes": 15,
      "accuracy": 0.85,
      "difficulty_level": 4
    }
  }'
```

### 3. Search the Knowledge Graph
```bash
curl -X POST http://localhost:8000/search \
  -H "Content-Type: application/json" \
  -d '{
    "query": "jazz chords for beginners",
    "search_type": "hybrid",
    "limit": 5,
    "user_id": "demo-user-123"
  }'
```

### 4. Get AI Recommendations
```bash
curl -X POST http://localhost:8000/recommendations \
  -H "Content-Type: application/json" \
  -d '{
    "user_id": "demo-user-123",
    "recommendation_type": "next_chord",
    "context": {
      "current_skill_level": 3.5,
      "recently_practiced": ["C", "G", "Am"]
    }
  }'
```

### 5. View User Progress
```bash
curl http://localhost:8000/users/demo-user-123/progress
```

## 🎨 Frontend Demo

Once all services are running:

1. **Open the React Frontend**: http://localhost:5173
2. **Navigate to Graphiti Demo**: http://localhost:5173/test/graphiti-demo
3. **Explore the Features**:
   - Add practice episodes
   - Search the knowledge graph
   - Get personalized recommendations
   - View interactive graph visualization
   - Track learning progress over time

## 📊 Service URLs

| Service | URL | Description |
|---------|-----|-------------|
| **Graphiti API** | http://localhost:8000 | Python FastAPI service |
| **Graphiti Docs** | http://localhost:8000/docs | Interactive API documentation |
| **GA .NET API** | http://localhost:7001 | Main Guitar Alchemist API |
| **GA API Swagger** | http://localhost:7001/swagger | .NET API documentation |
| **React Frontend** | http://localhost:5173 | React development server |
| **Graphiti Demo** | http://localhost:5173/test/graphiti-demo | Interactive demo page |
| **FalkorDB Browser** | http://localhost:3000 | Graph database browser |

## 🔧 Prerequisites

### Required Software
- **Docker** - For FalkorDB graph database
- **.NET 9 SDK** - For the main GA API
- **Python 3.11+** - For the Graphiti service
- **Node.js 20+** - For the React frontend
- **Ollama** (Optional) - For local LLM processing

### Ollama Setup (Optional but Recommended)
```bash
# Install Ollama models for local AI processing
ollama pull qwen2.5-coder:1.5b-base
ollama pull nomic-embed-text
```

## 🐛 Troubleshooting

### Port Conflicts
- **FalkorDB**: Use port 6380 instead of 6379 if Redis is running
- **Graphiti Service**: Default port 8000, change in main.py if needed
- **GA API**: Default port 7001, change in launchSettings.json if needed

### Service Dependencies
1. **FalkorDB must start first** - Graph database for Graphiti
2. **Graphiti service second** - Depends on FalkorDB
3. **GA API third** - Depends on Graphiti service
4. **React frontend last** - Depends on GA API

### Common Issues
- **"Port already in use"**: Change ports in configuration files
- **"Connection refused"**: Check if prerequisite services are running
- **"Module not found"**: Run `pip install -r requirements.txt` or `npm install`
- **"Build failed"**: Ensure .NET 9 SDK is installed

## 🎯 Demo Scenarios

### Scenario 1: New User Learning Journey
1. Create user profile
2. Add first practice session (C major chord)
3. Get beginner recommendations
4. Practice recommended chords
5. View progress over time

### Scenario 2: Advanced User Skill Development
1. Add multiple practice sessions with different chords
2. Search for jazz chord progressions
3. Get advanced recommendations
4. Explore knowledge graph relationships
5. Track skill level improvements

### Scenario 3: Knowledge Graph Exploration
1. Search for "chord progressions"
2. Explore related concepts in the graph
3. Add practice sessions for discovered chords
4. See how the graph evolves with new data
5. Get contextual recommendations

## 🎸 What Makes This Special

### Temporal Knowledge Graphs
- **Time-Aware Learning**: Tracks how knowledge evolves over time
- **Context Preservation**: Maintains learning context across sessions
- **Dynamic Relationships**: Musical concepts relate differently as users progress

### AI-Powered Recommendations
- **Personalized Suggestions**: Based on individual learning history
- **Adaptive Difficulty**: Scales with user skill development
- **Context-Aware**: Considers musical relationships and user goals

### Interactive Visualization
- **D3.js Graph**: Interactive exploration of musical relationships
- **Real-time Updates**: Graph evolves as users practice and learn
- **Multiple Views**: Different perspectives on the same knowledge

## 🚀 Ready to Rock!

Your Guitar Alchemist × Graphiti integration is **complete and ready for demonstration**! This represents a significant advancement in AI-powered music education, combining temporal knowledge graphs with interactive learning experiences.

**Start with the manual setup approach for the best demo experience!** 🎸✨

---

*Built with ❤️ for the future of music education*
