# Guitar Alchemist - Microservices Architecture

## Overview

This document describes the microservices architecture for Guitar Alchemist, splitting the monolithic GaApi into focused, independently deployable services.

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                         API Gateway (GaApi)                          │
│              (Routing, Auth, Rate Limiting, CORS)                    │
│                    Port: 7000 (HTTPS), 5000 (HTTP)                   │
└─────────────────────────────────────────────────────────────────────┘
                                  │
        ┌─────────────────────────┼─────────────────────────┐
        │                         │                         │
┌───────▼────────┐      ┌────────▼────────┐      ┌────────▼────────┐
│ Music Theory   │      │   BSP/Spatial   │      │  AI/ML Service  │
│   Service      │      │    Service      │      │                 │
│  Port: 7001    │      │   Port: 7002    │      │   Port: 7003    │
│                │      │                 │      │                 │
│ • Keys/Scales  │      │ • BSP Trees     │      │ • Embeddings    │
│ • Modes        │      │ • Rooms         │      │ • Semantic      │
│ • Intervals    │      │ • Spatial Query │      │ • Ollama        │
│ • Chords       │      │ • Tonal Context │      │ • Vector Search │
└────────────────┘      └─────────────────┘      └─────────────────┘
        │                         │                         │
┌───────▼────────┐      ┌────────▼────────┐      ┌────────▼────────┐
│ Knowledge Base │      │   Fretboard     │      │   Analytics     │
│   Service      │      │    Service      │      │    Service      │
│  Port: 7004    │      │   Port: 7005    │      │   Port: 7006    │
│                │      │                 │      │                 │
│ • YAML Configs │      │ • Analysis      │      │ • Spectral      │
│ • Techniques   │      │ • Voicings      │      │ • Grothendieck  │
│ • Tunings      │      │ • Ergonomics    │      │ • Invariants    │
│ • Progressions │      │ • Biomechanics  │      │ • Advanced Math │
└────────────────┘      └─────────────────┘      └─────────────────┘
```

## Service Breakdown

### 1. API Gateway (GaApi) - Port 7000
**Responsibility**: Thin routing layer, authentication, rate limiting, CORS
**Controllers**: None (pure gateway)
**Dependencies**: All microservices
**Technology**: ASP.NET Core with YARP (Yet Another Reverse Proxy)

### 2. Music Theory Service - Port 7001
**Responsibility**: Core music theory primitives and operations
**Controllers**:
- MusicTheoryController (keys, modes, scales, intervals)
- ChordsController (chord generation, analysis)
- DslController (music theory DSL)

**Dependencies**:
- GA.Business.Core
- GA.MusicTheory.DSL
- GA.Core

### 3. BSP/Spatial Service - Port 7002
**Responsibility**: Binary Space Partitioning and spatial analysis
**Controllers**:
- BSPController (spatial queries, tonal context)
- BSPRoomController (room generation)
- MusicRoomController (music theory rooms)
- IntelligentBSPController (intelligent BSP generation)

**Dependencies**:
- GA.BSP.Core
- GA.Business.Orchestration
- GA.Data.MongoDB

### 4. AI/ML Service - Port 7003
**Responsibility**: AI/ML operations, embeddings, semantic search
**Controllers**:
- SemanticSearchController (semantic search)
- VectorSearchController (vector search)
- VectorSearchStrategyController (search strategies)
- AdvancedAIController (advanced AI features)
- AdaptiveAIController (adaptive learning)

**Dependencies**:
- GA.Business.AI
- GA.Business.Intelligence
- GA.Data.SemanticKernel.Embeddings
- OllamaSharp

### 5. Knowledge Base Service - Port 7004
**Responsibility**: YAML configuration management and musical knowledge
**Controllers**:
- MusicalKnowledgeController (search, categories, artists)
- GuitarTechniquesController (techniques catalog)
- SpecializedTuningsController (tuning configurations)
- AssetsController (asset management)
- AssetRelationshipsController (asset relationships)

**Dependencies**:
- GA.Business.Config
- GA.Business.Assets
- GA.Data.EntityFramework

### 6. Fretboard Service - Port 7005
**Responsibility**: Guitar-specific analysis and biomechanics
**Controllers**:
- GuitarPlayingController (hand pose, sound generation)
- BiomechanicsController (biomechanical analysis)
- ContextualChordsController (contextual chord generation)
- ChordProgressionsController (progression analysis)
- MonadicChordsController (monadic chord operations)

**Dependencies**:
- GA.Business.Fretboard
- GA.Business.Core.Fretboard
- HandPoseClient
- SoundBankClient

### 7. Analytics Service - Port 7006
**Responsibility**: Advanced mathematical analysis
**Controllers**:
- SpectralAnalyticsController (spectral analysis)
- GrothendieckController (Grothendieck theory)
- InvariantsController (musical invariants)
- AdvancedAnalyticsController (advanced analytics)
- MetricsController (metrics and monitoring)

**Dependencies**:
- GA.Business.Analytics
- GA.Business.Intelligence

### 8. Existing External Services
**Already Deployed**:
- hand-pose-service (Python, Port 8001)
- sound-bank-service (Python, Port 8002)
- ga-graphiti-service (Python, Port 8000)
- ScenesService (C#, Port 7007)
- FloorManager (C#, Port 7008)

## Shared Infrastructure

### MongoDB
- **Port**: 27017
- **Used By**: All services for persistence
- **Collections**: Organized by service domain

### Redis
- **Port**: 6379
- **Used By**: All services for caching and distributed state
- **Purpose**: Response caching, rate limiting, session management

### Aspire Dashboard
- **Port**: 15001
- **Purpose**: Service orchestration, monitoring, health checks

## Communication Patterns

### Service-to-Service Communication
- **Protocol**: HTTP/HTTPS with service discovery
- **Discovery**: Aspire service discovery (`https+http://service-name`)
- **Resilience**: Polly retry policies, circuit breakers
- **Timeout**: 30 seconds default

