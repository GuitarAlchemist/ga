var builder = DistributedApplication.CreateBuilder(args);

// Initialize ILGPU for GPU acceleration
// This enables cross-platform GPU support (NVIDIA CUDA, AMD ROCm, CPU fallback)
// See: https://ilgpu.net/docs/01-primers/01-setting-up-ilgpu/
var ilgpuInitialized = false;
try
{
    // ILGPU context will be created lazily when first vector search is performed
    // No explicit initialization needed here - services will handle it
    ilgpuInitialized = true;
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: ILGPU initialization failed: {ex.Message}");
    Console.WriteLine("Falling back to CPU-based vector search");
}

// Add Redis for distributed caching
var redis = builder.AddRedis("redis")
    .WithRedisCommander()
    .WithDataVolume("ga-redis-data");

// Add FalkorDB (Redis-compatible graph database for Graphiti)
// Changed port from 6379 to 6380 to avoid conflict with Docker/WSL
var falkordb = builder.AddContainer("falkordb", "falkordb/falkordb", "edge")
    .WithHttpEndpoint(6380, 6379, "tcp")
    .WithBindMount("ga-falkordb-data", "/data");

// Add MongoDB with MongoExpress UI
var mongodb = builder.AddMongoDB("mongodb")
    .WithMongoExpress()
    .WithDataVolume("ga-mongodb-data");

var mongoDatabase = mongodb.AddDatabase("guitar-alchemist");

// Add Graphiti Service (Python-based temporal knowledge graph)
// Must be added before GaApi so it can be referenced
builder.AddContainer("graphiti-service", "ga-graphiti-service")
    .WithDockerfile("../Apps/ga-graphiti-service")
    .WithHttpEndpoint(8000, 8000, "http")
    .WithEnvironment("API_HOST", "0.0.0.0")
    .WithEnvironment("API_PORT", "8000")
    .WithEnvironment("DEBUG", "false")
    .WithEnvironment("MONGODB_URI", mongoDatabase)
    .WithEnvironment("FALKORDB_HOST", "falkordb")
    .WithEnvironment("FALKORDB_PORT", "6380")
    .WithEnvironment("FALKORDB_DATABASE", "graphiti")
    .WithExternalHttpEndpoints();

// Add HandPoseService (Python-based hand pose detection using MediaPipe)
builder.AddContainer("hand-pose-service", "hand-pose-service")
    .WithDockerfile("../Apps/hand-pose-service")
    .WithHttpEndpoint(8081, 8080, "http")
    .WithEnvironment("PORT", "8080")
    .WithEnvironment("LOG_LEVEL", "INFO")
    .WithExternalHttpEndpoints();

// Add SoundBankService (Python-based AI sound generation using MusicGen)
builder.AddContainer("sound-bank-service", "sound-bank-service")
    .WithDockerfile("../Apps/sound-bank-service")
    .WithHttpEndpoint(8082, 8080, "http")
    .WithEnvironment("PORT", "8080")
    .WithEnvironment("LOG_LEVEL", "INFO")
    .WithReference(redis)
    .WithReference(mongoDatabase)
    .WithBindMount("ga-sound-samples", "/app/samples")
    .WithExternalHttpEndpoints();

// ============================================================================
// Microservices
// ============================================================================

// Add GA.MusicTheory.Service (Port 7001)
var musicTheoryService = builder.AddProject("music-theory-service", @"..\Apps\ga-server\GA.MusicTheory.Service\GA.MusicTheory.Service.csproj")
    .WithReference(mongoDatabase)
    .WithReference(redis)
    .WithExternalHttpEndpoints();

// Add GA.BSP.Service (Port 7002)
// TODO: Uncomment when service implementation is complete
// var bspService = builder.AddProject("bsp-service", @"..\Apps\ga-server\GA.BSP.Service\GA.BSP.Service.csproj")
//     .WithReference(mongoDatabase)
//     .WithReference(redis)
//     .WithExternalHttpEndpoints();

// Add GA.AI.Service (Port 7003)
// TODO: Uncomment when service implementation is complete
// var aiService = builder.AddProject("ai-service", @"..\Apps\ga-server\GA.AI.Service\GA.AI.Service.csproj")
//     .WithReference(mongoDatabase)
//     .WithReference(redis)
//     .WithExternalHttpEndpoints();

// Add GA.Knowledge.Service (Port 7004)
// TODO: Uncomment when service implementation is complete
// var knowledgeService = builder.AddProject("knowledge-service", @"..\Apps\ga-server\GA.Knowledge.Service\GA.Knowledge.Service.csproj")
//     .WithReference(mongoDatabase)
//     .WithReference(redis)
//     .WithExternalHttpEndpoints();

// Add GA.Fretboard.Service (Port 7005)
// TODO: Uncomment when service implementation is complete
// var fretboardService = builder.AddProject("fretboard-service", @"..\Apps\ga-server\GA.Fretboard.Service\GA.Fretboard.Service.csproj")
//     .WithReference(mongoDatabase)
//     .WithReference(redis)
//     .WithExternalHttpEndpoints();

// Add GA.Analytics.Service (Port 7006)
// TODO: Uncomment when service implementation is complete
// var analyticsService = builder.AddProject("analytics-service", @"..\Apps\ga-server\GA.Analytics.Service\GA.Analytics.Service.csproj")
//     .WithReference(mongoDatabase)
//     .WithReference(redis)
//     .WithExternalHttpEndpoints();

// ============================================================================
// API Gateway
// ============================================================================

// Add GaApi (API Gateway with YARP reverse proxy)
builder.AddProject("gaapi", @"..\Apps\ga-server\GaApi\GaApi.csproj")
    .WithReference(mongoDatabase)
    .WithReference(redis)
    .WithReference(musicTheoryService)
    // TODO: Uncomment when service implementation is complete
    // .WithReference(bspService)
    // TODO: Uncomment when service implementation is complete
    // .WithReference(aiService)
    // TODO: Uncomment when service implementation is complete
    // .WithReference(knowledgeService)
    // TODO: Uncomment when service implementation is complete
    // .WithReference(fretboardService)
    // TODO: Uncomment when service implementation is complete
    // .WithReference(analyticsService)
    .WithExternalHttpEndpoints();

// Add GuitarAlchemistChatbot (Blazor chatbot)
builder.AddProject("chatbot", @"..\Apps\GuitarAlchemistChatbot\GuitarAlchemistChatbot.csproj")
    .WithReference(mongoDatabase)
    .WithReference(redis)
    .WithExternalHttpEndpoints();

// Add ScenesService (GLB scene builder and server)
builder.AddProject("scenes-service", @"..\Apps\ScenesService\ScenesService.csproj")
    .WithReference(mongoDatabase)
    .WithExternalHttpEndpoints();

// Add FloorManager (BSP dungeon floor viewer)
builder.AddProject("floor-manager", @"..\Apps\FloorManager\FloorManager.csproj")
    .WithExternalHttpEndpoints();

// Add GaMcpServer (MCP server for AI integrations)
builder.AddProject("ga-mcp-server", @"..\GaMcpServer\GaMcpServer.csproj")
    .WithReference(mongoDatabase)
    .WithReference(redis);

// Add ga-client (React frontend) - as NPM project
builder.AddNpmApp("ga-client", "../Apps/ga-client")
    .WithHttpEndpoint(5173, env: "PORT")
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();
