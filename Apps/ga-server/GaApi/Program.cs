using System.Net;
using System.Reflection;
using AllProjects.ServiceDefaults;
using GA.Business.Core.Microservices.Microservices;
using GA.Data.EntityFramework;
using GaApi.Configuration;
using GaApi.Extensions;
using GaApi.GraphQL.Mutations;
using GaApi.GraphQL.Queries;
using GaApi.Hubs;
using GaApi.Models;
using GaApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MudBlazor.Services;
using Polly;
using Polly.Extensions.Http;
using Path = System.IO.Path;
using GA.Business.Core.Chords;
using GA.Business.Core.Unified;

var builder = WebApplication.CreateBuilder(args);

// Add shared configuration
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "../../appsettings.Shared.json"), optional: true, reloadOnChange: true);


// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Redis distributed cache (Aspire integration)
builder.AddRedisDistributedCache("redis");

// Add services to the container.

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));

// Configure Vector Search Options
builder.Services.Configure<VectorSearchOptions>(
    builder.Configuration.GetSection("VectorSearch"));

// Configure Caching Options
builder.Services.Configure<CachingOptions>(
    builder.Configuration.GetSection(CachingOptions.SectionName));

// Configure Chatbot pipeline
builder.Services.Configure<ChatbotOptions>(
    builder.Configuration.GetSection(ChatbotOptions.SectionName));

builder.Services.Configure<GuitarAgentOptions>(
    builder.Configuration.GetSection(GuitarAgentOptions.SectionName));

// Register musical knowledge persistence and analytics services
builder.Services.AddDbContext<MusicalKnowledgeDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("MusicalKnowledge")
        ?? "Data Source=musical-knowledge.db"));

// TODO: Add missing services when available
// builder.Services.AddScoped<MusicalKnowledgeCacheService>();
// builder.Services.AddScoped<MusicalAnalyticsService>();
// builder.Services.AddScoped<AdvancedMusicalAnalyticsService>();
// builder.Services.AddScoped<EnhancedUserPersonalizationService>();
// builder.Services.AddScoped<ConfigurationReloadService>();
// builder.Services.AddSingleton<ConfigurationBroadcastService>();
// builder.Services.AddHostedService<EnhancedConfigurationWatcherService>();
// builder.Services.AddScoped<UserPersonalizationService>(sp =>
//     sp.GetRequiredService<EnhancedUserPersonalizationService>());

// Register core services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<LocalEmbeddingService>();
builder.Services.AddSingleton<VectorSearchService>();

// Music theory naming services
builder.Services.AddScoped<IChordNamingService, ChordNamingService>();

// Unified modes service
builder.Services.AddSingleton<IUnifiedModeService, UnifiedModeService>();

// Register ILGPU GPU acceleration services
builder.Services.AddSingleton<IIlgpuContextManager, IlgpuContextManager>();
builder.Services.AddSingleton(sp =>
{
    var contextManager = sp.GetRequiredService<IIlgpuContextManager>();
    return contextManager.PrimaryAccelerator;
});
builder.Services.AddSingleton<IVectorSearchStrategy, IlgpuVectorSearchStrategy>();

// Register vector search services
builder.Services.AddVectorSearchServices();

// Optionally register additional chord-related services via extension when needed
// (Keeping direct registration for naming façade above ensures availability even if extension changes)
// builder.Services.AddChordServices();

// Register Graphiti services
builder.Services.Configure<GA.Business.Graphiti.Services.GraphitiOptions>(
    builder.Configuration.GetSection("Graphiti"));

// Add Graphiti service
builder.Services
    .AddHttpClient<GA.Business.Graphiti.Services.IGraphitiService,
        GA.Business.Graphiti.Services.GraphitiService>(client =>
    {
        // When running in Aspire, the service URL will be injected via environment variables
        // When running standalone, it will use the appsettings.json BaseUrl
        var graphitiUrl = builder.Configuration["services:graphiti-service:http:0"]
                          ?? builder.Configuration["Graphiti:BaseUrl"]
                          ?? "http://localhost:8000";

        client.BaseAddress = new Uri(graphitiUrl);
        client.Timeout = TimeSpan.FromSeconds(30);
    });

