# Service Registration Guidelines

## Overview

This document provides guidelines for registering services in the Guitar Alchemist (GA) project using dependency injection (DI) with Microsoft.Extensions.DependencyInjection.

## Core Principles

### 1. Use Extension Methods for Service Registration

All service registrations should be organized into extension methods grouped by feature area. This improves:
- **Readability**: Program.cs remains clean and focused
- **Maintainability**: Related services are grouped together
- **Testability**: Extension methods can be tested independently
- **Reusability**: Extension methods can be shared across projects

### 2. Extension Method Placement

**Rule**: Extension methods must be in the same project as the services they register.

**Correct**:
```
Apps/ga-server/GaApi/
├── Services/
│   ├── VectorSearchService.cs
│   └── CachingService.cs
└── Extensions/
    ├── VectorSearchServiceExtensions.cs
    └── CachingServiceExtensions.cs
```

**Incorrect** (causes circular dependencies):
```
Common/GA.Business.Core/
└── Extensions/
    └── VectorSearchServiceExtensions.cs  ❌ References GaApi services
```

### 3. Naming Conventions

- **Extension class**: `{Feature}ServiceExtensions.cs`
- **Extension method**: `Add{Feature}Services(this IServiceCollection services)`
- **Namespace**: Same as the project (e.g., `GaApi.Extensions`)

**Example**:
```csharp
namespace GaApi.Extensions;

public static class ChordServiceExtensions
{
    public static IServiceCollection AddChordServices(this IServiceCollection services)
    {
        // Service registrations
        return services;
    }
}
```

## Service Lifetime Rules

Choose the appropriate service lifetime based on the service's characteristics:

### Singleton (`AddSingleton`)

**Use when**:
- Service is stateless
- Service is expensive to create
- Service is thread-safe
- Service maintains application-wide state (caches, factories)

**Examples**:
```csharp
// Caches
services.AddSingleton<ICachingService, CachingService>();

// Factories
services.AddSingleton<IChordFactory, ChordFactory>();

// Strategy managers
services.AddSingleton<VectorSearchStrategyManager>();

// Analyzers
services.AddSingleton<IProgressionAnalyzer, ProgressionAnalyzer>();
```

**Characteristics**:
- Created once per application lifetime
- Shared across all requests
- Must be thread-safe
- Should not depend on scoped services

### Scoped (`AddScoped`)

**Use when**:
- Service maintains per-request state
- Service works with database contexts
- Service needs to be isolated per HTTP request

**Examples**:
```csharp
// Services with per-request state
services.AddScoped<IChordService, ChordService>();
services.AddScoped<IContextualChordService, ContextualChordService>();

// Repository pattern
services.AddScoped<IChordRepository, ChordRepository>();

// Database contexts
services.AddScoped<ApplicationDbContext>();
```

**Characteristics**:
- Created once per HTTP request (or scope)
- Disposed at end of request
- Can depend on singleton services
- Should not be injected into singleton services

### Transient (`AddTransient`)

**Use when**:
- Service is lightweight
- Service is stateful and not thread-safe
- Service is used infrequently
- Each consumer needs its own instance

**Examples**:
```csharp
// Commands and queries
services.AddTransient<IChordQuery, ChordQuery>();
services.AddTransient<IProgressionCommand, ProgressionCommand>();

// Lightweight utilities
services.AddTransient<IValidator, Validator>();
```

**Characteristics**:
- Created every time it's requested
- Not shared between consumers
- Disposed when scope ends
- Can depend on singleton or scoped services

## Extension Method Structure

### Basic Template

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace GaApi.Extensions;

/// <summary>
/// Extension methods for registering {feature} services.
/// </summary>
public static class {Feature}ServiceExtensions
{
    /// <summary>
    /// Adds {feature} services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection Add{Feature}Services(this IServiceCollection services)
    {
        // Register services
        services.AddSingleton<IService1, Service1>();
        services.AddScoped<IService2, Service2>();
        services.AddTransient<IService3, Service3>();
        
        return services;
    }
}
```

### Advanced Template with Configuration

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GaApi.Extensions;

public static class {Feature}ServiceExtensions
{
    public static IServiceCollection Add{Feature}Services(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<{Feature}Options>(
            configuration.GetSection("{Feature}"));
        
        // Register services
        services.AddSingleton<IService, Service>();
        
        return services;
    }
}
```

