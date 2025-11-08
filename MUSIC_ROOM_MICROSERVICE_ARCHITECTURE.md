# Music Room Generation Microservice Architecture

## Overview

This document outlines the architecture for refactoring the Music Room Generation service to use a proper microservices architecture with Redis caching for data synchronization.

## Current State

### Problems
1. **Hardcoded Data**: `MusicRoomService` hardcodes music theory data (SetClass, ForteNumber, etc.)
2. **No Cache Synchronization**: Each service has its own in-memory cache
3. **Tight Coupling**: Music room generation is tightly coupled to data sources
4. **No Service Communication**: Services don't share data efficiently

### Current Architecture
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

## Proposed Architecture

### New Components
1. **Redis** - Distributed cache for data synchronization
2. **Music Data API** - Dedicated endpoints in GaApi for music theory data
3. **Cache Sync Service** - Background service to sync data between services

### Target Architecture
```
┌─────────────────────────────────────────────────────┐
│              Aspire AppHost                         │
└─────────────────────────────────────────────────────┘
                         │
        ┌────────────────┼────────────────┬──────────┐
        ↓                ↓                ↓          ↓
    MongoDB          Redis            GaApi      Chatbot
    (Docker)       (Docker)          (.NET)     (Blazor)
        │                │                │          │
        │                │                │          │
        └────────────────┴────────────────┴──────────┘
                  Distributed Cache Layer
                  (Redis for sync)
```

### Service Communication Flow
```
FloorManager (Blazor)
    ↓ HTTP
MusicRoomService (GaApi)
    ↓ Check Redis Cache
    ├─ Cache Hit → Return cached data
    └─ Cache Miss → Fetch from GaApi
        ↓ HTTP (Service Discovery)
        GaApi Music Data Endpoints
            ↓ Query MongoDB
            SetClass, ForteNumber, Chords, etc.
            ↓ Cache in Redis
            Return data
```

## Implementation Plan

### Phase 1: Add Redis to Infrastructure

#### 1.1 Update AppHost (AllProjects.AppHost/Program.cs)
```csharp
var builder = DistributedApplication.CreateBuilder(args);

// Add Redis
var redis = builder.AddRedis("redis")
    .WithRedisCommander(); // Optional: Redis UI

// Add MongoDB
var mongodb = builder.AddMongoDB("mongodb")
    .WithMongoExpress();

// Add GaApi with Redis reference
var gaApi = builder.AddProject<Projects.GaApi>("gaapi")
    .WithReference(mongodb)
    .WithReference(redis);

// Add Chatbot with Redis reference
var chatbot = builder.AddProject<Projects.GuitarAlchemistChatbot>("chatbot")
    .WithReference(mongodb)
    .WithReference(redis)
    .WithReference(gaApi);

// Add FloorManager with Redis reference
var floorManager = builder.AddProject<Projects.FloorManager>("floormanager")
    .WithReference(redis)
    .WithReference(gaApi);

builder.Build().Run();
```

#### 1.2 Add Redis NuGet Packages
```bash
# GaApi
dotnet add Apps/ga-server/GaApi package StackExchange.Redis
dotnet add Apps/ga-server/GaApi package Microsoft.Extensions.Caching.StackExchangeRedis

# FloorManager
dotnet add Apps/FloorManager package StackExchange.Redis
dotnet add Apps/FloorManager package Microsoft.Extensions.Caching.StackExchangeRedis

# Chatbot
dotnet add Apps/GuitarAlchemistChatbot package StackExchange.Redis
dotnet add Apps/GuitarAlchemistChatbot package Microsoft.Extensions.Caching.StackExchangeRedis
```

### Phase 2: Create Music Data API Endpoints