// Register HttpClient for service-to-service communication
builder.Services.AddHttpClient();

// Register music room generation service
builder.Services.AddSingleton<MusicRoomService>();

// Register background job processor for room generation
builder.Services.AddHostedService<RoomGenerationBackgroundService>();

// Register chord query planner
builder.Services.AddChordQueryServices();

// Register AI and ML services (includes autonomous curation and document processing)
builder.Services.AddAiServices(builder.Configuration);

// Register voicing search services (GPU-accelerated semantic search for guitar voicings)
builder.Services.AddVoicingSearchServices(builder.Configuration);

// TODO: Add fretboard analysis services when available
// builder.Services.AddFretboardAnalysisServices();

// TODO: Add Tonal BSP services when available
// builder.Services.AddTonalBSP(builder.Configuration);

// TODO: Add health check services when available
// builder.Services.AddHealthCheckServices();

// Add caching services (memory cache, metrics, invalidation, warming)
builder.Services.AddCachingServices();

// Add HTTP client for external services
builder.Services.AddHttpClient();

// Ollama services are now registered via AddAIServices()

// Register Grothendieck service for atonal analysis
builder.Services.AddScoped<GA.Business.Core.Atonal.Grothendieck.IGrothendieckService,
    GA.Business.Core.Atonal.Grothendieck.GrothendieckService>();

// TODO: Add Proto.Actor system when available
// builder.Services.AddSingleton<ActorSystemManager>();

// TODO: Add HandPose client when GA.Business.Core.AI is available
// builder.Services.AddHttpClient<GA.Business.Core.AI.HandPose.HandPoseClient>(client =>
// {
//     // Aspire service discovery will inject the correct URL
//     client.BaseAddress = new Uri("https+http://hand-pose-service");
//     client.Timeout = TimeSpan.FromSeconds(30);
// });

// TODO: Add SoundBank client when GA.Business.Core.AI is available
// builder.Services.AddHttpClient<GA.Business.Core.AI.SoundBank.SoundBankClient>(client =>
// {
//     // Aspire service discovery will inject the correct URL
//     client.BaseAddress = new Uri("https+http://sound-bank-service");
//     client.Timeout = TimeSpan.FromMinutes(2); // Longer timeout for sound generation
// });

// TODO: Add TARS MCP client when GA.Business.Core.Diagnostics is available
// builder.Services.AddHttpClient<GA.Business.Core.Diagnostics.TarsMcpClient>(client =>
// {
//     // TARS MCP server runs locally via MCP protocol
//     var tarsMcpUrl = builder.Configuration["TarsMcp:BaseUrl"] ?? "http://localhost:9001";
//     client.BaseAddress = new Uri(tarsMcpUrl);
//     client.Timeout = TimeSpan.FromSeconds(10);
// });

// Register Hugging Face services for music/audio generation
// TODO: Add HuggingFace configuration when GA.Business.Core.AI is available
// builder.Services.Configure<GA.Business.Core.AI.HuggingFace.HuggingFaceSettings>(
//     builder.Configuration.GetSection("HuggingFace"));

// Register HuggingFaceClient (real or mock based on configuration)
// var hfSettings = builder.Configuration.GetSection("HuggingFace")
//     .Get<GA.Business.Core.AI.HuggingFace.HuggingFaceSettings>();
// if (hfSettings?.UseMockClient == true)
// {
//     // Use mock client for testing without API token
//     builder.Services
//         .AddHttpClient<GA.Business.Core.AI.HuggingFace.HuggingFaceClient,
//             GA.Business.Core.AI.HuggingFace.MockHuggingFaceClient>((serviceProvider, client) =>
//         {
//             var settings = serviceProvider
//                 .GetRequiredService<IOptions<GA.Business.Core.AI.HuggingFace.HuggingFaceSettings>>().Value;
//             client.BaseAddress = new Uri(settings.ApiUrl);
//             client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
//         });
// }
// TODO: Add HuggingFace client when GA.Business.Core.AI is available
// else
// {
//     // Use real client
//     builder.Services.AddHttpClient<GA.Business.Core.AI.HuggingFace.HuggingFaceClient>((serviceProvider, client) =>
//     {
//         var settings = serviceProvider
//             .GetRequiredService<IOptions<GA.Business.Core.AI.HuggingFace.HuggingFaceSettings>>().Value;
//         client.BaseAddress = new Uri(settings.ApiUrl);
//         client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
//     });
// }