### API Gateway Routing
```csharp
// Example routing configuration
app.MapReverseProxy(proxyPipeline =>
{
    proxyPipeline.Use((context, next) =>
    {
        // Add correlation ID
        // Add authentication
        // Add rate limiting
        return next();
    });
});
```

### Service Registration
```csharp
// In AllProjects.AppHost/Program.cs
var musicTheory = builder.AddProject("music-theory-service", "...");
var bsp = builder.AddProject("bsp-service", "...");
var ai = builder.AddProject("ai-service", "...");

// API Gateway references all services
builder.AddProject("gaapi", "...")
    .WithReference(musicTheory)
    .WithReference(bsp)
    .WithReference(ai)
    // ... other services
```

## Migration Strategy

### Phase 1: Create Service Projects
1. Create 6 new ASP.NET Core Web API projects
2. Copy relevant controllers from GaApi
3. Configure dependencies and NuGet packages
4. Add Aspire ServiceDefaults

### Phase 2: Fix Dependencies
1. Re-enable all excluded controllers
2. Implement missing services
3. Fix compilation errors
4. Add proper DI registration

### Phase 3: Configure Gateway
1. Install YARP in GaApi
2. Configure routes to microservices
3. Add authentication/authorization
4. Add rate limiting

### Phase 4: Update Orchestration
1. Register all services in AppHost
2. Configure service discovery
3. Set up health checks
4. Configure monitoring

### Phase 5: Testing
1. Unit tests for each service
2. Integration tests for gateway
3. End-to-end tests
4. Performance testing

## Benefits

1. **Independent Scaling**: Scale AI service separately from music theory
2. **Technology Diversity**: Use best language/framework for each domain
3. **Team Autonomy**: Different teams own different services
4. **Fault Isolation**: Service failures don't cascade
5. **Deployment Independence**: Deploy services independently
6. **Clear Boundaries**: Single responsibility per service
7. **Better Testing**: Easier to test isolated services
8. **Performance**: Optimize each service independently

## Challenges

1. **Distributed Transactions**: Need saga pattern for multi-service operations
2. **Data Consistency**: Eventual consistency model required
3. **Network Latency**: More network hops
4. **Debugging**: Distributed tracing required (OpenTelemetry)
5. **Deployment Complexity**: More services to deploy
6. **Service Discovery**: Need reliable service registry

## Monitoring & Observability

### Metrics
- Request rate per service
- Response time per endpoint
- Error rate per service
- Resource utilization (CPU, memory)

### Logging
- Structured logging with Serilog
- Centralized log aggregation
- Correlation IDs for request tracing

### Tracing
- OpenTelemetry for distributed tracing
- Jaeger for trace visualization
- Span correlation across services

### Health Checks
- Liveness probes (is service running?)
- Readiness probes (can service handle requests?)
- Dependency health (MongoDB, Redis, external services)

## Security

### Authentication
- JWT tokens issued by API Gateway
- Token validation in each service
- Service-to-service authentication

### Authorization
- Role-based access control (RBAC)
- Policy-based authorization
- Scope-based permissions

### Network Security
- HTTPS for all external communication
- mTLS for service-to-service communication
- Network policies in Kubernetes

## Next Steps

1. ✅ Create architecture document (this file)
2. ⏳ Create service project templates
3. ⏳ Migrate controllers to services
4. ⏳ Implement API Gateway with YARP
5. ⏳ Update Aspire orchestration
6. ⏳ Add monitoring and observability
7. ⏳ Write integration tests
8. ⏳ Deploy and validate

