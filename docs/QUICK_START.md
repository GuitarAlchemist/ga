# Quick Start Guide

## Prerequisites

- **.NET 9 SDK** (RC2 or later)
- **Node.js 18+** and npm/pnpm
- **MongoDB** (local or Atlas)
- **Ollama** (for local LLM and embeddings)
- **Git**

## Installation

### 1. Clone the Repository

```bash
git clone https://github.com/GuitarAlchemist/ga.git
cd ga
```

### 2. Setup Development Environment

```powershell
# Run the setup script (Windows)
pwsh Scripts/setup-dev-environment.ps1

# Or manually:
dotnet restore AllProjects.slnx
npm ci --prefix ReactComponents/ga-react-components
```

### 3. Configure Services

Create `appsettings.Development.json` in `Apps/ga-server/GaApi/`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"
  },
  "Embeddings": {
    "Endpoint": "http://localhost:11434",
    "ModelName": "nomic-embed-text"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "llama3.2:3b",
    "EmbeddingModel": "nomic-embed-text"
  }
}
```

## Running Services

### Option 1: Start All Services (Recommended)

```powershell
# Start with Aspire dashboard
pwsh Scripts/start-all.ps1 -Dashboard

# Access:
# - Frontend: http://localhost:5173
# - API: https://localhost:7001
# - Aspire Dashboard: https://localhost:15001
# - Jaeger: http://localhost:16686
# - Mongo Express: http://localhost:8081
```

### Option 2: Start Individual Services

```powershell
# Terminal 1: Backend API
cd Apps/ga-server/GaApi
dotnet run

# Terminal 2: Frontend
cd ReactComponents/ga-react-components
npm run dev

# Terminal 3: Chatbot
cd Apps/GuitarAlchemistChatbot
dotnet run
```

### Option 3: Start with Docker

```bash
docker-compose up -d
```

## First Steps

### 1. Verify Installation

```powershell
# Build the solution
dotnet build AllProjects.slnx -c Debug

# Run tests
dotnet test AllProjects.slnx
```

### 2. Populate Database

```powershell
# Generate and import chord data
cd Apps/GaDataCLI
dotnet run -- -e chords -o ./export

# Import to MongoDB
mongoimport --db guitar-alchemist --collection chords --file export/all-chords.json --jsonArray
```

### 3. Generate Embeddings

```powershell
# Generate embeddings for chords
cd Apps/LocalEmbedding
dotnet run
```

### 4. Test the API

```bash
# Get all chords
curl https://localhost:7001/api/chords

# Search for chords
curl "https://localhost:7001/api/chords/search?query=major"

# Semantic search
curl "https://localhost:7001/api/semantic-search/search?query=dark%20jazz%20chords"
```

## Common Tasks

### Build

```powershell
# Full solution
dotnet build AllProjects.slnx -c Debug

# Specific project
dotnet build Apps/ga-server/GaApi/GaApi.csproj -c Debug

# Release build
dotnet build AllProjects.slnx -c Release
```

### Test

```powershell
# All tests
dotnet test AllProjects.slnx

# Backend only
pwsh Scripts/run-all-tests.ps1 -BackendOnly

# Specific test
dotnet test --filter "FullyQualifiedName~ChordTests"
```

### Frontend Development

```bash
cd ReactComponents/ga-react-components

# Development server
npm run dev

# Build for production
npm run build

# Lint
npm run lint

# Type check
npm run type-check
```

### Database

```bash
# Connect to MongoDB
mongosh

# View databases
show dbs

# Use guitar-alchemist database
use guitar-alchemist

# View collections
show collections

# Count chords
db.chords.countDocuments()
```

## Troubleshooting

### Port Already in Use

```powershell
# Find process using port
netstat -ano | findstr :7001

# Kill process
taskkill /PID <PID> /F
```

### MongoDB Connection Failed

```powershell
# Check if MongoDB is running
mongosh

# Start MongoDB (if using local installation)
mongod
```

### Ollama Not Available

```bash
# Install Ollama from https://ollama.ai
# Start Ollama
ollama serve

# Pull required models
ollama pull llama3.2:3b
ollama pull nomic-embed-text
```

### NuGet Cache Issues

```powershell
# Clear NuGet cache
dotnet nuget locals all --clear

# Restore packages
dotnet restore AllProjects.slnx
```

## Next Steps

- Read [AGENTS.md](./AGENTS.md) for repository guidelines
- Check [ARCHITECTURE.md](./ARCHITECTURE.md) for system design
- Review [BUILD_STATUS_2025.md](./BUILD_STATUS_2025.md) for build info
- Explore [MODULAR_RESTRUCTURING_PLAN.md](./MODULAR_RESTRUCTURING_PLAN.md) for architecture details

## Getting Help

- Check existing issues on GitHub
- Review test files for usage examples
- Read inline code documentation
- Check Aspire dashboard for service health

