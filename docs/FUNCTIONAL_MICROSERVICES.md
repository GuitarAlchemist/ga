# Functional Microservices Framework

## Overview

This framework brings **Spring Boot-inspired patterns** to .NET microservices using **F#-inspired monads in C#**. It emphasizes:

- **Monadic Composition**: Option, Result, Reader, State, and Async monads
- **Immutability**: All configuration and state is immutable (C# records)
- **Railway-Oriented Programming**: Error handling using Result monad
- **Dependency Injection as Monad**: Reader monad for DI
- **Type Safety**: Strong typing with C# 12+ features
- **LINQ Integration**: Full LINQ query syntax support for all monads
- **Pure Functions**: Side effects are isolated and explicit

## F#-Inspired Monads in C#

### 1. Option Monad (`Option<T>`)

Replaces null references with explicit optionality:

```csharp
// Instead of nullable references
Chord? chord = FindChord("Cmaj7"); // Can be null!

// Use Option monad
Option<Chord> chord = FindChord("Cmaj7"); // Explicitly optional

chord.Match(
    onSome: c => Console.WriteLine($"Found: {c.Name}"),
    onNone: () => Console.WriteLine("Not found")
);

// LINQ support
var result = from c in FindChord("Cmaj7")
             select c.Name;
```

### 2. Result Monad (`Result<TSuccess, TFailure>`)

Railway-oriented programming for error handling:

```csharp
Result<Chord, string> ValidateChord(Chord chord)
{
    if (string.IsNullOrWhiteSpace(chord.Name))
        return new Result<Chord, string>.Failure("Name required");

    return new Result<Chord, string>.Success(chord);
}

// Chain operations
var result = ValidateChord(chord)
    .Bind(c => EnrichChord(c))
    .Bind(c => SaveChord(c))
    .Match(
        onSuccess: c => $"Saved: {c.Name}",
        onFailure: error => $"Error: {error}"
    );

// LINQ support
var result2 = from c in ValidateChord(chord)
              from enriched in EnrichChord(c)
              from saved in SaveChord(enriched)
              select saved;
```

### 3. Reader Monad (`Reader<TEnv, T>`)

Dependency injection as a monad:

```csharp
record ServiceDeps(IConfiguration Config, ILogger Logger, IMemoryCache Cache);

Reader<ServiceDeps, Chord> GetChord(string id) =>
    from deps in Reader.Ask<ServiceDeps>()
    let cacheKey = $"chord:{id}"
    let cached = deps.Cache.Get<Chord>(cacheKey)
    select cached ?? LoadFromDatabase(id);

// Run with dependencies
var deps = new ServiceDeps(config, logger, cache);
var chord = GetChord("123").Run(deps);
```

### 4. State Monad (`State<TState, T>`)

Threading state through computations:

```csharp
State<int, Chord> TransposeChord(Chord chord, int semitones) =>
    from currentTransposition in State<int, int>.Get
    let newTransposition = currentTransposition + semitones
    from _ in State<int, Unit>.Put(newTransposition)
    select new Chord(
        chord.Name,
        chord.Quality,
        chord.PitchClasses.Select(pc => (pc + semitones) % 12).ToList()
    );

// Run with initial state
var (transposedChord, finalState) = TransposeChord(chord, 2).Run(0);
```

### 5. Async Monad (`Async<T>`)

Asynchronous computations with monadic operations:

```csharp
Async<Chord> LoadChordAsync(string id) =>
    from chord in Async.FromTask(database.LoadAsync(id))
    from validated in Async.Return(ValidateChord(chord))
    select validated;

// Run async computation
var chord = await LoadChordAsync("123").ToTask();
```

## Core Concepts

### 1. Starters (Spring Boot @SpringBootApplication)

Starters are cohesive bundles of configuration, services, and lifecycle hooks.

```fsharp
// Define a starter
let myStarter =
    AutoConfiguration.createStarter "my-service" "1.0.0"
    |> AutoConfiguration.withConfiguration myConfig
    |> AutoConfiguration.withService myService
    |> AutoConfiguration.withCondition (WhenConfigured "MyService")
    |> AutoConfiguration.withStartup onStartup
    |> AutoConfiguration.withShutdown onShutdown
```

### 2. Auto-Configuration (Spring Boot @Configuration)

Configuration is validated at startup using pure functions:

```fsharp
type MyConfig = {
    Timeout: TimeSpan
    MaxRetries: int
}

let validateConfig config =
    [
        if config.Timeout.TotalSeconds < 1.0 then
            yield "Timeout must be at least 1 second"
        if config.MaxRetries < 0 then
            yield "MaxRetries must be non-negative"
    ]
    |> function
        | [] -> Ok ()
        | errors -> Error errors
```

### 3. Service Factories (Spring Boot @Bean)

Services are created using factory functions with explicit dependencies:

```fsharp
let createMyService (provider: IServiceProvider) =
    let config = provider.GetService<IOptions<MyConfig>>().Value
    let logger = provider.GetService<ILogger<MyService>>()
    
    new MyService(config, logger)

let myServiceFactory =
    AutoConfiguration.createService<IMyService>
        "myService"
        createMyService
        ServiceLifetime.Singleton
    |> AutoConfiguration.withHealthCheck (fun service ->
        async {
            let! isHealthy = service.CheckHealth()
            return if isHealthy then Actuator.Up else Actuator.Down
        }
    )
```

### 4. Conditional Registration (Spring Boot @Conditional)

Services can be conditionally registered:

```fsharp
// Register only if configuration section exists
|> AutoConfiguration.withCondition (WhenConfigured "MyService")

// Register only in production
|> AutoConfiguration.withCondition (Custom (fun provider ->
    Profiles.isActive provider Profiles.Production
))

// Register only if another service is present
|> AutoConfiguration.withCondition (WhenBeanPresent typeof<IOtherService>)
```

### 5. Health Indicators (Spring Boot Actuator)

Health checks are pure async functions:

```fsharp
let createDatabaseHealthIndicator (db: IDatabase) =
    Actuator.createHealthIndicator "database" (fun () ->
        async {
            try
                do! db.Ping()
                return Actuator.Up
            with
            | _ -> return Actuator.Down
        }
    )
    |> Actuator.withDetails (fun () ->
        async {
            let! stats = db.GetStats()
            return Map.ofList [
                "connections", box stats.ActiveConnections
                "queries", box stats.TotalQueries
            ]
        }
    )
```

### 6. Metrics (Spring Boot Micrometer)

Metrics are functional and composable:

```fsharp
let createRequestMetrics () =
    let mutable requestCount = 0L
    
    let increment () = 
        System.Threading.Interlocked.Increment(&requestCount) |> ignore
    
    let metric = Actuator.createCounter
        "http.requests"
        (fun () -> box requestCount)
        (Map.ofList ["endpoint", "/api/chords"])
    
    (increment, metric)
```

### 7. Event System (Spring Boot ApplicationEvent)

Type-safe event publishing and subscription:

```fsharp
type ChordCreatedEvent = {
    ChordId: string
    Name: string
    CreatedAt: DateTime
}

let eventBus = Events.EventBus()

// Subscribe
eventBus.Subscribe<ChordCreatedEvent>(fun event ->
    async {
        printfn $"Chord created: {event.Data.Name}"
    }
)

// Publish
let event = Events.createEvent "ChordService" {
    ChordId = "123"
    Name = "Cmaj7"
    CreatedAt = DateTime.UtcNow
}
do! eventBus.Publish event
```

## Functional Patterns

### Reader Monad for Dependency Injection

Instead of constructor injection, use the Reader monad:

```fsharp
type ServiceDeps = {
    Config: IConfiguration
    Logger: ILogger
    Cache: IMemoryCache
}

let getChordById chordId : Reader<ServiceDeps, Chord option> =
    reader {
        let! deps = ask
        
        // Try cache first
        match deps.Cache.TryGetValue(chordId) with
        | true, chord -> return Some (chord :?> Chord)
        | false, _ ->
            // Load from database
            deps.Logger.LogInformation($"Cache miss for chord {chordId}")
            let! chord = loadChordFromDb chordId
            
            // Cache it
            deps.Cache.Set(chordId, chord, TimeSpan.FromMinutes(15.0))
            
            return chord
    }

// Run the reader with dependencies
let deps = { Config = config; Logger = logger; Cache = cache }
let chord = runReader (getChordById "123") deps
```

### Railway-Oriented Programming

Chain operations that can fail:

```fsharp
let validateChord chord =
    if String.IsNullOrWhiteSpace(chord.Name) then
        Error "Chord name is required"
    else
        Ok chord

let enrichChord chord =
    Ok { chord with Metadata = getMetadata chord }

let saveChord chord =
    try
        database.Save(chord)
        Ok chord
    with
    | ex -> Error $"Failed to save: {ex.Message}"

// Compose operations
let processChord chord =
    chord
    |> validateChord
    |> Result.bind enrichChord
    |> Result.bind saveChord
```

### Middleware Composition

Functional middleware pattern:

```fsharp
let pipeline =
    loggingMiddleware logger
    |> compose (errorHandlingMiddleware logger)
    |> compose (timingMiddleware logger)

let handler context = async {
    // Your business logic
    return result
}

let enhancedHandler = apply pipeline handler
```

## Practical Example: Music Service

```fsharp
// 1. Define configuration
type MusicServiceConfig = {
    CacheEnabled: bool
    CacheTTL: TimeSpan
    MaxConcurrentRequests: int
}

let validateMusicConfig config =
    [
        if config.MaxConcurrentRequests < 1 then
            yield "MaxConcurrentRequests must be positive"
    ]
    |> function [] -> Ok () | errors -> Error errors

// 2. Create service factory
let createMusicService (provider: IServiceProvider) =
    let config = provider.GetService<IOptions<MusicServiceConfig>>().Value
    let logger = provider.GetService<ILogger<MusicService>>()
    let cache = provider.GetService<IMemoryCache>()
    
    new MusicService(config, logger, cache)

// 3. Create health indicator
let createMusicHealthIndicator (service: IMusicService) =
    Actuator.createHealthIndicator "music-service" (fun () ->
        async {
            let! canConnect = service.TestConnection()
            return if canConnect then Actuator.Up else Actuator.Down
        }
    )

// 4. Create starter
let musicServiceStarter =
    AutoConfiguration.createStarter "music-service" "1.0.0"
    |> AutoConfiguration.withConfiguration (
        AutoConfiguration.createConfig
            "MusicServiceConfig"
            "MusicService"
            validateMusicConfig
            { CacheEnabled = true; CacheTTL = TimeSpan.FromMinutes(15.0); MaxConcurrentRequests = 100 }
    )
    |> AutoConfiguration.withService (
        AutoConfiguration.createService<IMusicService>
            "musicService"
            createMusicService
            ServiceLifetime.Singleton
    )
    |> AutoConfiguration.withCondition (WhenConfigured "MusicService")
    |> AutoConfiguration.withStartup (fun provider ->
        async {
            let logger = provider.GetService<ILogger<obj>>()
            logger.LogInformation("Music service starting...")
            return ()
        }
    )

// 5. Use in Program.cs (C# interop)
// services.AddFunctionalMicroservices(configuration, builder => {
//     builder.AddStarter(musicServiceStarter);
// });
```

## Comparison with Spring Boot

| Spring Boot | Functional Framework | Notes |
|-------------|---------------------|-------|
| `@SpringBootApplication` | `MicroserviceStarter` | Cohesive service bundle |
| `@Configuration` | `ServiceConfiguration<T>` | Validated configuration |
| `@Bean` | `ServiceFactory<T>` | Service factory with DI |
| `@Conditional` | `ServiceCondition` | Conditional registration |
| `@Profile` | `Profiles` module | Environment-based config |
| `@Value` | `PropertyInjection` | Configuration injection |
| Actuator Health | `Actuator.HealthIndicator` | Health checks |
| Micrometer | `Actuator.Metric` | Metrics collection |
| ApplicationEvent | `Events.EventBus` | Type-safe events |
| Auto-configuration | `AutoConfiguration` | Convention over config |

## Benefits

1. **Type Safety**: F# type system catches errors at compile time
2. **Immutability**: No mutable state, easier to reason about
3. **Composition**: Build complex services from simple functions
4. **Testability**: Pure functions are easy to test
5. **Explicit Dependencies**: Reader monad makes dependencies clear
6. **Railway-Oriented**: Error handling is explicit and composable
7. **Functional Core, Imperative Shell**: Business logic is pure, side effects at edges

## Integration with Existing Code

This framework works alongside your existing C# services:

```csharp
// In Program.cs
builder.Services.AddFunctionalMicroservices(builder.Configuration, appBuilder =>
{
    appBuilder
        .AddStarter(CachingStarter.create())
        .AddStarter(MongoDbStarter.create())
        .AddStarter(MusicServiceStarter.create())
        .WithEventBus();
});
```

## Next Steps

1. **Create more starters**: Ollama, Semantic Kernel, Vector Search
2. **Add circuit breakers**: Functional resilience patterns
3. **Implement saga pattern**: Distributed transactions
4. **Add distributed tracing**: OpenTelemetry integration
5. **Create service mesh**: Functional service discovery