## XML Documentation

All extension methods must include XML documentation:

```csharp
/// <summary>
/// Adds chord services to the service collection.
/// Registers IChordService, IContextualChordService, IVoicingFilterService, and IModulationService.
/// </summary>
/// <param name="services">The service collection.</param>
/// <returns>The service collection for chaining.</returns>
public static IServiceCollection AddChordServices(this IServiceCollection services)
{
    // Implementation
}
```

## Usage in Program.cs

### Before (Inline Registration)

```csharp
// Program.cs - BAD
var builder = WebApplication.CreateBuilder(args);

// 100+ lines of inline service registrations
builder.Services.AddSingleton<IChordService, ChordService>();
builder.Services.AddSingleton<IChordFactory, ChordFactory>();
builder.Services.AddSingleton<ICachingService, CachingService>();
// ... many more lines
```

### After (Extension Methods)

```csharp
// Program.cs - GOOD
using GaApi.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Clean, organized service registration
builder.Services.AddChordServices();
builder.Services.AddCachingServices();
builder.Services.AddVectorSearchServices();
builder.Services.AddOllamaServices(builder.Configuration);
builder.Services.AddHealthCheckServices();
```

## Common Patterns

### Pattern 1: HTTP Client Registration

```csharp
public static IServiceCollection AddOllamaServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Register HTTP client with Polly policies
    services.AddHttpClient("Ollama", client =>
    {
        var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromMinutes(5);
    })
    .AddTransientHttpErrorPolicy(policy =>
        policy.WaitAndRetryAsync(3, retryAttempt =>
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))))
    .AddTransientHttpErrorPolicy(policy =>
        policy.CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
    
    return services;
}
```

### Pattern 2: Strategy Pattern Registration

```csharp
public static IServiceCollection AddVectorSearchServices(
    this IServiceCollection services)
{
    // Register strategies
    services.AddSingleton<InMemoryVectorSearchStrategy>();
    services.AddSingleton<CudaVectorSearchStrategy>();
    services.AddSingleton<MongoDbVectorSearchStrategy>();
    
    // Register strategy manager
    services.AddSingleton<VectorSearchStrategyManager>();
    
    // Register main service
    services.AddSingleton<IVectorSearchService, EnhancedVectorSearchService>();
    
    return services;
}
```

### Pattern 3: Conditional Registration

```csharp
public static IServiceCollection AddCachingServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    var useRedis = configuration.GetValue<bool>("Caching:UseRedis");
    
    if (useRedis)
    {
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = configuration["Redis:ConnectionString"];
        });
    }
    else
    {
        services.AddMemoryCache();
    }
    
    services.AddSingleton<ICachingService, CachingService>();
    
    return services;
}
```

## Testing Extension Methods

Extension methods should be testable:

```csharp
[Test]
public void AddChordServices_RegistersAllRequiredServices()
{
    // Arrange
    var services = new ServiceCollection();
    
    // Act
    services.AddChordServices();
    var provider = services.BuildServiceProvider();
    
    // Assert
    Assert.IsNotNull(provider.GetService<IChordService>());
    Assert.IsNotNull(provider.GetService<IContextualChordService>());
    Assert.IsNotNull(provider.GetService<IVoicingFilterService>());
}
```

## Migration Checklist

When refactoring existing inline registrations to extension methods:

- [ ] Identify related services by feature area
- [ ] Create extension class in correct project
- [ ] Move service registrations to extension method
- [ ] Add XML documentation
- [ ] Update Program.cs to use extension method
- [ ] Verify correct service lifetimes
- [ ] Run tests to ensure no regressions
- [ ] Update any related documentation

## References

- [Microsoft Dependency Injection Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
- [Service Lifetimes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection#service-lifetimes)
- [Extension Methods Best Practices](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/extension-methods)