// TODO: Add MusicGenService when GA.Business.Core.AI is available
// builder.Services.AddScoped<GA.Business.Core.AI.HuggingFace.MusicGenService>();

// Add SignalR for WebSocket support
builder.Services.AddSignalR();

// TODO: Add BSP services when available
// builder.Services.AddBSPServices();

builder.Services.AddControllers();

// Add Blazor Server and MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = MudBlazor.Defaults.Classes.Position.BottomRight;
});

// Add YARP Reverse Proxy for API Gateway
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add GraphQL services
builder.Services
    .AddGraphQLServer()
    .AddQueryType(d => d.Name("Query"))
    .AddMutationType(d => d.Name("Mutation"))
    .AddTypeExtension<DocumentQuery>()
    .AddTypeExtension<DocumentMutation>()
    .AddTypeExtension<ChordNamingQuery>()
    // TODO: Add FretboardQuery when available
    // .AddTypeExtension<FretboardQuery>()
    // TODO: Add MusicHierarchyQuery when available
    // .AddTypeExtension<MusicHierarchyQuery>()
    .AddFiltering()
    .AddSorting()
    .AddProjections();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Fix schema ID collisions by using full type names (including namespace)
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Guitar Alchemist API",
        Version = "v1",
        Description = @"RESTful API for guitar chord and music theory data

## Monad Pattern Endpoints

This API includes endpoints demonstrating functional programming patterns:

### Monadic Chords API (`/api/monadic/chords`)
Uses **Option**, **Result**, and **Try** monads for type-safe error handling:
- **Option<T>**: Represents optional values (Some/None)
- **Result<T, E>**: Represents success/failure with typed errors
- **Try<T>**: Represents operations that may throw exceptions

### Monadic Health API (`/api/monadic/health`)
Uses **Try** monad for graceful error handling in health checks:
- Always returns a response, even when services fail
- Consistent error format across all endpoints
- Composable health checks using monad patterns

### Error Response Format
All monadic endpoints return consistent error responses:
```json
{
  ""error"": ""ErrorType"",
  ""message"": ""Human-readable message"",
  ""details"": ""Technical details""
}
```

### AI Music Generation
New microservices for interactive guitar playing:
- **HandPoseService**: Computer vision for guitar hand pose detection
- **SoundBankService**: AI-powered guitar sound generation
- **GuitarPlayingController**: Orchestrates full pipeline (image → positions → sounds)
",
        Contact = new OpenApiContact
        {
            Name = "Guitar Alchemist",
            Url = new Uri("https://github.com/GuitarAlchemist/ga")
        }
    });

    // Include XML comments for better documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    // Add example schemas for monad patterns
    c.MapType<Option<object>>(() => new OpenApiSchema
    {
        Type = "object",
        Description = "Option monad - represents an optional value (Some/None)",
        Properties = new Dictionary<string, OpenApiSchema>
        {
            ["isSome"] = new() { Type = "boolean", Description = "True if value is present" },
            ["isNone"] = new() { Type = "boolean", Description = "True if value is absent" },
            ["value"] = new() { Type = "object", Description = "The wrapped value (if present)" }
        }
    });

    // TODO: Add ErrorResponse MapType when available
    // c.MapType<ErrorResponse>(() => new OpenApiSchema
    // {
    //     Type = "object",
    //     Description = "Standard error response for monad pattern endpoints",
    //     Required = new HashSet<string> { "error", "message" },
    //     Properties = new Dictionary<string, OpenApiSchema>
    //     {
    //         ["error"] = new() { Type = "string", Description = "Error type (e.g., ValidationError, NotFound)" },
    //         ["message"] = new() { Type = "string", Description = "Human-readable error message" },
    //         ["details"] = new() { Type = "string", Description = "Technical error details" }
    //     },
    //     Example = new OpenApiObject
    //     {
    //         ["error"] = new OpenApiString("ValidationError"),
    //         ["message"] = new OpenApiString("Invalid chord quality"),
    //         ["details"] = new OpenApiString("Quality 'InvalidQuality' not found in database")
    //     }
    // });

    c.EnableAnnotations();
});

