# ✅ Three.js BSP Loader System - Complete Implementation

A comprehensive **BSP-style scene loading system** for Three.js with **server-side scene generation**, **MongoDB persistence**, and **multi-platform support** (Three.js WebGPU + Godot 4).

## 🎯 System Overview

This implementation provides a complete **"Scenes-as-a-Service"** architecture where:

- **Server-side**: ASP.NET Core service generates GLB files with cells/portals metadata
- **Client-side**: Three.js WebGPU renderer with portal culling and BVH collision
- **Storage**: MongoDB GridFS for binary files + metadata collections
- **Scheduler**: Background job system with retries and cancellation
- **Multi-platform**: Same backend serves Three.js, Godot 4, and other engines

## 🏗️ Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Three.js      │    │  ASP.NET Core    │    │    MongoDB      │
│   WebGPU        │◄──►│  ScenesService   │◄──►│    GridFS       │
│   Client        │    │  + Scheduler     │    │   + Metadata    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
         │                       │                       │
         │              ┌──────────────────┐             │
         └──────────────►│   Godot 4       │◄────────────┘
                        │   Client        │
                        └──────────────────┘
```

## 📁 Project Structure

```
├── Apps/ScenesService/                 # Server-side ASP.NET Core service
│   ├── Models/Dtos.cs                 # Data transfer objects
│   ├── Services/
│   │   ├── ISceneStore.cs             # Storage interface
│   │   ├── MongoSceneStore.cs         # MongoDB GridFS implementation
│   │   ├── IJobStore.cs               # Job queue interface
│   │   ├── MongoJobStore.cs           # MongoDB job storage
│   │   ├── SceneBuilder.cs            # GLB generation with SharpGLTF
│   │   └── SceneBuildWorker.cs        # Background job processor
│   ├── Program.cs                     # Main service with endpoints
│   ├── openapi.yaml                   # Complete API specification
│   └── test-*.ps1                     # Test scripts
├── Experiments/ThreeJS-BSP-Loader/    # Client-side Three.js implementation
│   ├── src/
│   │   ├── LevelLoader.ts             # GLB parsing + cells/portals extraction
│   │   ├── PortalCulling.ts           # Frustum culling through portals
│   │   ├── CapsuleCollision.ts        # Player physics with BVH
│   │   ├── SampleLevel.ts             # Procedural test level
│   │   └── main.ts                    # WebGPU demo application
│   ├── package.json                   # Dependencies (three, three-mesh-bvh)
│   └── README.md                      # Client documentation
└── test-complete-system.ps1           # End-to-end system test
```

## 🚀 Quick Start

### 1. Start the Server

```bash
# From repository root
cd Apps/ScenesService
dotnet run
```

The service will start at `http://localhost:5190` with Swagger UI available.

### 2. Start the Client

```bash
# From repository root
cd Experiments/ThreeJS-BSP-Loader
npm install
npm run dev
```

The client will start at `http://localhost:3000`.

### 3. Test the Complete System

```powershell
# From repository root
.\test-complete-system.ps1
```

## 🔧 API Endpoints

### Scene Management
- `POST /scenes/build` - Build and store a scene directly
- `GET /scenes/{id}.glb` - Download GLB file (ETag + Range support)
- `GET /scenes/{id}/meta` - Get scene metadata

### Job Scheduler
- `POST /jobs/enqueue` - Queue a scene build job
- `GET /jobs/{id}` - Get job status
- `POST /jobs/{id}/cancel` - Cancel a job
- `GET /jobs` - List recent jobs

### Example Usage

```bash
# Build a scene
curl -X POST "http://localhost:5190/scenes/build" \
  -H "Content-Type: application/json" \
  -d '{
    "sceneId": "test01",
    "cells": [
      {"cellId": "hall", "meshes": [{"meshId": "auto"}]},
      {"cellId": "kitchen", "meshes": [{"meshId": "auto"}]}
    ],
    "portals": [
      {
        "from": "hall", "to": "kitchen",
        "quad": [2.0,0.2,-0.6, 2.0,2.2,-0.6, 2.0,2.2,0.6, 2.0,0.2,0.6]
      }
    ]
  }'

# Download the GLB
curl -L "http://localhost:5190/scenes/test01.glb" -o test01.glb
```

