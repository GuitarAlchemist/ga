---
name: aspire-orchestration
description: "Use when touching AllProjects.AppHost/Program.cs, debugging service startup, or onboarding a new dependency. Knows GA's Aspire AppHost topology (MongoDB, Redis, FalkorDB, GaApi, microservices, ga-client, MCP, Python sidecars), how connection strings flow via WithReference, and how to add new .NET projects, Python containers, or infrastructure resources. Invoke for Aspire, AppHost, DistributedApplication, AddProject, AddRedis, AddMongoDB, WithReference, start-all.ps1, Aspire dashboard."
license: MIT
metadata:
  version: "1.0.0"
  domain: orchestration
  triggers: Aspire, AppHost, AllProjects.AppHost, DistributedApplication, AddProject, AddRedis, AddMongoDB, WithReference, start-all.ps1, Aspire dashboard
  role: specialist
  last_verified: 2026-05-06
  verified_against: "AllProjects.AppHost/Program.cs (.NET 10, Aspire 9.x)"
---

# Aspire Orchestration

Use this skill when you need to **add a service to the AppHost**, **debug why something fails to start**, **understand how connection strings flow**, or **explain the topology to someone new**.

## What Aspire owns in this repo

`AllProjects.AppHost/Program.cs` is the single source of truth for how the GA backend stack runs locally. Aspire builds a `DistributedApplication` that orchestrates every service Guitar Alchemist needs — infrastructure containers, .NET projects, the React frontend, the MCP server, and Python sidecars — and exposes the **Aspire dashboard at `https://localhost:15001`** for live status, logs, traces, and resource URLs.

The dashboard is the canonical place to discover ports, follow logs, and trigger restarts. Don't hardcode service URLs in client code or skill docs — read them from the dashboard or the configuration injected at runtime.

## Service inventory (current)

| Resource | Kind | Notes |
|---|---|---|
| `redis` | container | Redis + RedisCommander UI; data volume `ga-redis-data` |
| `falkordb` | container | Redis-compatible graph DB for Graphiti; **port 6380** (avoids conflict with Docker/WSL Redis on 6379); volume `ga-falkordb-data` |
| `mongodb` | container | MongoDB + MongoExpress UI; volume `ga-mongodb-data`; database `guitar-alchemist` |
| `graphiti-service` | Python container | Temporal knowledge graph; port 8000; bound to `mongoDatabase` + `falkordb` |
| `hand-pose-service` | Python container | MediaPipe hand pose; port 8081 |
| `sound-bank-service` | Python container | MusicGen-based sound gen; port 8082; bound to `redis` + `mongoDatabase`; sample volume `ga-sound-samples` |
| `music-theory-service` | .NET project | Microservice (port 7001 in launchSettings) |
| `gaapi` | .NET project | YARP API gateway; references `mongoDatabase`, `redis`, `musicTheoryService` |
| `scenes-service` | .NET project | GLB scene builder; references `mongoDatabase` |
| `floor-manager` | .NET project | BSP dungeon floor viewer |
| `ga-mcp-server` | .NET project | MCP server; references `mongoDatabase`, `redis` |
| `ga-client` | NPM app | React/Vite frontend; HTTP port 5173 (`PORT` env) |

Several other microservices (`bsp-service`, `ai-service`, `knowledge-service`, `fretboard-service`, `analytics-service`) are scaffolded but commented out in `Program.cs` until their implementations land. **If you uncomment one, also add it to GaApi's `.WithReference(...)` chain** so the gateway can route to it.

## How to start everything

```powershell
# Build + start all services (default)
pwsh Scripts/start-all.ps1 -Dashboard

# Start without rebuilding (daily dev)
pwsh Scripts/start-all.ps1 -NoBuild -Dashboard
```

The `-Dashboard` switch opens the Aspire dashboard in your browser. Without it, services still start; you'd just open `https://localhost:15001` manually.

## How connection strings flow

Aspire injects connection strings as environment variables of the form `ConnectionStrings__<resource-name>`:

