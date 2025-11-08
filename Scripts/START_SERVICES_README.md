# Guitar Alchemist - Start All Services

## Overview

The `start-all.ps1` script provides a simple way to start all Guitar Alchemist services using .NET Aspire orchestration.

### What Gets Started

When you run this script, the **Aspire AppHost** starts and orchestrates all services:

1. **MongoDB** - Database with vector search capabilities
    - Includes **MongoExpress** UI for database management

2. **GaApi** - Main REST API server
    - Chord data endpoints
    - Vector search endpoints
    - Health checks
    - Swagger UI

3. **GuitarAlchemistChatbot** - Blazor chatbot application
    - AI-powered music theory assistant
    - Chord diagram rendering
    - Tab viewer
    - MCP integration

4. **ga-client** - React frontend
    - Material-UI components
    - Chord browser
    - Interactive UI

## Quick Start

### Start Everything (Recommended)

```powershell
# From repository root
.\Scripts\start-all.ps1
```

This will:

1. ✅ Build the entire solution
2. ✅ Start the Aspire AppHost
3. ✅ Start all services (MongoDB, GaApi, Chatbot, React frontend)
4. ✅ Display service URLs

### Common Usage

```powershell
# Skip build (faster during development)
.\Scripts\start-all.ps1 -NoBuild

# Open Aspire Dashboard automatically
.\Scripts\start-all.ps1 -Dashboard

# Combine options
.\Scripts\start-all.ps1 -NoBuild -Dashboard
```

## Service URLs

Once all services are running, you can access them at:

| Service              | URL                            | Description                     |
|----------------------|--------------------------------|---------------------------------|
| **Aspire Dashboard** | https://localhost:15001        | Service orchestration dashboard |
| **GaApi (Swagger)**  | https://localhost:7001/swagger | API documentation and testing   |
| **GaApi (Health)**   | https://localhost:7001/health  | Health check endpoint           |
| **Chatbot**          | https://localhost:7002         | Blazor chatbot application      |
| **React Frontend**   | http://localhost:5173          | React frontend application      |
| **MongoExpress**     | http://localhost:8081          | MongoDB management UI           |

**Note:** Actual ports may vary. Check the **Aspire Dashboard** for exact URLs.

## Aspire Dashboard

The **Aspire Dashboard** is your central control panel for all services:

### Features

- 📊 **Service Status** - See which services are running
- 📈 **Metrics** - CPU, memory, request rates
- 📝 **Logs** - Centralized logging from all services
- 🔍 **Traces** - Distributed tracing with OpenTelemetry
- 🌐 **Endpoints** - Quick links to all service URLs
- 🔄 **Dependencies** - Service dependency graph

### Accessing the Dashboard

1. **Automatic** - The dashboard URL is displayed when you start services
2. **Manual** - Navigate to https://localhost:15001
3. **Auto-open** - Use `.\Scripts\start-all.ps1 -Dashboard`

## Service Dependencies

The services start in the correct order based on dependencies:

```
MongoDB
  ↓
GaApi ← MongoDB
  ↓
Chatbot ← GaApi, MongoDB
  ↓
ga-client ← GaApi
```

Aspire handles this automatically - you don't need to worry about startup order!

## Stopping Services

### Graceful Shutdown

Press **Ctrl+C** in the terminal where the script is running.

This will:

1. Stop all services gracefully
2. Clean up resources
3. Close database connections
4. Shut down containers

### Force Stop

If services don't stop gracefully:

```powershell
# Kill all dotnet processes
Get-Process dotnet | Stop-Process -Force

# Stop Docker containers (if using Docker for MongoDB)
docker stop $(docker ps -q)
```

## Troubleshooting

### Port Already in Use

If you see "port already in use" errors:

```powershell
# Find process using port 7001 (GaApi)
netstat -ano | findstr :7001

# Kill the process (replace PID with actual process ID)
taskkill /PID <PID> /F
```

### MongoDB Connection Issues

If MongoDB fails to start:

```powershell
# Check if MongoDB container is running
docker ps

# Restart MongoDB container
docker restart <container-id>

# Or remove and let Aspire recreate it
docker rm -f <container-id>
```

### Build Errors

If the build fails:

```powershell
# Clean and rebuild
dotnet clean AllProjects.sln
dotnet build AllProjects.sln
```

### React Frontend Issues

If the React frontend fails to start:

```powershell
# Install dependencies
cd Apps/ga-client
pnpm install

# Or use npm
npm install
```

### Aspire Dashboard Not Opening

If the dashboard doesn't open:

1. Check the console output for the actual dashboard URL
2. Try accessing it manually: https://localhost:15001
3. Check if another process is using port 15001

## Development Workflow

### Typical Development Session

```powershell
# 1. Start all services
.\Scripts\start-all.ps1 -Dashboard

# 2. Make code changes in your editor

# 3. Services will hot-reload automatically:
#    - GaApi: Hot reload enabled
#    - Chatbot: Hot reload enabled
#    - React frontend: Vite hot module replacement (HMR)

# 4. When done, press Ctrl+C to stop all services
```

### Fast Iteration (Skip Build)

```powershell
# After first run, skip build for faster startup
.\Scripts\start-all.ps1 -NoBuild
```

### Testing Changes

