# Technology Stack

## Core Platform
- **Runtime:** .NET 9
- **Orchestration:** .NET Aspire (`AllProjects.AppHost`)
- **Languages:** C# 12, F# 9, Python 3.11+, TypeScript 5+

## Backend Architecture
The system uses a microservices architecture orchestrated by .NET Aspire.

### Services (Managed by Aspire)
- **API Gateway:** `GaApi` (YARP Reverse Proxy)
- **Core Domain:** `GA.MusicTheory.Service` (Port 7001)
- **AI/ML:**
  - `ga-graphiti-service` (Python/FalkorDB) - Temporal Knowledge Graph
  - `hand-pose-service` (Python/MediaPipe) - Hand tracking
  - `sound-bank-service` (Python/MusicGen) - Audio synthesis
- **Frontend Servers:**
  - `ga-client` (Vite/React)
  - `GuitarAlchemistChatbot` (Blazor Hybrid)
  - `FloorManager` (BSP Viewer)
  - `ScenesService` (3D Scene Builder)

## Data Layer
- **Primary Document Store:** MongoDB (`guitar-alchemist` db) + MongoExpress UI
- **Caching:** Redis (`redis` container) + Redis Commander UI
- **Graph Database:** FalkorDB (`falkordb` container)

## AI & Machine Learning Stack
- **Frameworks:**
  - Microsoft.SemanticKernel (v1.38.0)
  - ILGPU (GPU acceleration)
  - Ollama (Local LLM)
- **Models:**
  - MusicGen (via `sound-bank-service`)
  - MediaPipe (via `hand-pose-service`)

## Frontend (ga-client)
- **Framework:** React 18+
- **Build Tool:** Vite
- **UI Library:** Material-UI (MUI)
- **State:** Jotai
- **3D Graphics:** Three.js / React Three Fiber (implied by BSP/Scene references)

## Development Tools
- **IDE:** VS Code, Visual Studio 2022, JetBrains Fleet
- **Containerization:** Docker Desktop
- **Shell:** PowerShell 7 (`pwsh`)