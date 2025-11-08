# Refactoring Code Examples

**Date:** 2025-11-02  
**Purpose:** Concrete before/after examples for key refactoring improvements

---

## Example 1: Service Registration Cleanup

### Before (Current Code)

**File:** `Apps/ga-server/GaApi/Program.cs`

```csharp
// Lines 118-135
builder.Services.AddHttpClient("Ollama", client =>
{
    var ollamaBaseUrl = builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
    client.BaseAddress = new Uri(ollamaBaseUrl);
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddSingleton<OllamaEmbeddingService>();
builder.Services.AddSingleton<OllamaChatService>();

builder.Services.AddSingleton<GA.Business.Core.Fretboard.SemanticIndexing.SemanticSearchService>(sp =>
{
    var embeddingService = sp.GetRequiredService<OllamaEmbeddingService>();
    return new GA.Business.Core.Fretboard.SemanticIndexing.SemanticSearchService(embeddingService);
});

builder.Services.AddSingleton<GA.Business.Core.Atonal.Grothendieck.IGrothendieckService, GA.Business.Core.Atonal.Grothendieck.GrothendieckService>();

// Lines 141-151 (DUPLICATES!)
builder.Services.AddSingleton<GA.Business.Core.BSP.IntelligentBspGenerator>();
builder.Services.AddSingleton<GA.Business.Core.AI.AdaptiveDifficultySystem>();
builder.Services.AddSingleton<GA.Business.Core.AI.StyleLearningSystem>();
builder.Services.AddSingleton<GA.Business.Core.AI.PatternRecognitionSystem>();
builder.Services.AddSingleton<GA.Business.Core.Analytics.Spectral.AgentSpectralAnalyzer>();

builder.Services.AddSingleton<GA.Business.Core.BSP.IntelligentBspGenerator>();
builder.Services.AddSingleton<GA.Business.Core.AI.AdaptiveDifficultySystem>();
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Shapes.IShapeGraphBuilder, GA.Business.Core.Fretboard.Shapes.ShapeGraphBuilder>();
builder.Services.AddSingleton<GA.Business.Core.Atonal.Grothendieck.MarkovWalker>();
```

### After (Refactored)

**File:** `Apps/ga-server/GaApi/Program.cs`

```csharp
// Clean, organized service registration
builder.Services.AddSemanticServices(builder.Configuration);
builder.Services.AddGrothendieckServices();
builder.Services.AddAIServices();
builder.Services.AddBSPServices();
builder.Services.AddShapeGraphServices();
```

**New Extension Methods:**

```csharp
// Common/GA.Business.Core/Extensions/SemanticServiceExtensions.cs
public static class SemanticServiceExtensions
{
    public static IServiceCollection AddSemanticServices(
        this IServiceCollection services, 
        IConfiguration configuration)
    {
        // Configure Ollama HTTP client
        services.AddHttpClient("Ollama", client =>
        {
            var baseUrl = configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        // Register services
        services.AddSingleton<OllamaEmbeddingService>();
        services.AddSingleton<OllamaChatService>();
        services.AddSingleton<SemanticSearchService>(sp =>
        {
            var embeddingService = sp.GetRequiredService<OllamaEmbeddingService>();
            return new SemanticSearchService(embeddingService);
        });

        return services;
    }
}

// Common/GA.Business.Core/Extensions/AIServiceExtensions.cs
public static class AIServiceExtensions
{
    public static IServiceCollection AddAIServices(this IServiceCollection services)
    {
        services.AddSingleton<IntelligentBspGenerator>();
        services.AddSingleton<AdaptiveDifficultySystem>();
        services.AddSingleton<StyleLearningSystem>();
        services.AddSingleton<PatternRecognitionSystem>();
        services.AddSingleton<AgentSpectralAnalyzer>();
        
        return services;
    }
}
```

**Benefits:**
- ✅ No duplicates
- ✅ Clear organization
- ✅ Reusable across projects
- ✅ Easier to test
- ✅ Self-documenting

---

## Example 2: Centralized Cache Keys

### Before (Current Code)

**Multiple files with inconsistent cache key formats:**

