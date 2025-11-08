# Configuration Strategy Guide

**Purpose**: Define clear guidelines for when to use `appsettings.json`, YAML files, or code-based configuration in the Guitar Alchemist project.

---

## Configuration Types Overview

The Guitar Alchemist project uses three primary configuration approaches:

1. **appsettings.json** - Runtime application settings
2. **YAML files** - Domain data and business rules
3. **Code-based configuration** - Strongly-typed options and DI setup

---

## 1. appsettings.json - Runtime Application Settings

### When to Use:

✅ **Infrastructure Configuration**:
- Database connection strings (MongoDB, SQLite)
- External service URLs (Ollama, OpenAI, HuggingFace, Graphiti, TARS MCP)
- API keys and credentials (use user-secrets for development)
- Logging levels and providers

✅ **Runtime Behavior**:
- Feature flags (EnableSemanticSearch, EnableGpuMonitoring)
- Performance tuning (timeouts, retries, cache sizes)
- Environment-specific overrides (Development, Production, Testing)

✅ **Service Configuration**:
- HTTP client settings (timeouts, base URLs)
- Rate limiting parameters
- Caching policies (expiration, size limits)

### Examples:

```json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "guitar-alchemist"
  },
  "Ollama": {
    "BaseUrl": "http://localhost:11434",
    "ChatModel": "qwen2.5-coder:1.5b-base",
    "EmbeddingModel": "nomic-embed-text"
  },
  "VectorSearch": {
    "PreferredStrategies": ["CUDA", "InMemory", "MongoDB"],
    "EnableAutoSwitching": false,
    "MaxMemoryUsageMB": 2048
  },
  "Caching": {
    "Regular": {
      "ExpirationMinutes": 15,
      "SizeLimit": 1000
    }
  }
}
```

### File Structure:

- `appsettings.json` - Base configuration (committed to repo)
- `appsettings.Development.json` - Development overrides (committed to repo)
- `appsettings.Production.json` - Production overrides (committed to repo)
- User secrets - Sensitive data (NOT committed, use `dotnet user-secrets`)

### Best Practices:

1. **Never commit secrets** - Use `dotnet user-secrets` or environment variables
2. **Provide defaults** - Base `appsettings.json` should have sensible defaults
3. **Document settings** - Add comments in code or separate docs
4. **Use Options pattern** - Bind to strongly-typed classes (see Section 3)
5. **Environment-specific** - Use `appsettings.{Environment}.json` for overrides

---

## 2. YAML Files - Domain Data and Business Rules

### When to Use:

✅ **Musical Domain Data**:
- Scale definitions and modes (`Modes_Simple.yaml`)
- Chord progressions and patterns (`ChordProgressions.yaml`)
- Iconic chord voicings (`IconicChords.yaml`)
- Guitar techniques (`GuitarTechniques.yaml`)

✅ **Business Rules**:
- Invariant definitions (`InvariantDefinitions.yaml`)
- Voice leading rules (`VoiceLeading.yaml`)
- Modal interchange patterns (`ModalInterchange.yaml`)
- Atonal techniques (`AtonalTechniques.yaml`)

✅ **Reference Data**:
- Specialized tunings (`SpecializedTunings.yaml`)
- Key modulation techniques (`KeyModulationTechniques.yaml`)
- Pedal tones (`PedalTones.yaml`)
- Advanced harmony (`AdvancedHarmony.yaml`)

### Examples:

**Modes_Simple.yaml**:
```yaml
modes:
  - name: Ionian
    intervals: [0, 2, 4, 5, 7, 9, 11]
    family: Diatonic
    brightness: 7
    
  - name: Dorian
    intervals: [0, 2, 3, 5, 7, 9, 10]
    family: Diatonic
    brightness: 6
```

**InvariantDefinitions.yaml**:
```yaml
invariants:
  - name: ChordNameNotEmpty
    category: Chord
    severity: Error
    description: "Chord name must not be empty"
    enabled: true
    
environments:
  development:
    cache_enabled: false
    performance_monitoring: true
    
  production:
    cache_enabled: true
    cache_duration_minutes: 60
```

### File Locations:

- `Common/GA.Business.Config/` - F# config files and YAML loaders
- `Common/GA.Business.Core/Configuration/` - YAML configuration files
- `config/` - Application-level YAML configs (if needed)

### Best Practices:

1. **Use for domain data** - Not for runtime settings
2. **Version control** - Always commit YAML files
3. **Schema validation** - Define schemas for complex YAML
4. **Hot reload** - Use `ConfigurationWatcherService` for file watching
5. **Centralize loading** - Use `GA.Business.Config` project for YAML parsing
6. **F# for parsing** - Leverage F# type providers and pattern matching

---

## 3. Code-Based Configuration - Strongly-Typed Options

### When to Use:

✅ **Strongly-Typed Settings**:
- Options pattern classes (ChatbotOptions, VectorSearchOptions)
- Service registration and DI configuration
- Middleware configuration
- Complex validation logic

✅ **Programmatic Configuration**:
- Conditional service registration
- Feature detection (CUDA availability, GPU support)
- Dynamic configuration based on environment
- Fallback strategies

### Examples:

**Options Pattern**:

```csharp
// Configuration class
public sealed class ChatbotOptions
{
    public const string SectionName = "Chatbot";
    
    public string Model { get; set; } = "llama3.2:3b";
    public int HistoryLimit { get; set; } = 12;
    public bool EnableSemanticSearch { get; set; } = true;
    public int SemanticSearchLimit { get; set; } = 5;
}

// Registration in Program.cs
builder.Services.Configure<ChatbotOptions>(
    builder.Configuration.GetSection(ChatbotOptions.SectionName));

// Usage in service
public class ChatService
{
    private readonly ChatbotOptions _options;
    
    public ChatService(IOptions<ChatbotOptions> options)
    {
        _options = options.Value;
    }
}
```

