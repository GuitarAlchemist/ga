#pragma warning disable SKEXP0001
using System.Reflection;
using Hellang.Middleware.ProblemDetails;

using System.Text.Json;
using GA.AI.Service.Models;
using GA.AI.Service.Services;
using GA.Business.Core.AI.Benchmarks;
using GA.Business.ML.AI.Benchmarks;
using Microsoft.Extensions.Caching.Memory;
using AllProjects.ServiceDefaults;
using AllProjects.ServiceDefaults;
using GA.Business.Core.Fretboard.Shapes;
using GaChatbot.Services;
using GaChatbot.Abstractions;
using GA.Business.ML.Extensions;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Tabs;
using GA.Business.ML.Musical.Enrichment;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Wavelets;
using GA.Business.Core.Fretboard.Voicings.Search;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Redis distributed caching
builder.AddRedisDistributedCache("redis");

// Add controllers
// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<PerformanceMetricsService>();
// builder.Services.AddScoped<SemanticSearchService>();
// builder.Services.AddScoped<EnhancedUserPersonalizationService>();
// builder.Services.AddScoped<VectorSearchService>();
// builder.Services.AddScoped<EnhancedVectorSearchService>();
builder.Services.AddScoped<ICachingService, CachingService>();
builder.Services.AddScoped<IShapeGraphBuilder, ShapeGraphBuilder>();
builder.Services.AddScoped<ActorSystemManager>();

// Benchmarks
builder.Services.AddSingleton<IBenchmark, VoicingQualityBenchmark>();
builder.Services.AddSingleton<BenchmarkRunner>();

// AI Services
builder.Services.AddHttpClient<IOllamaService, OllamaService>();
builder.Services.AddSingleton<NotebookExecutionService>();

// Chatbot Services
builder.Services.AddGuitarAlchemistAI();
builder.Services.AddSingleton<IVectorIndex>(sp => new QdrantVectorIndex("ga-qdrant", 6334)); // Docker host
builder.Services.AddScoped<SpectralRetrievalService>();
builder.Services.AddSingleton<GroundedPromptBuilder>();
builder.Services.AddSingleton<ResponseValidator>();
builder.Services.AddSingleton<IGroundedNarrator, OllamaGroundedNarrator>();
builder.Services.AddSingleton<SpectralRagOrchestrator>();
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Tuning>(GA.Business.Core.Fretboard.Tuning.Default);
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Analysis.FretboardPositionMapper>();
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Analysis.IMlNaturalnessRanker, GA.Business.ML.Naturalness.MlNaturalnessRanker>();
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Analysis.PhysicalCostService>();
builder.Services.AddSingleton<GA.Business.Core.Abstractions.IEmbeddingGenerator, GA.Business.ML.Embeddings.MusicalEmbeddingGenerator>();
builder.Services.AddSingleton<GA.Business.ML.Retrieval.StyleProfileService>();
builder.Services.AddSingleton<GA.Business.ML.Retrieval.NextChordSuggestionService>();
builder.Services.AddSingleton<GA.Business.ML.Retrieval.ModulationAnalyzer>();
builder.Services.AddSingleton<GA.Business.ML.Tabs.AdvancedTabSolver>();
builder.Services.AddSingleton<GA.Business.ML.Tabs.AdvancedTabSolver>();
builder.Services.AddSingleton<GA.Business.ML.Tabs.AlternativeFingeringService>();
// New Dependencies
builder.Services.AddSingleton<ModalFlavorService>();
builder.Services.AddSingleton<VoicingExplanationService>();
builder.Services.AddSingleton<ProgressionSignalService>();
builder.Services.AddSingleton<StyleClassifierService>();

builder.Services.AddSingleton<GaChatbot.Services.TabPresentationService>();
builder.Services.AddSingleton<TabAnalysisService>(); // Missing
builder.Services.AddSingleton<MusicalEmbeddingGenerator>(); // Concrete for ProductionOrchestrator
builder.Services.AddSingleton<GaChatbot.Services.TabAwareOrchestrator>();
builder.Services.AddSingleton<GaChatbot.Services.ProductionOrchestrator>();

builder.Services.AddMemoryCache();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "AI Service",
        Version = "v1",
        Description = "AI/ML operations, embeddings, semantic search"
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }

    c.EnableAnnotations();
});

// Add problem details middleware
builder.Services.AddProblemDetails(options =>
{
    options.IncludeExceptionDetails = (ctx, ex) => builder.Environment.IsDevelopment();
    options.MapToStatusCode<NotImplementedException>(StatusCodes.Status501NotImplemented);
    options.MapToStatusCode<ArgumentException>(StatusCodes.Status400BadRequest);
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(httpContext =>
        System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            factory: partition => new System.Threading.RateLimiting.FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 10,
                Window = TimeSpan.FromMinutes(1)
            }));
});

var app = builder.Build();

// Configure middleware pipeline
app.UseProblemDetails();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "AI Service v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
app.UseRateLimiter();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();
