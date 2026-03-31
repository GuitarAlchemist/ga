using System.Threading.RateLimiting;
using AllProjects.ServiceDefaults;
using GA.Business.Core.Session;
using GaApi.Extensions;
using GaApi.Hubs;
using GaApi.Controllers;
using GaApi.Services;
using GaApi.GraphQL.Queries;
using Microsoft.AspNetCore.RateLimiting;
using MudBlazor;
using MudBlazor.Services;
using Path = System.IO.Path;

var builder = WebApplication.CreateBuilder(args);

// Add shared configuration
builder.Configuration.AddJsonFile(Path.Combine(AppContext.BaseDirectory, "../../appsettings.Shared.json"), true, true);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Redis distributed cache (Aspire integration)
builder.AddRedisDistributedCache("redis");

// Add services to the container.

// Register core infrastructure (MongoDB, ILGPU, Redis)
builder.Services.AddGaInfrastructure(builder.Configuration);

// Register AI services (Ollama, Embeddings, Vector Search, Chatbot)
builder.Services.AddAiServices(builder.Configuration);

// Register voicing search services (GPU-accelerated semantic search for guitar voicings)
builder.Services.AddVoicingSearchServices(builder.Configuration);

// Add caching services
builder.Services.AddCachingServices(builder.Configuration);

// Register monadic services (health check, chords)
builder.Services.AddMonadicHealthCheckService();
builder.Services.AddMonadicChordService();

// Register standard health check service (used by HealthController)
builder.Services.AddSingleton<IHealthCheckService, HealthCheckService>();

// Register contextual chord services
builder.Services.AddSingleton<ContextualChordService>();
builder.Services.AddSingleton<VoicingFilterService>();

// Shared LLM concurrency gate (3 parallel calls) — applied to both hub and REST controller
builder.Services.AddSingleton<ILlmConcurrencyGate, LlmConcurrencyGate>();

// Add session context provider (scoped = one per HTTP request)
builder.Services.AddSessionContextScoped();

// Add HTTP client for external services
builder.Services.AddHttpClient();

// Add SignalR for WebSocket support
builder.Services.AddSignalR();

// Belief state service — reads/updates tetravalent belief files
builder.Services.AddSingleton<BeliefStateService>();

// Visual critic — Claude vision analysis of Prime Radiant screenshots
builder.Services.AddSingleton<VisualCriticService>();

// Pipeline execution — runs brainstorm/plan/build/review/compound via Claude Code CLI
builder.Services.AddSingleton<PipelineExecutionService>();

// Governance file watcher — pushes updates via SignalR when governance files change
builder.Services.AddHostedService<GovernanceWatcherService>();

// Register GovernanceController for DI (used by GovernanceHub)
builder.Services.AddTransient<GovernanceController>();

builder.Services.AddControllers()
    .ConfigureApplicationPartManager(manager =>
    {
        // GA.Fretboard.Service is referenced for shared types but runs as its own service.
        // Exclude its assembly from controller discovery to prevent AmbiguousMatchException
        // (e.g., both assemblies define ContextualChordsController on the same route prefix).
        var fretboardPart = manager.ApplicationParts
            .FirstOrDefault(p => p.Name == "GA.Fretboard.Service");
        if (fretboardPart != null)
            manager.ApplicationParts.Remove(fretboardPart);
    });

// Add Blazor Server and MudBlazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
});

// Add YARP Reverse Proxy for API Gateway
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add GraphQL Server
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddTypeExtension<ChordNamingQuery>()
    .AddTypeExtension<MusicTheoryQuery>()
    .AddTypeExtension<DomainSchemaQuery>()
    .AddTypeExtension<MongoCollectionsQuery>()
    .AddProjections()
    .AddFiltering()
    .AddSorting();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Fix schema ID collisions by using full type names (including namespace)
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    c.SwaggerDoc("v1", new()
    {
        Title = "Guitar Alchemist Chatbot API",
        Version = "v1",
        Description = @"RESTful API for Guitar Alchemist Chatbot",
        Contact = new()
        {
            Name = "Guitar Alchemist",
            Url = new("https://github.com/GuitarAlchemist/ga")
        }
    });

    c.EnableAnnotations();
});

// Add CORS support with WebSocket support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://localhost:5176", "http://localhost:5177", "http://localhost:8501")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR WebSocket connections
    });
});

builder.Services.AddProblemDetails();

// Per-IP rate limiting: 300 requests/minute to support Prime Radiant multi-panel polling
// (health check every 30s + N panels × refresh intervals + governance queries + grammar API)
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        // Exempt health/status endpoints from rate limiting
        var path = ctx.Request.Path.Value ?? "";
        if (path.Contains("/status") || path.Contains("/health"))
        {
            return RateLimitPartition.GetNoLimiter("health");
        }
        return RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.Request.Headers.Host.ToString(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

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
app.UseRateLimiter();

// Dev-only guard: block unauthenticated access to the chatbot API in non-Development environments.
// CORS + rate limiting provide weak protection; this gate prevents accidental public exposure
// while full bearer auth is designed. Replace with [Authorize] + AddAuthentication once a
// token/key issuance strategy is in place (see todo 036).
if (!app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/api/chatbot")
            && !ctx.Request.Headers.ContainsKey("X-Api-Key"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        await next(ctx);
    });
}

// Enable static files for Blazor
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapGraphQL();

app.MapGet("/api/stats", async (VectorSearchService vs) => Results.Ok(await vs.GetStatsAsync())).WithName("GetStats");
// /stats (without /api prefix) kept for backwards compatibility with older clients
app.MapGet("/stats", async (VectorSearchService vs) => Results.Ok(await vs.GetStatsAsync())).WithName("GetStatsRoot");

// Map YARP Reverse Proxy routes (API Gateway)
app.MapReverseProxy();

// Map SignalR hubs
app.MapHub<ChatbotHub>("/hubs/chatbot");
app.MapHub<GovernanceHub>("/hubs/governance");
app.MapHub<PipelineHub>("/hubs/pipeline");

// Map Aspire default endpoints (health checks, liveness)
app.MapDefaultEndpoints();

// Add API info endpoint
app.MapGet("/api", () => new
{
    message = "Guitar Alchemist Chatbot API",
    version = "1.0.0",
    timestamp = DateTime.UtcNow,
    documentation = "/swagger"
});

app.Run();

namespace GaApi
{
    public partial class Program
    {
    }
}