**Extension Methods for Service Registration**:

```csharp
public static class ChordServiceExtensions
{
    public static IServiceCollection AddChordServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register chord-related services
        services.AddSingleton<ChordFactory>();
        services.AddSingleton<ChordTemplateFactory>();
        services.AddScoped<ChordAnalyzer>();
        
        // Configure options
        services.Configure<ChordOptions>(
            configuration.GetSection("Chord"));
        
        return services;
    }
}
```

### Best Practices:

1. **Use Options pattern** - Bind appsettings.json to strongly-typed classes
2. **Extension methods** - Group related service registrations
3. **Validate options** - Use `IValidateOptions<T>` or data annotations
4. **Immutable options** - Use `IOptionsSnapshot<T>` for reloadable config
5. **Document options** - XML comments on all properties
6. **Defaults in code** - Provide sensible defaults in option classes

---

## Decision Matrix

| Configuration Type | Use Case | Example | Reload Support | Type Safety |
|-------------------|----------|---------|----------------|-------------|
| **appsettings.json** | Infrastructure, runtime settings | MongoDB connection, Ollama URL | ✅ Yes (IOptionsSnapshot) | ✅ Yes (Options pattern) |
| **YAML files** | Domain data, business rules | Modes, progressions, invariants | ✅ Yes (ConfigurationWatcherService) | ⚠️ Partial (F# types) |
| **Code-based** | Service registration, DI | Extension methods, middleware | ❌ No (compile-time) | ✅ Yes (strongly-typed) |

---

## Configuration Loading Order

1. **Base appsettings.json** - Default settings
2. **appsettings.{Environment}.json** - Environment overrides
3. **User secrets** - Development secrets (Development only)
4. **Environment variables** - Container/deployment overrides
5. **Command-line arguments** - Runtime overrides
6. **YAML files** - Domain data (loaded on startup or hot-reloaded)

---

## Common Patterns

### Pattern 1: Infrastructure Service Configuration

**Use**: appsettings.json + Options pattern

```csharp
// appsettings.json
{
  "MongoDB": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "guitar-alchemist"
  }
}

// MongoDbSettings.cs
public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "guitar-alchemist";
}

// Program.cs
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));
```

### Pattern 2: Domain Data Configuration

**Use**: YAML files + F# config loader

```fsharp
// ModesConfig.fs
module ModesConfig =
    let getConfigPath() =
        let configName = "Modes_Simple.yaml"
        // Search multiple locations
        possiblePaths |> List.tryFind File.Exists
    
    let loadModes() =
        // Parse YAML and return strongly-typed F# records
```

### Pattern 3: Feature Flags

**Use**: appsettings.json + code-based conditional registration

```csharp
// appsettings.json
{
  "VectorSearch": {
    "PreferredStrategies": ["CUDA", "InMemory"],
    "EnableAutoSwitching": false
  }
}

// Program.cs
var vectorSearchOptions = builder.Configuration
    .GetSection("VectorSearch")
    .Get<VectorSearchOptions>();

if (vectorSearchOptions.PreferredStrategies.Contains("CUDA"))
{
    builder.Services.AddCudaVectorSearch();
}
```

---

## Migration Guidelines

### Moving from appsettings.json to YAML:

❌ **Don't move**:
- Connection strings
- API keys
- Service URLs
- Performance tuning

✅ **Do move**:
- Musical scales/modes
- Chord progressions
- Business rules
- Reference data

### Moving from YAML to appsettings.json:

❌ **Don't move**:
- Domain data (scales, chords)
- Business rules
- Large reference datasets

✅ **Do move**:
- Feature flags
- Runtime behavior
- Service configuration

---

## Security Considerations

1. **Never commit secrets** to appsettings.json
2. **Use user-secrets** for development: `dotnet user-secrets set "OpenAI:ApiKey" "sk-..."`
3. **Use environment variables** for production
4. **Audit configuration files** regularly for accidental leaks
5. **Encrypt sensitive YAML** if it contains proprietary data

---

## File Watching and Hot Reload

### appsettings.json:
- Automatically reloaded by ASP.NET Core
- Use `IOptionsSnapshot<T>` to get updated values
- Changes apply on next request

### YAML files:
- Use `ConfigurationWatcherService` for file watching
- Manually reload domain data when files change
- Notify dependent services of changes

```csharp
// ConfigurationWatcherService.cs
private void SetupFileWatchers()
{
    var configurationFiles = new[]
    {
        "IconicChords.yaml",
        "ChordProgressions.yaml",
        "Modes_Simple.yaml"
    };
    
    foreach (var file in configurationFiles)
    {
        var watcher = new FileSystemWatcher(configDirectory, file);
        watcher.Changed += OnConfigurationFileChanged;
        watcher.EnableRaisingEvents = true;
    }
}
```

---

## Summary

| Aspect | appsettings.json | YAML Files | Code-Based |
|--------|-----------------|------------|------------|
| **Purpose** | Runtime settings | Domain data | Service setup |
| **Examples** | DB connections, URLs | Modes, progressions | DI registration |
| **Reload** | Automatic | Manual/watched | No |
| **Type Safety** | Options pattern | F# types | Full |
| **Version Control** | Yes (no secrets) | Yes | Yes |
| **Environment Override** | Yes | No | No |

**Golden Rule**: Use appsettings.json for "how the app runs", YAML for "what the app knows", and code for "how the app is built".

