# Actor System Dependency Injection Fix

## Problem

The application failed to start with the following error:

```
System.InvalidOperationException: Unable to resolve service for type 'GaApi.Services.GuitarAgentOrchestrator' 
while attempting to activate 'GaApi.Services.ActorSystemManager'.
```

## Root Cause

**Singleton-Scoped Dependency Mismatch:**

- `ActorSystemManager` was registered as **Singleton**
- `GuitarAgentOrchestrator` was registered as **Scoped**
- A singleton service cannot depend on a scoped service (violates DI lifetime rules)

## Solution

Changed the actor system to use **IServiceProvider** pattern instead of direct dependency injection:

### **Before (Broken):**

```csharp
public class ActorSystemManager
{
    private readonly GuitarAgentOrchestrator _orchestrator;
    
    public ActorSystemManager(GuitarAgentOrchestrator orchestrator, ...)
    {
        _orchestrator = orchestrator; // ❌ Singleton depending on Scoped
    }
}

public class AIAgentTaskActor
{
    private readonly GuitarAgentOrchestrator _orchestrator;
    
    public AIAgentTaskActor(GuitarAgentOrchestrator orchestrator, ...)
    {
        _orchestrator = orchestrator; // ❌ Long-lived actor holding scoped service
    }
}
```

### **After (Fixed):**

```csharp
public class ActorSystemManager
{
    private readonly IServiceProvider _serviceProvider;
    
    public ActorSystemManager(IServiceProvider serviceProvider, ...)
    {
        _serviceProvider = serviceProvider; // ✅ Singleton can hold IServiceProvider
    }
    
    public PID CreateAgentTask(string taskId)
    {
        var props = Props.FromProducer(() => new AIAgentTaskActor(
            taskId,
            _serviceProvider, // ✅ Pass IServiceProvider to actor
            _loggerFactory.CreateLogger<AIAgentTaskActor>()));
        
        return _actorSystem.Root.Spawn(props);
    }
}

public class AIAgentTaskActor
{
    private readonly IServiceProvider _serviceProvider;
    
    public AIAgentTaskActor(IServiceProvider serviceProvider, ...)
    {
        _serviceProvider = serviceProvider; // ✅ Actor holds IServiceProvider
    }
    
    private async Task ExecuteTaskAsync(StartAgentTask msg)
    {
        // ✅ Create scope and resolve orchestrator when needed
        using var scope = _serviceProvider.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<IGuitarAgentOrchestrator>();
        
        var result = await orchestrator.SpiceUpProgressionAsync(...);
        // Scope is disposed after task completes
    }
}
```

## Why This Works

### **Service Provider Pattern Benefits:**

1. **Respects DI Lifetimes:**
   - Singleton (`ActorSystemManager`) holds `IServiceProvider` (also singleton)
   - Scoped services (`GuitarAgentOrchestrator`) are resolved only when needed
   - Each task execution creates a new scope → new orchestrator instance

2. **Proper Resource Management:**
   - Scoped services are disposed when scope ends
   - No memory leaks from long-lived actors holding scoped services
   - Each AI task gets a fresh orchestrator instance

3. **Follows Best Practices:**
   - Service Locator pattern (acceptable for framework-level code like actors)
   - Actors remain stateless regarding business logic dependencies
   - Clear separation between actor lifetime and service lifetime

## Files Changed

1. **Apps/ga-server/GaApi/Services/ActorSystemManager.cs**
   - Changed constructor parameter from `GuitarAgentOrchestrator` to `IServiceProvider`
   - Updated `CreateAgentTask` to pass `IServiceProvider` to actor

2. **Apps/ga-server/GaApi/Actors/AIAgentTaskActor.cs**
   - Changed constructor parameter from `GuitarAgentOrchestrator` to `IServiceProvider`
   - Updated `ExecuteTaskAsync` to create scope and resolve orchestrator
   - Added `using Microsoft.Extensions.DependencyInjection;`

## Testing

```bash
# Build succeeds
dotnet build Apps/ga-server/GaApi/GaApi.csproj -c Debug
# Build succeeded.

# Application starts without errors
dotnet run --project Apps/ga-server/GaApi/GaApi.csproj
# ✅ No DI errors
```

## Alternative Solutions Considered

### **Option 1: Make GuitarAgentOrchestrator Singleton** ❌
- **Problem:** Orchestrator has scoped dependencies (DbContext, HttpContext, etc.)
- **Impact:** Would break existing functionality

### **Option 2: Make ActorSystemManager Scoped** ❌
- **Problem:** Actor system should be singleton (one per application)
- **Impact:** Would create multiple actor systems, breaking isolation

### **Option 3: Use Factory Pattern** ⚠️
- **Complexity:** Requires creating `IGuitarAgentOrchestratorFactory`
- **Benefit:** More explicit than Service Locator
- **Decision:** Service Provider pattern is simpler for this use case

## Lessons Learned

1. **Always check DI lifetimes** when mixing singleton and scoped services
2. **Use IServiceProvider** when singletons need to resolve scoped services
3. **Create scopes explicitly** to ensure proper disposal
4. **Actors are long-lived** - don't inject scoped services directly

## Related Documentation

- [docs/ACTOR_SYSTEM_IMPLEMENTATION.md](ACTOR_SYSTEM_IMPLEMENTATION.md) - Full actor system architecture
- [docs/ACTOR_SYSTEM_STATUS.md](ACTOR_SYSTEM_STATUS.md) - Implementation status report

