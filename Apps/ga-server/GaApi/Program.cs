using System.Net;
using System.Reflection;
using AllProjects.ServiceDefaults;
using GaApi.Configuration;
using GaApi.Extensions;
using GaApi.Hubs;
using GaApi.Models;
using GaApi.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MudBlazor.Services;
using Polly;
using Polly.Extensions.Http;
using Path = System.IO.Path;

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

// Register core services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<LocalEmbeddingService>();
builder.Services.AddSingleton<VectorSearchService>();

// Register ILGPU GPU acceleration services
builder.Services.AddSingleton<IIlgpuContextManager, IlgpuContextManager>();
builder.Services.AddSingleton<ILGPU.Runtime.Accelerator>(sp =>
{
    var contextManager = sp.GetRequiredService<IIlgpuContextManager>();
    return contextManager.PrimaryAccelerator!;
});
builder.Services.AddSingleton<IVectorSearchStrategy, IlgpuVectorSearchStrategy>();

// Register vector search services
builder.Services.AddVectorSearchServices();

// Register AI services (Ollama, etc.)
builder.Services.AddAiServices(builder.Configuration);

// Register voicing search services (GPU-accelerated semantic search for guitar voicings)
builder.Services.AddVoicingSearchServices(builder.Configuration);

// Add caching services
builder.Services.AddCachingServices();

// Add HTTP client for external services
builder.Services.AddHttpClient();

// Add SignalR for WebSocket support
builder.Services.AddSignalR();

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

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Fix schema ID collisions by using full type names (including namespace)
    c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Guitar Alchemist Chatbot API",
        Version = "v1",
        Description = @"RESTful API for Guitar Alchemist Chatbot",
        Contact = new OpenApiContact
        {
            Name = "Guitar Alchemist",
            Url = new Uri("https://github.com/GuitarAlchemist/ga")
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

// Make the implicit Program class public for integration testing
namespace GaApi
{
    public partial class Program
    {
    }
}
