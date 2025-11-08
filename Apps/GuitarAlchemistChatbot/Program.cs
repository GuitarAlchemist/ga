using System.ClientModel;
using GA.Business.AI.AI.HuggingFace;
using GA.Business.Web;
using GuitarAlchemistChatbot.Components;
using GuitarAlchemistChatbot.Plugins;
using GuitarAlchemistChatbot.Services;
using Microsoft.Extensions.Options;
using OpenAI;

// Check for CLI mode
if (args.Length > 0 && args[0] == "--cli")
{
    Console.WriteLine("=== Guitar Alchemist Chatbot - CLI Mode ===");
    Console.WriteLine();

    // Set up minimal services for CLI
    var services = new ServiceCollection();
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));

    // Add embedding generator
    var cliEmbeddingGenerator = new DemoEmbeddingGenerator();
    services.AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>(cliEmbeddingGenerator);
    services.AddSingleton<ILoggerFactory>(loggerFactory);
    services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));

    var serviceProvider = services.BuildServiceProvider();
    var logger = serviceProvider.GetRequiredService<ILogger<InMemoryVectorStoreService>>();

    // Create and test the vector store
    var vectorStore = new InMemoryVectorStoreService(cliEmbeddingGenerator, logger, loggerFactory);

    Console.WriteLine("Starting vector store indexing...");
    var result = await vectorStore.IndexKnowledgeBaseAsync();

    Console.WriteLine();
    Console.WriteLine("Indexing Result:");
    Console.WriteLine($"  Success: {result.Success}");
    Console.WriteLine($"  Documents Indexed: {result.DocumentsIndexed}");
    Console.WriteLine($"  Duration: {result.Duration.TotalSeconds:F2}s");
    Console.WriteLine($"  Message: {result.Message}");
    Console.WriteLine();

    // Test a search
    Console.WriteLine("Testing search for 'major seventh chord'...");
    var searchResults = await vectorStore.SearchAsync("major seventh chord", 5);

    Console.WriteLine($"Found {searchResults.Count()} results:");
    foreach (var searchResult in searchResults.Take(5))
    {
        Console.WriteLine(
            $"  - [{searchResult.Category}] {searchResult.Id} (similarity: {searchResult.Similarity:F3})");
        Console.WriteLine($"    {searchResult.Content.Substring(0, Math.Min(100, searchResult.Content.Length))}...");
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);

// Add Aspire service defaults (telemetry, health checks, service discovery)
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Configure AI services with fallback for demo mode
var openAiApiKey = builder.Configuration["OpenAI:ApiKey"];
var isApiKeyConfigured = !string.IsNullOrEmpty(openAiApiKey);

IChatClient chatClient;
IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;

if (isApiKeyConfigured)
{
    Console.WriteLine("✅ OpenAI API key configured. Full AI functionality enabled.");
    var credential = new ApiKeyCredential(openAiApiKey!);
    var openAiOptions = new OpenAIClientOptions();
    var openAiClient = new OpenAIClient(credential, openAiOptions);

    chatClient = openAiClient.AsChatClient("gpt-4o-mini");
    embeddingGenerator = openAiClient.AsEmbeddingGenerator("text-embedding-3-small");
}
else
{
    Console.WriteLine("ℹ️  OpenAI API key not configured. Running fully locally (demo mode).");
    Console.WriteLine("   No API keys are required — all core features work locally.");
    Console.WriteLine("   Optional: set OpenAI:ApiKey in appsettings.json if you want to try cloud models.");

    // Use in-memory demo implementations with full functionality
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    var demoLogger = loggerFactory.CreateLogger<DemoChatClient>();

    chatClient = new DemoChatClient(demoLogger);
    embeddingGenerator = new DemoEmbeddingGenerator();
}

// Register HTTP clients
builder.Services.AddHttpClient<ChordSearchService>();

// Register GaApi HTTP client with Aspire service discovery
builder.Services.AddHttpClient<GaApiClient>(client =>
{
    // Use Aspire service discovery to find GaApi
    client.BaseAddress = new Uri("https+http://gaapi");
});

// Register Graphiti HTTP client with Aspire service discovery
builder.Services.AddHttpClient<GraphitiClient>(client =>
{
    // Use Aspire service discovery to find Graphiti service
    var graphitiUrl = builder.Configuration["services:graphiti-service:http:0"]
                      ?? builder.Configuration["Graphiti:BaseUrl"]
                      ?? "http://localhost:8000";

    client.BaseAddress = new Uri(graphitiUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

// Register web integration services (web scraping, feeds, search)
builder.Services.AddWebIntegrationServices();

// Add Tonal BSP services for advanced musical analysis
//builder.Services.AddTonalBSP(builder.Configuration);

// Register Hugging Face services for music/audio generation
builder.Services.Configure<HuggingFaceSettings>(
    builder.Configuration.GetSection("HuggingFace"));

builder.Services.AddHttpClient<HuggingFaceClient>((serviceProvider, client) =>
{
    var settings = serviceProvider.GetRequiredService<IOptions<HuggingFaceSettings>>()
        .Value;
    client.BaseAddress = new Uri(settings.ApiUrl);
    client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
});

builder.Services.AddScoped<MusicGenService>();

// Register services
builder.Services.AddScoped<ChordSearchService>();
builder.Services.AddScoped<ConversationContextService>();
builder.Services.AddScoped<GuitarAlchemistFunctions>();

// Register monadic services
builder.Services.AddScoped<MonadicChordSearchService>();

// Register Semantic Kernel plugins for analyzer integration
builder.Services.AddScoped<ChordProgressionPlugin>();
builder.Services.AddScoped<PracticePathPlugin>();
builder.Services.AddScoped<ShapeGraphPlugin>();
builder.Services.AddScoped<GraphitiPlugin>();
builder.Services.AddScoped<BSPDungeonPlugin>();

builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Map Aspire default endpoints (health checks, liveness)
app.MapDefaultEndpoints();

app.Run();