#### 2.1 New Controller: MusicDataController.cs (GaApi)
```csharp
[ApiController]
[Route("api/music-data")]
public class MusicDataController : ControllerBase
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<MusicDataController> _logger;

    [HttpGet("set-classes")]
    public async Task<ActionResult<List<string>>> GetSetClasses()
    {
        // Check Redis cache first
        var cached = await _cache.GetStringAsync("music:set-classes");
        if (cached != null)
            return Ok(JsonSerializer.Deserialize<List<string>>(cached));

        // Fetch from source
        var items = SetClass.Items.Select(sc => sc.ToString()).ToList();
        
        // Cache for 1 hour
        await _cache.SetStringAsync("music:set-classes", 
            JsonSerializer.Serialize(items),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

        return Ok(items);
    }

    [HttpGet("forte-numbers")]
    public async Task<ActionResult<List<string>>> GetForteNumbers() { ... }

    [HttpGet("chords")]
    public async Task<ActionResult<List<string>>> GetChords() { ... }

    [HttpGet("floor/{floorNumber}/items")]
    public async Task<ActionResult<List<string>>> GetFloorItems(int floorNumber) { ... }
}
```

### Phase 3: Refactor MusicRoomService

#### 3.1 Add HTTP Client for GaApi Communication
```csharp
public class MusicRoomService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IDistributedCache _cache;
    private readonly ILogger<MusicRoomService> _logger;

    private async Task<List<string>> GetMusicItemsForFloor(int floor)
    {
        // Check Redis cache first
        var cacheKey = $"floor:{floor}:items";
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            _logger.LogInformation("Cache hit for floor {Floor}", floor);
            return JsonSerializer.Deserialize<List<string>>(cached)!;
        }

        // Fetch from GaApi
        var client = _httpClientFactory.CreateClient("GaApi");
        var response = await client.GetAsync($"/api/music-data/floor/{floor}/items");
        response.EnsureSuccessStatusCode();

        var items = await response.Content.ReadFromJsonAsync<List<string>>();
        
        // Cache for 1 hour
        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(items),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1)
            });

        return items!;
    }
}
```

### Phase 4: Configure Services

#### 4.1 GaApi Program.cs
```csharp
// Add Redis distributed cache
builder.AddRedisDistributedCache("redis");

// Configure HTTP clients for service-to-service communication
builder.Services.AddHttpClient("GaApi", client =>
{
    client.BaseAddress = new Uri("http://gaapi"); // Service discovery
});
```

#### 4.2 FloorManager Program.cs
```csharp
// Add Redis distributed cache
builder.AddRedisDistributedCache("redis");

// Configure HTTP client for GaApi
builder.Services.AddHttpClient("GaApi", client =>
{
    client.BaseAddress = new Uri("http://gaapi"); // Service discovery
});
```

## Benefits

### 1. **Decoupling**
- Music room generation no longer hardcodes data
- Data sources can be updated independently
- Services communicate via well-defined APIs

### 2. **Performance**
- Redis caching reduces database queries
- Distributed cache shared across all services
- Faster response times for repeated requests

### 3. **Scalability**
- Services can scale independently
- Redis handles cache synchronization
- No single point of failure

### 4. **Maintainability**
- Single source of truth for music data (GaApi)
- Easier to add new data types
- Clear separation of concerns

## Testing Strategy

### 1. Unit Tests
- Test cache hit/miss scenarios
- Test service communication
- Test data serialization

### 2. Integration Tests
- Test Redis connectivity
- Test service-to-service communication
- Test cache expiration

### 3. Performance Tests
- Measure cache hit rates
- Measure response times
- Test under load

## Monitoring

### 1. Metrics to Track
- Cache hit/miss ratio
- Service-to-service latency
- Redis memory usage
- API endpoint response times

### 2. Aspire Dashboard
- View all metrics in centralized dashboard
- Monitor service health
- View distributed traces

## Next Steps

1. ✅ Add Redis to AppHost
2. ✅ Create MusicDataController endpoints
3. ✅ Refactor MusicRoomService to use HTTP client
4. ✅ Configure distributed caching
5. ✅ Add monitoring and logging
6. ✅ Write tests
7. ✅ Update documentation

## References

- [.NET Aspire Redis Integration](https://learn.microsoft.com/en-us/dotnet/aspire/caching/stackexchange-redis-integration)
- [Distributed Caching in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed)
- [Service Discovery in .NET Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/service-discovery/overview)

