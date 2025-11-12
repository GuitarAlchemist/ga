using GA.DocumentProcessing.Service.Models;
using GA.DocumentProcessing.Service.Services;
using Microsoft.Extensions.Caching.Memory;
using System.Reflection;
using Hellang.Middleware.ProblemDetails;

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add Redis distributed caching
builder.AddRedisDistributedCache("redis");

// Configure MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB"));

// Register services
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<PerformanceMetricsService>();
builder.Services.AddScoped<DocumentIngestionService>();
builder.Services.AddScoped<PdfProcessorService>();
builder.Services.AddScoped<MarkdownProcessorService>();
builder.Services.AddScoped<OllamaSummarizationService>();
builder.Services.AddScoped<KnowledgeExtractionService>();
builder.Services.AddScoped<YouTubeTranscriptService>();
builder.Services.AddScoped<RetroactionLoopOrchestrator>();
builder.Services.AddMemoryCache();

// Register embedding service
builder.Services.AddScoped<GA.Data.SemanticKernel.Embeddings.IBatchEmbeddingService>(sp =>
{
    var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Ollama");
    return new GA.Data.SemanticKernel.Embeddings.BatchOllamaEmbeddingService(
        httpClient,
        builder.Configuration["Ollama:EmbeddingModel"] ?? "nomic-embed-text");
});

// Add HTTP client for Ollama
builder.Services.AddHttpClient("Ollama", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddStandardResilienceHandler();

// Add HTTP client for Google Cloud
builder.Services.AddHttpClient("GoogleCloud", client =>
{
    client.Timeout = TimeSpan.FromMinutes(10);
})
.AddStandardResilienceHandler();

builder.Services.AddControllers();

// Add API documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "DocumentProcessing Service",
        Version = "v1",
        Description = @"NotebookLM-style document processing for music theory knowledge base.

Features:
- PDF/Markdown ingestion and parsing
- Ollama-based summarization and analysis
- Structured knowledge extraction (chords, scales, progressions, techniques)
- Vector embeddings generation
- MongoDB storage with semantic search capabilities

Workflow:
1. Upload document (PDF, Markdown, or URL)
2. Extract text and structure
3. Generate summary using Ollama
4. Extract music theory concepts
5. Create embeddings for semantic search
6. Store in MongoDB for retrieval"
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
    options.MapToStatusCode<InvalidOperationException>(StatusCodes.Status400BadRequest);
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
                PermitLimit = 50, // Lower limit for document processing
                QueueLimit = 5,
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "DocumentProcessing Service v1");
        c.RoutePrefix = string.Empty;
    });
}

app.UseCors("AllowAll");
app.UseRateLimiter();

app.MapControllers();
app.MapDefaultEndpoints();

app.Run();

