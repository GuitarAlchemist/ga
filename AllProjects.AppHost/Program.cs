var builder = DistributedApplication.CreateBuilder(args);

// Add Redis for distributed caching
var redis = builder.AddRedis("redis")
    .WithRedisCommander()
    .WithDataVolume("ga-redis-data");

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
    .WithEnvironment("REDIS_URL", redis)
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

// Add GaApi (main API server)
builder.AddProject("gaapi", @"..\Apps\ga-server\GaApi\GaApi.csproj")
    .WithReference(mongoDatabase)
    .WithReference(redis)
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