```csharp
// Apps/ga-server/GaApi/Services/ChordService.cs
var cacheKey = $"chords_search_{query}_{limit}";
var cacheKey = $"chord_{id}";

// Common/GA.Business.Core/Spatial/TonalBSPService.cs
var cacheKey = $"scales_{pitchClassSet}";
var cacheKey = $"spatial_{center}_{radius}_{strategy}";

// Apps/ga-server/GaApi/Services/ChordQuery/ChordQueryExecutor.cs
var cacheKey = $"query_{plan.Query.QueryType}_{plan.Query.Filters.GetHashCode()}";
```

### After (Refactored)

**New centralized cache key generator:**

```csharp
// Common/GA.Business.Core/Caching/CacheKeys.cs
namespace GA.Business.Core.Caching;

/// <summary>
/// Centralized cache key generation for consistent caching across the application
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "ga";
    private const string Separator = ":";
    
    /// <summary>
    /// Chord-related cache keys
    /// </summary>
    public static class Chords
    {
        public static string ById(string id) 
            => $"{Prefix}{Separator}chords{Separator}id{Separator}{id}";
        
        public static string Search(string query, int limit) 
            => $"{Prefix}{Separator}chords{Separator}search{Separator}{query}{Separator}{limit}";
        
        public static string ByQuality(string quality, int limit) 
            => $"{Prefix}{Separator}chords{Separator}quality{Separator}{quality}{Separator}{limit}";
        
        public static string ByPitchClassSet(PitchClassSet pcs) 
            => $"{Prefix}{Separator}chords{Separator}pcs{Separator}{pcs}";
    }
    
    /// <summary>
    /// Scale-related cache keys
    /// </summary>
    public static class Scales
    {
        public static string ByPitchClassSet(PitchClassSet pcs) 
            => $"{Prefix}{Separator}scales{Separator}pcs{Separator}{pcs}";
        
        public static string Related(PitchClassSet pcs) 
            => $"{Prefix}{Separator}scales{Separator}related{Separator}{pcs}";
    }
    
    /// <summary>
    /// Spatial query cache keys
    /// </summary>
    public static class Spatial
    {
        public static string Query(PitchClassSet center, double radius, TonalPartitionStrategy strategy) 
            => $"{Prefix}{Separator}spatial{Separator}{center}{Separator}{radius:F2}{Separator}{strategy}";
    }
    
    /// <summary>
    /// Query plan cache keys
    /// </summary>
    public static class QueryPlans
    {
        public static string ForQuery(ChordQuery query) 
            => $"{Prefix}{Separator}plans{Separator}{query.QueryType}{Separator}{query.Filters.GetHashCode()}";
    }
}
```

**Updated service code:**

```csharp
// Apps/ga-server/GaApi/Services/ChordService.cs
public async Task<List<Chord>> SearchChordsAsync(string query, int limit = 100)
{
    var cacheKey = CacheKeys.Chords.Search(query, limit); // ✅ Centralized
    
    if (cache.TryGetValue(cacheKey, out List<Chord>? cachedChords))
    {
        return cachedChords!;
    }
    
    var chords = await mongoDb.SearchChordsAsync(query, limit);
    cache.Set(cacheKey, chords, TimeSpan.FromMinutes(5));
    
    return chords;
}
```

**Benefits:**
- ✅ Consistent key format
- ✅ Easy to find all cache keys
- ✅ Type-safe key generation
- ✅ Easier to implement cache invalidation
- ✅ Better cache analytics

---

## Example 3: Enhanced Error Handling

### Before (Current Code)

**Inconsistent error handling:**

```csharp
// Some controllers
catch (Exception ex)
{
    logger.LogError(ex, "Error recording performance");
    return StatusCode(500, ApiResponse<object>.Fail("Internal server error", ex.Message));
}

// Other controllers
catch (Exception ex)
{
    logger.LogError(ex, "Error parsing chord progression");
    return StatusCode(500, new ParseChordProgressionResponse
    {
        Success = false,
        Error = $"Internal error: {ex.Message}"
    });
}

// Some services
catch (Exception ex)
{
    logger.LogError(ex, "Error analyzing data quality for {ConceptType}", conceptType);
    return new DataQualityAnalysis { ConceptType = conceptType, OverallScore = 0.5 };
}
```

### After (Refactored)