```powershell
# 1. Start services
.\Scripts\start-all.ps1 -NoBuild

# 2. Run tests in another terminal
.\Scripts\run-all-tests.ps1 -BackendOnly

# 3. Test manually in browser
#    - API: https://localhost:7001/swagger
#    - Chatbot: https://localhost:7002
#    - Frontend: http://localhost:5173
```

## Configuration

### Environment Variables

Services use configuration from:

1. **appsettings.json** - Default configuration
2. **appsettings.Development.json** - Development overrides
3. **Environment variables** - Runtime overrides
4. **User secrets** - Sensitive data (API keys)

### Setting User Secrets

```powershell
# Set OpenAI API key for vector search
cd Apps/ga-server/GaApi
dotnet user-secrets set "OpenAI:ApiKey" "your-api-key-here"

# Set MongoDB connection string (if not using Aspire default)
dotnet user-secrets set "MongoDB:ConnectionString" "mongodb://localhost:27017"
```

### Aspire Configuration

Edit `AllProjects.AppHost/appsettings.json` to configure:

- Service ports
- Resource limits
- Environment variables
- Volume mounts

## Hot Reload

All services support hot reload during development:

### Backend (.NET)

- **GaApi** - Changes to C# code reload automatically
- **Chatbot** - Changes to Razor components reload automatically

### Frontend (React)

- **ga-client** - Vite HMR (Hot Module Replacement)
- Changes to React components update instantly
- No page refresh needed

## Monitoring

### Logs

View logs in the Aspire Dashboard:

1. Open https://localhost:15001
2. Click on a service
3. View **Logs** tab

### Metrics

View metrics in the Aspire Dashboard:

1. Open https://localhost:15001
2. Click on a service
3. View **Metrics** tab

### Traces

View distributed traces:

1. Open https://localhost:15001
2. Click **Traces** in the sidebar
3. View request flows across services

## Production Deployment

This script is for **development only**. For production:

### Option 1: Docker Compose

```powershell
# Build Docker images
dotnet publish AllProjects.AppHost -c Release

# Run with Docker Compose
docker-compose up -d
```

### Option 2: Kubernetes

```powershell
# Generate Kubernetes manifests
dotnet publish AllProjects.AppHost -c Release -p:PublishProfile=DefaultContainer

# Deploy to Kubernetes
kubectl apply -f manifests/
```

### Option 3: Azure Container Apps

```powershell
# Deploy to Azure
azd up
```

## Related Scripts

- **`Scripts/run-all-tests.ps1`** - Run all tests (backend + frontend)
- **`Scripts/test-chord-api.ps1`** - Test chord API endpoints
- **`Scripts/test-vector-search.ps1`** - Test vector search
- **`Scripts/import-to-mongodb.ps1`** - Import chord data to MongoDB

## Prerequisites

### Required

- ✅ .NET 9 SDK
- ✅ Node.js 18+ (for React frontend)
- ✅ pnpm or npm (for React frontend)
- ✅ Docker Desktop (for MongoDB container)

### Optional

- OpenAI API key (for vector search)
- MongoDB Atlas account (for cloud database)

## Performance Tips

### Faster Startup

1. **Skip build** - Use `-NoBuild` after first run
2. **Use SSD** - Store repository on SSD for faster builds
3. **Increase RAM** - Allocate more RAM to Docker Desktop

### Reduce Resource Usage

1. **Stop unused services** - Comment out services in `AllProjects.AppHost/Program.cs`
2. **Reduce log verbosity** - Edit `appsettings.Development.json`
3. **Limit Docker resources** - Configure Docker Desktop settings

## Support

### Common Issues

1. **Port conflicts** - See "Port Already in Use" section
2. **MongoDB issues** - See "MongoDB Connection Issues" section
3. **Build errors** - See "Build Errors" section

### Getting Help

1. Check the **Aspire Dashboard** for service status
2. View **logs** in the dashboard
3. Check **health endpoints** (e.g., https://localhost:7001/health)
4. Review **console output** for errors

## Architecture

### Aspire Orchestration

```
┌─────────────────────────────────────────┐
│         Aspire AppHost                  │
│  (AllProjects.AppHost/Program.cs)       │
└─────────────────────────────────────────┘
                    │
        ┌───────────┼───────────┬─────────┐
        ↓           ↓           ↓         ↓
    MongoDB      GaApi      Chatbot   ga-client
    (Docker)     (.NET)     (Blazor)  (React)
        │           │           │         │
        └───────────┴───────────┴─────────┘
              Service Discovery
           (Aspire Service Defaults)
```

### Service Communication

- **GaApi** ← MongoDB (database queries)
- **Chatbot** ← GaApi (API calls)
- **Chatbot** ← MongoDB (direct queries)
- **ga-client** ← GaApi (API calls)

All communication uses **service discovery** - no hardcoded URLs!

## Next Steps

After starting services:

1. ✅ **Explore the API** - https://localhost:7001/swagger
2. ✅ **Try the Chatbot** - https://localhost:7002
3. ✅ **Use the Frontend** - http://localhost:5173
4. ✅ **Monitor Services** - https://localhost:15001
5. ✅ **Run Tests** - `.\Scripts\run-all-tests.ps1`

Happy coding! 🎸

