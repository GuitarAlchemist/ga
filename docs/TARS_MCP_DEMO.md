# TARS MCP Server - Live Demo 🤖

**What TARS MCP can do for Guitar Alchemist development**

---

## 🎯 Available Tools (10 Total)

### **System Diagnostics** (Most Useful for GA)

#### 1️⃣ `get_gpu_info` - GPU/CUDA Information
**Input:** None required  
**Returns:**
```json
{
  "name": "NVIDIA GeForce RTX 3080",
  "memoryTotal": 10240,
  "memoryUsed": 2048,
  "memoryFree": 8192,
  "temperature": 45,
  "cudaSupported": true,
  "driverVersion": "531.68"
}
```
**Use Case:** Check if CUDA is available for Semantic Kernel GPU acceleration

---

#### 2️⃣ `get_system_resources` - System Resource Metrics
**Input:** None required  
**Returns:**
```json
{
  "cpuUsage": 23.5,
  "memoryUsage": 67.2,
  "diskUsage": 45.8,
  "processCount": 342,
  "uptime": 86400
}
```
**Use Case:** Monitor system performance during development, identify resource bottlenecks

---

#### 3️⃣ `get_service_health` - Service Status Checks
**Input:** None required  
**Returns:**
```json
{
  "environmentVariables": {
    "NODE_ENV": "development",
    "MONGODB_URI": "mongodb://localhost:27017",
    "REDIS_URL": "redis://localhost:6379"
  },
  "portsListening": [27017, 6379, 5176, 5001],
  "servicesRunning": ["mongod", "redis-server", "node", "dotnet"]
}
```
**Use Case:** Verify MongoDB, Redis, GaApi, and React dev server are running

---

#### 4️⃣ `get_network_diagnostics` - Network Connectivity
**Input:** None required  
**Returns:**
```json
{
  "isConnected": true,
  "publicIpAddress": "203.0.113.42",
  "dnsResolutionTime": 15,
  "pingLatency": 12.5,
  "downloadSpeed": 100.5,
  "uploadSpeed": 50.2,
  "activeConnections": 47
}
```
**Use Case:** Check internet connectivity, diagnose network issues

---

#### 5️⃣ `get_git_health` - Repository Health
**Input:**
```json
{
  "repositoryPath": "C:/Users/spare/source/repos/ga"
}
```
**Returns:**
```json
{
  "currentBranch": "main",
  "uncommittedChanges": 5,
  "commitsAhead": 2,
  "commitsBehind": 0,
  "remoteUrl": "https://github.com/GuitarAlchemist/ga.git",
  "lastCommit": {
    "hash": "abc123",
    "message": "feat: Add 3D hand visualization",
    "author": "Stephane Pareilleux",
    "date": "2025-11-06T20:30:00Z"
  }
}
```
**Use Case:** Check repository status, uncommitted changes, sync status

---

#### 6️⃣ `get_comprehensive_diagnostics` - Complete System Overview
**Input:**
```json
{
  "repositoryPath": "C:/Users/spare/source/repos/ga"  // Optional
}
```
**Returns:** ALL of the above in one comprehensive report!
```json
{
  "gpu": { /* GPU info */ },
  "systemResources": { /* System resources */ },
  "serviceHealth": { /* Service health */ },
  "network": { /* Network diagnostics */ },
  "git": { /* Git health */ }
}
```
**Use Case:** Get complete system snapshot in one call

---

### **TARS-Specific Tools** (Not applicable to GA)

#### 7️⃣ `get_tars_project_info` - TARS Project Analysis
**Input:**
```json
{
  "projectPath": "C:/path/to/tars/project"
}
```
**Use Case:** Analyze TARS projects (not applicable to Guitar Alchemist)

---

#### 8️⃣ `execute_tars_command` - Run TARS CLI
**Input:**
```json
{
  "command": "tars build",
  "workingDirectory": "C:/path/to/tars/project"
}
```
**Use Case:** Execute TARS commands (not applicable to Guitar Alchemist)

---

#### 9️⃣ `build_tars_project` - Build TARS Projects
**Input:**
```json
{
  "projectPath": "C:/path/to/tars/project"
}
```
**Use Case:** Build TARS projects (not applicable to Guitar Alchemist)

---