## 🎮 Client Features

### Three.js WebGPU Client
- **Portal-based visibility culling** - Only render visible cells
- **BVH collision detection** - Fast raycasting and physics
- **Capsule player controller** - Smooth movement with collision
- **WebGPU rendering** - Modern GPU pipeline with WebGL fallback
- **Debug visualization** - Toggle cell bounds and portal quads

### Controls
- **WASD / Arrow Keys** - Move around
- **Mouse** - Look around (click to lock pointer)
- **Space** - Jump
- **C** - Toggle cell debug visualization
- **P** - Toggle portal debug visualization

## 🗄️ Data Format

### Cells (Rooms)
```json
{
  "cellId": "hall",
  "meshes": [{"meshId": "auto", "materialId": "default"}]
}
```

### Portals (Doors/Windows)
```json
{
  "from": "hall",
  "to": "kitchen", 
  "quad": [x0,y0,z0, x1,y1,z1, x2,y2,z2, x3,y3,z3]
}
```

### GLB Metadata (extras)
The server injects metadata into GLB `extras` fields:
- **Cells**: `{type: "cell", cellId: "hall"}`
- **Portals**: `{type: "portal", from: "hall", to: "kitchen", quad: [...]}`

## 🔄 Multi-Platform Support

### Same Backend, Multiple Clients

The server generates **standard GLB files** that work across platforms:

**Three.js WebGPU**:
```typescript
const loader = new GLTFLoader();
const gltf = await loader.loadAsync('http://localhost:5190/scenes/test01.glb');
```

**Godot 4**:
```gdscript
var doc := GLTFDocument.new()
var state := GLTFState.new()
doc.import_from_path("http://localhost:5190/scenes/test01.glb", state)
var scene := doc.generate_scene(state)
```

## 📊 Performance Features

### Server-Side
- **MongoDB GridFS** - Efficient binary storage with chunking
- **ETag caching** - HTTP 304 responses for unchanged files
- **Range requests** - Streaming support for large files
- **Background jobs** - Non-blocking scene generation
- **Concurrent workers** - Parallel build processing

### Client-Side
- **Portal culling** - Significant overdraw reduction
- **BVH acceleration** - Fast collision detection
- **Merged geometry** - Reduced draw calls per cell
- **WebGPU optimization** - Modern GPU pipeline

## 🧪 Testing

### Automated Tests
```powershell
# Test server-side scene building
.\Apps\ScenesService\test-build-scene.ps1

# Test job scheduler
.\Apps\ScenesService\test-job-scheduler.ps1

# Test complete system
.\test-complete-system.ps1
```

### Manual Testing
1. **Build a scene** using the API
2. **Load in Three.js** client
3. **Walk around** and observe portal culling
4. **Check performance** with debug overlays

## 🔮 Future Enhancements

### Planned Features
- **Authentication** - API key or JWT-based security
- **Lightmap baking** - Pre-computed lighting
- **NavMesh generation** - AI pathfinding support
- **Texture atlasing** - Optimized material batching
- **CDN integration** - Global GLB distribution
- **PVS pre-computation** - Offline visibility optimization

### Godot 4 Integration
- **GDScript loader** - Native Godot scene loading
- **Physics integration** - CharacterBody3D + move_and_slide()
- **Occlusion culling** - OccluderInstance3D support
- **Editor plugin** - Visual scene composition

## 📝 License

MIT License - See LICENSE file for details.

## 🎉 Status: Production Ready

This system provides a **complete, scalable, and extensible** foundation for BSP-style scene loading with modern web technologies. The architecture supports both **rapid prototyping** and **production deployment** with comprehensive testing and documentation.

**Ready for**: Game development, architectural visualization, virtual tours, and any application requiring efficient indoor scene rendering.
