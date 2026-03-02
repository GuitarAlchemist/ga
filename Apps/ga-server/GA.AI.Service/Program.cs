#pragma warning disable SKEXP0001
using System.Reflection;
using Hellang.Middleware.ProblemDetails;
using System.Text.Json;
using System.Threading.RateLimiting;
using GA.AI.Service.Models;
using GA.AI.Service.Services;
using GA.Business.ML.AI.Benchmarks;
using AllProjects.ServiceDefaults;
using GA.Business.ML.Extensions;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Tabs;
using GA.Business.ML.Musical.Enrichment;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Naturalness;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Wavelets;
using GA.Domain.Core.Instruments;
using GA.Domain.Services.AI.Benchmarks;
using GA.Domain.Services.Fretboard.Analysis;
using GA.Domain.Services.Fretboard.Shapes;

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
builder.Services.AddSingleton<IVectorIndex>(sp => new QdrantVectorIndex("ga-qdrant")); // Docker host
builder.Services.AddScoped<ISpectralRetrievalService, SpectralRetrievalService>();
// TODO: These types are in GaChatbot project - need to be moved or referenced
// builder.Services.AddSingleton<GroundedPromptBuilder>();
// builder.Services.AddSingleton<ResponseValidator>();
// builder.Services.AddSingleton<IGroundedNarrator, OllamaGroundedNarrator>();
// builder.Services.AddSingleton<SpectralRagOrchestrator>();
builder.Services.AddSingleton(Tuning.Default);
builder.Services.AddSingleton<FretboardPositionMapper>();
builder.Services.AddSingleton<IMlNaturalnessRanker, MlNaturalnessRanker>();
builder.Services.AddSingleton<PhysicalCostService>();
builder.Services.AddSingleton<GA.Business.ML.Abstractions.IEmbeddingGenerator, MusicalEmbeddingGenerator>();
builder.Services.AddSingleton<StyleProfileService>();
builder.Services.AddSingleton<NextChordSuggestionService>();
builder.Services.AddSingleton<ModulationAnalyzer>();
builder.Services.AddSingleton<AdvancedTabSolver>();
builder.Services.AddSingleton<AdvancedTabSolver>();
builder.Services.AddSingleton<AlternativeFingeringService>();
// New Dependencies
builder.Services.AddSingleton<ModalFlavorService>();
builder.Services.AddSingleton<VoicingExplanationService>();
builder.Services.AddSingleton<ProgressionSignalService>();
builder.Services.AddSingleton<StyleClassifierService>();

// TODO: These types are in GaChatbot project - need to be moved or referenced
// builder.Services.AddSingleton<TabPresentationService>();
builder.Services.AddSingleton<TabAnalysisService>();
builder.Services.AddSingleton<MusicalEmbeddingGenerator>(); // Concrete for ProductionOrchestrator
// builder.Services.AddSingleton<TabAwareOrchestrator>();
// builder.Services.AddSingleton<ProductionOrchestrator>(); // TODO: ProductionOrchestrator is in GaChatbot project
builder.Services.AddMemoryCache();
#pragma warning disable EXTEXP0018
builder.Services.AddHybridCache();
#pragma warning restore EXTEXP0018
builder.Services.AddControllers()
    .AddJsonOptions(options => { options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase; });

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
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.Identity?.Name ?? httpContext.Request.Headers.Host.ToString(),
            partition => new()
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

app.MapGet("/api/stats", async (MongoDbService mongo) =>
    {
        var count = await mongo.GetTotalChordCountAsync();
        // Assuming nomic-embed-text (768) as configured in docker-compose
        return Results.Ok(new { totalVoicings = count, embeddingDimensions = 768 });
    })
    .WithName("GetStats");

app.Run();
