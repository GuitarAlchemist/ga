# Guitar Alchemist - Startup Guide

This guide explains how to start the Guitar Alchemist application with both backend and frontend services.

## Prerequisites

- **.NET 9 SDK** - For building and running the backend
- **Node.js 18+** - For running the frontend
- **pnpm** - Package manager for Node.js (will be installed automatically if missing)
- **PowerShell 5.0+** - For running the startup scripts

## Quick Start

### Start Everything (Backend + Frontend)

```powershell
.\Scripts\start-all-dev.ps1
```

This will:
1. Build the .NET solution
2. Install frontend dependencies
3. Start the backend (Aspire AppHost with dashboard)
4. Start the frontend development server

Both services will run in separate terminal windows.

### Start Only Backend

```powershell
.\Scripts\start-backend.ps1
```

This will:
1. Build the .NET solution
2. Start the Aspire AppHost with all backend services

**Access the Aspire Dashboard:** https://localhost:15001

### Start Only Frontend

```powershell
.\Scripts\start-frontend.ps1
```

This will:
1. Install frontend dependencies (if needed)
2. Start the frontend development server

**Access the Frontend:** http://localhost:5173

## Script Options

### start-all-dev.ps1

```powershell
.\Scripts\start-all-dev.ps1 -SkipBuild -SkipInstall -DashboardPort 15001 -FrontendPort 5173
```

- `-SkipBuild` - Skip building the .NET solution (useful for quick restarts)
- `-SkipInstall` - Skip installing frontend dependencies
- `-DashboardPort` - Port for Aspire Dashboard (default: 15001)
- `-FrontendPort` - Port for frontend dev server (default: 5173)

### start-backend.ps1

```powershell
.\Scripts\start-backend.ps1 -SkipBuild -Dashboard
```

- `-SkipBuild` - Skip building the .NET solution
- `-Dashboard` - Enable Aspire Dashboard (default: true)

### start-frontend.ps1

```powershell
.\Scripts\start-frontend.ps1 -SkipInstall -Port 5173
```

- `-SkipInstall` - Skip installing dependencies
- `-Port` - Port for dev server (default: 5173)

## Services

### Backend Services

When you start the backend, the following services are orchestrated by Aspire:

- **GaApi** - Main REST API
- **GuitarAlchemistChatbot** - Chatbot service
- **MongoDB** - Document database
- **Redis** - Cache and message broker
- **Other services** - As configured in AllProjects.AppHost

**Dashboard:** https://localhost:15001

### Frontend Services

- **ga-client** - Main React application
- **ga-react-components** - Shared component library

**Development Server:** http://localhost:5173

## Troubleshooting

### Port Already in Use

If you get a "port already in use" error:

1. **For backend (port 15001):**
   ```powershell
   .\Scripts\start-backend.ps1 -DashboardPort 15002
   ```

2. **For frontend (port 5173):**
   ```powershell
   .\Scripts\start-frontend.ps1 -Port 5174
   ```

### Build Failures

If the build fails:

1. Clean the solution:
   ```powershell
   dotnet clean AllProjects.sln
   ```

2. Restore packages:
   ```powershell
   dotnet restore AllProjects.sln
   ```

3. Try building again:
   ```powershell
   dotnet build AllProjects.sln -c Debug
   ```

### Frontend Dependencies Issues

If you encounter frontend dependency issues:

1. Clear pnpm cache:
   ```powershell
   pnpm store prune
   ```

2. Reinstall dependencies:
   ```powershell
   cd Apps/ga-client
   pnpm install
   ```

## Development Workflow

1. **Start all services:**
   ```powershell
   .\Scripts\start-all-dev.ps1
   ```

2. **Access the application:**
   - Frontend: http://localhost:5173
   - Aspire Dashboard: https://localhost:15001

3. **Make changes:**
   - Frontend changes auto-reload in the browser
   - Backend changes require rebuilding and restarting

4. **Stop services:**
   - Press Ctrl+C in each terminal window

## Next Steps

- Check the [README.md](../README.md) for project overview
- Review [AGENTS.md](../AGENTS.md) for repository guidelines
- See [BUILD_STATUS_2025.md](../docs/BUILD_STATUS_2025.md) for build status