#### 🔟 `run_tars_tests` - Run TARS Tests
**Input:**
```json
{
  "projectPath": "C:/path/to/tars/project"
}
```
**Use Case:** Run TARS tests (not applicable to Guitar Alchemist)

---

## 🎸 Real-World Examples for Guitar Alchemist

### **Example 1: Check if CUDA is available for Semantic Kernel**
```
You: "Use TARS MCP to check if I have CUDA support"

TARS Response:
✅ GPU: NVIDIA GeForce RTX 3080
✅ CUDA Supported: Yes
✅ Driver Version: 531.68
✅ Memory: 8192 MB free / 10240 MB total
```

### **Example 2: Verify all services are running**
```
You: "Check if MongoDB and Redis are running using TARS MCP"

TARS Response:
✅ MongoDB: Running on port 27017
✅ Redis: Running on port 6379
✅ GaApi: Running on port 5001
✅ React Dev Server: Running on port 5176
```

### **Example 3: Monitor system during heavy computation**
```
You: "Show me current system resources with TARS MCP"

TARS Response:
📊 CPU Usage: 23.5%
📊 Memory Usage: 67.2%
📊 Disk Usage: 45.8%
📊 Processes: 342
⏱️ Uptime: 24 hours
```

### **Example 4: Check Git repository status**
```
You: "Use TARS MCP to check the health of the GA repository"

TARS Response:
🌿 Branch: main
📝 Uncommitted Changes: 5 files
⬆️ Commits Ahead: 2
⬇️ Commits Behind: 0
🔗 Remote: https://github.com/GuitarAlchemist/ga.git
📅 Last Commit: "feat: Add 3D hand visualization" (2 hours ago)
```

### **Example 5: Complete system diagnostic**
```
You: "Run comprehensive diagnostics with TARS MCP for the GA project"

TARS Response:
🤖 COMPREHENSIVE SYSTEM DIAGNOSTICS

GPU:
  ✅ NVIDIA GeForce RTX 3080 (CUDA supported)
  📦 8192 MB free / 10240 MB total

System Resources:
  📊 CPU: 23.5% | Memory: 67.2% | Disk: 45.8%
  🔢 Processes: 342 | Uptime: 24h

Services:
  ✅ MongoDB (port 27017)
  ✅ Redis (port 6379)
  ✅ GaApi (port 5001)
  ✅ React Dev (port 5176)

Network:
  ✅ Connected | Ping: 12.5ms | DNS: 15ms

Git (GA Repository):
  🌿 main | 5 uncommitted | 2 ahead | 0 behind
  📅 Last: "feat: Add 3D hand visualization"
```

---

## 💡 When to Use TARS MCP

### **Before Starting Development:**
- ✅ Check if MongoDB and Redis are running
- ✅ Verify CUDA support for Semantic Kernel
- ✅ Check Git repository status

### **During Development:**
- ✅ Monitor system resources during heavy operations
- ✅ Verify services are still running
- ✅ Check network connectivity

### **Before Committing:**
- ✅ Check uncommitted changes
- ✅ Verify repository sync status
- ✅ Check if all services are healthy

### **Troubleshooting:**
- ✅ Diagnose service failures
- ✅ Check port conflicts
- ✅ Verify environment variables
- ✅ Check network issues

---

## 🚀 How to Use

Just ask me naturally:

```
"Check my GPU with TARS MCP"
"Show system resources using TARS"
"Verify MongoDB and Redis are running"
"Check Git status with TARS"
"Run comprehensive diagnostics"
```

I'll call the appropriate TARS MCP tool and format the results for you!

---

## 📊 Summary

| Tool | Usefulness for GA | Use Case |
|------|-------------------|----------|
| `get_gpu_info` | ⭐⭐⭐⭐⭐ | Check CUDA for Semantic Kernel |
| `get_system_resources` | ⭐⭐⭐⭐⭐ | Monitor development performance |
| `get_service_health` | ⭐⭐⭐⭐⭐ | Verify MongoDB, Redis, ports |
| `get_git_health` | ⭐⭐⭐⭐⭐ | Repository status |
| `get_network_diagnostics` | ⭐⭐⭐ | Network troubleshooting |
| `get_comprehensive_diagnostics` | ⭐⭐⭐⭐⭐ | Complete system overview |
| TARS-specific tools | ⭐ | Not applicable to GA |

---

**TARS MCP is now ready to help with Guitar Alchemist development! 🤖✨**