- A reference to `mongodb`'s database (`AddDatabase("guitar-alchemist")`) becomes `ConnectionStrings__guitar-alchemist`
- A reference to `redis` becomes `ConnectionStrings__redis`

Inside a referenced project, read them via `IConfiguration.GetConnectionString("guitar-alchemist")` or DI'd `IOptions<...>`. **Never hardcode** the MongoDB URL, port, or database name — Aspire chooses dynamic ports for the underlying container, and hardcoded values will randomly break across restarts.

## Adding a new service

The dispatch to use depends on what kind of service you're adding:

### A new .NET project

```csharp
var newService = builder.AddProject("my-service", @"..\Apps\ga-server\My.Service\My.Service.csproj")
    .WithReference(mongoDatabase)        // any infra it needs
    .WithReference(redis)
    .WithExternalHttpEndpoints();        // makes its URL discoverable from the dashboard
```

Then if other services need to call it, add `.WithReference(newService)` to their `AddProject` calls. Almost always `gaapi` needs the new reference if the service is user-facing (gateway routes through it).

### A new Python container

```csharp
builder.AddContainer("my-py-service", "my-py-service")
    .WithDockerfile("../Apps/my-py-service")           // path relative to AppHost
    .WithHttpEndpoint(8083, 8080, "http")              // host port 8083, container port 8080
    .WithEnvironment("PORT", "8080")
    .WithReference(mongoDatabase)                       // injects ConnectionStrings__guitar-alchemist
    .WithExternalHttpEndpoints();
```

Pick a host port that doesn't collide with existing ones (8000 graphiti, 8081 hand-pose, 8082 sound-bank).

### A new infrastructure container

Use the Aspire `Add*` extensions where one exists (`AddRedis`, `AddMongoDB`, `AddPostgres`, etc.). For anything else, fall back to `AddContainer` with an explicit image name + port mapping, like `falkordb`.

## Common failure modes

| Symptom | Diagnosis | Fix |
|---|---|---|
| Port collision on startup | Two containers binding the same host port; or another local Redis/Mongo competing | Inspect `Program.cs` for the `WithHttpEndpoint(<host>, ...)` calls; reassign one to a free port |
| `Connection refused` from a service | Service references a sibling that hasn't started, or `WithReference` is missing | Verify the dashboard shows both as "Running"; verify `.WithReference(...)` chain in `Program.cs` |
| Missing connection string | Database not added to the resource (`AddDatabase` only on `AddMongoDB`-typed resources) | Use `mongodb.AddDatabase("guitar-alchemist")` and reference the returned handle, not the raw `mongodb` |
| Aspire dashboard 404 | Wrong scheme (HTTP vs HTTPS) or service didn't expose external endpoints | Check `.WithExternalHttpEndpoints()` is present on every user-facing project |
| Python container can't see Mongo | Hardcoded `localhost` in the container | Read `MONGODB_URI` from env (Aspire injects it via `WithEnvironment("MONGODB_URI", mongoDatabase)`) |

## What NOT to do

- **Don't add Aspire references in service code.** A service should not import the AppHost project. Cross-service contracts are HTTP/gRPC, not in-process.
- **Don't run GaApi from a Docker image when developing locally.** GaApi is a .NET project Aspire orchestrates as a process. Containerized GaApi belongs in the production deployment story, not the dev loop.
- **Don't put Ollama in the AppHost.** Ollama runs natively at `localhost:11434` per the `chat` skill — it's not Aspire-orchestrated.
- **Don't hardcode dynamic ports** in tests, frontend code, or skill docs. Read them from the dashboard or environment.

## Pointers

- AppHost: [`AllProjects.AppHost/Program.cs`](../../../AllProjects.AppHost/Program.cs)
- Startup script: [`Scripts/start-all.ps1`](../../../Scripts/start-all.ps1)
- Aspire docs: [learn.microsoft.com/dotnet/aspire](https://learn.microsoft.com/dotnet/aspire)
- Related skills: `chat` (uses Aspire to start the chatbot), `dotnet-core-expert` (broader .NET guidance), `optic-k-rebuild` (touches the data layer Aspire orchestrates)