**Standardized error handling with correlation IDs:**

```csharp
// Apps/ga-server/GaApi/Models/ErrorResponse.cs
public record ErrorResponse
{
    public required string Message { get; init; }
    public string? Details { get; init; }
    public required string CorrelationId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string? Path { get; init; }
    public string? Method { get; init; }
    public Dictionary<string, object>? Context { get; init; }
}

// Apps/ga-server/GaApi/Extensions/ControllerExtensions.cs
public static class ControllerExtensions
{
    public static IActionResult InternalServerError(
        this ControllerBase controller,
        Exception exception,
        string message = "An internal server error occurred")
    {
        var correlationId = controller.HttpContext.TraceIdentifier;
        
        var response = new ErrorResponse
        {
            Message = message,
            Details = controller.HttpContext.RequestServices
                .GetRequiredService<IWebHostEnvironment>()
                .IsDevelopment() ? exception.ToString() : null,
            CorrelationId = correlationId,
            Path = controller.HttpContext.Request.Path,
            Method = controller.HttpContext.Request.Method
        };
        
        return controller.StatusCode(500, response);
    }
}

// Updated controller code
catch (Exception ex)
{
    logger.LogError(ex, 
        "Error recording performance. CorrelationId: {CorrelationId}", 
        HttpContext.TraceIdentifier);
    return this.InternalServerError(ex, "Error recording performance");
}
```

**Enhanced middleware:**

```csharp
// Apps/ga-server/GaApi/Middleware/ErrorHandlingMiddleware.cs
public async Task InvokeAsync(HttpContext context)
{
    var correlationId = context.TraceIdentifier;
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    
    try
    {
        await next(context);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, 
            "Unhandled exception. CorrelationId: {CorrelationId}, Path: {Path}, Method: {Method}, User: {User}",
            correlationId, 
            context.Request.Path, 
            context.Request.Method,
            context.User?.Identity?.Name ?? "Anonymous");
            
        await HandleExceptionAsync(context, ex, correlationId);
    }
}
```

**Benefits:**
- ✅ Consistent error format
- ✅ Correlation IDs for tracing
- ✅ Better debugging
- ✅ Structured logging
- ✅ Production-safe error details

---

## Example 4: Configuration Consolidation

### Before (Current Code)

**Duplicated configuration across multiple files:**

```json
// Apps/ga-server/GaApi/appsettings.json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "guitar-alchemist",
    "Collections": {
      "Chords": "chords",
      "ChordTemplates": "chord-templates"
    }
  }
}

// Apps/GuitarAlchemistChatbot/appsettings.json
{
  "MongoDB": {
    "DatabaseName": "guitar-alchemist",  // Different structure!
    "Collections": {
      "Chords": "chords",
      "ChordTemplates": "chord-templates"
    }
  },
  "ConnectionStrings": {
    "MongoDB": "mongodb://localhost:27017"  // Different location!
  }
}

// GaCLI/Program.cs
options.ConnectionString = "mongodb://localhost:27017";  // Hardcoded!
options.DatabaseName = "guitaralchemist";  // Different name!
```

### After (Refactored)

**Shared configuration:**

```json
// appsettings.Shared.json (NEW)
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "guitar-alchemist",
    "Collections": {
      "Chords": "chords",
      "ChordTemplates": "chord-templates",
      "Scales": "scales",
      "Progressions": "progressions"
    }
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "qwen2.5-coder:1.5b-base",
    "EmbeddingModel": "nomic-embed-text"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Updated Program.cs:**

```csharp
// All applications use the same pattern
var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Shared.json", optional: true, reloadOnChange: true)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true)
    .AddEnvironmentVariables();
```

**Benefits:**
- ✅ Single source of truth
- ✅ No configuration drift
- ✅ Environment-specific overrides
- ✅ Easier to maintain

---

## Summary

These examples demonstrate **practical, low-risk refactoring** that will:

1. **Improve Maintainability** - Easier to understand and modify
2. **Enhance Consistency** - Standard patterns across the codebase
3. **Better Debugging** - Correlation IDs and structured logging
4. **Reduce Errors** - Centralized configuration and validation
5. **Increase Performance** - Better caching strategies

All changes are **backward compatible** and can be implemented **incrementally**.