// Add CORS support with WebSocket support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5176", "http://localhost:5177")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR WebSocket connections
    });
});

builder.Services.AddProblemDetails();

// TODO: Fix rate limiting - API has changed in .NET 9
// Add rate limiting with different policies for regular vs semantic endpoints
//builder.Services.AddRateLimiter(options =>
//{
//    options.AddFixedWindowLimiter("regular", limiterOptions =>
//    {
//        limiterOptions.PermitLimit = 100;
//        limiterOptions.Window = TimeSpan.FromMinutes(1);
//        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
//        limiterOptions.QueueLimit = 10;
//    });
//
//    options.AddFixedWindowLimiter("semantic", limiterOptions =>
//    {
//        limiterOptions.PermitLimit = 10;
//        limiterOptions.Window = TimeSpan.FromMinutes(1);
//        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
//        limiterOptions.QueueLimit = 2;
//    });
//
//    options.AddFixedWindowLimiter("admin", limiterOptions =>
//    {
//        limiterOptions.PermitLimit = 5;
//        limiterOptions.Window = TimeSpan.FromMinutes(1);
//        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
//        limiterOptions.QueueLimit = 0;
//    });
//
//    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
//    {
//        return RateLimitPartition.GetFixedWindowLimiter(
//            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
//            factory: _ => new FixedWindowRateLimiterOptions
//            {
//                PermitLimit = 200,
//                Window = TimeSpan.FromMinutes(1)
//            });
//    });
//
//    options.OnRejected = async (context, token) =>
//    {
//        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
//
//        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
//        {
//            await context.HttpContext.Response.WriteAsJsonAsync(new
//            {
//                error = "Too many requests",
//                message = "Rate limit exceeded. Please try again later.",
//                retryAfter = retryAfter.TotalSeconds
//            }, cancellationToken: token);
//        }
//        else
//        {
//            await context.HttpContext.Response.WriteAsJsonAsync(new
//            {
//                error = "Too many requests",
//                message = "Rate limit exceeded. Please try again later."
//            }, cancellationToken: token);
//        }
//    };
//});

var app = builder.Build();

// Configure the HTTP request pipeline.

// Add custom middleware (order matters!)

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Guitar Alchemist API v1");
        c.RoutePrefix = "swagger";
        c.DocumentTitle = "Guitar Alchemist API Documentation";
    });
}


// Use CORS
app.UseCors("AllowAll");

// Use rate limiting (must be before UseAuthorization)
// TODO: Fix rate limiting for .NET 9 - API changed
// app.UseRateLimiter();

// Enable static files for Blazor
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

// Map YARP Reverse Proxy routes (API Gateway)
app.MapReverseProxy();

// Map GraphQL endpoint
app.MapGraphQL();

// Map SignalR hubs
app.MapHub<ChatbotHub>("/hubs/chatbot");
// TODO: Add ConfigurationUpdateHub when available
// app.MapHub<ConfigurationUpdateHub>("/hubs/configuration");

// Map Blazor components
app.MapRazorComponents<GaApi.Components.App>()
    .AddInteractiveServerRenderMode();

// Map Aspire default endpoints (health checks, liveness)
app.MapDefaultEndpoints();

// Add API info endpoint
app.MapGet("/api", () => new
{
    message = "Guitar Alchemist API",
    version = "1.0.0",
    timestamp = DateTime.UtcNow,
    documentation = "/swagger"
});

app.Run();

static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(response => response.StatusCode == HttpStatusCode.TooManyRequests)
        .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromMilliseconds(200 * Math.Pow(2, retryAttempt)));
}

static IAsyncPolicy<HttpResponseMessage> CreateCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
}

// Make the implicit Program class public for integration testing
namespace GaApi
{
    public partial class Program
    {
    }
}
