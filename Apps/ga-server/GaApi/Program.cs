using System.Threading.RateLimiting;
using AllProjects.ServiceDefaults;
using GA.Business.Core.Session;
using GaApi.Extensions;
using GaApi.Hubs;
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

// Register contextual chord services
builder.Services.AddSingleton<ContextualChordService>();
builder.Services.AddSingleton<VoicingFilterService>();

// Add session context provider (scoped = one per HTTP request)
builder.Services.AddSessionContextScoped();

// Add HTTP client for external services
builder.Services.AddHttpClient();

// Add SignalR for WebSocket support
builder.Services.AddSignalR();

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
        policy.WithOrigins("http://localhost:5173", "http://localhost:5176", "http://localhost:5177")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials(); // Required for SignalR WebSocket connections
    });
});

builder.Services.AddProblemDetails();

// Per-IP rate limiting: 60 requests/minute, queue up to 5 overflow requests
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? ctx.Request.Headers.Host.ToString(),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 5
            }));
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

// Enable static files for Blazor
app.UseStaticFiles();
app.UseAntiforgery();

app.MapControllers();

app.MapGraphQL();

app.MapGet("/api/stats", async (VectorSearchService vs) => Results.Ok(await vs.GetStatsAsync())).WithName("GetStats");
app.MapGet("/stats", async (VectorSearchService vs) => Results.Ok(await vs.GetStatsAsync())).WithName("GetStatsRoot");

// Map YARP Reverse Proxy routes (API Gateway)
app.MapReverseProxy();

// Map SignalR hubs
app.MapHub<ChatbotHub>("/hubs/chatbot");

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
