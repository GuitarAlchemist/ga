using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Services;
using GA.Business.Core.AI.Services.Embeddings;
using GaCLI.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using static System.Console;

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddYamlFile("appsettings.yaml", false, true)
    .AddYamlFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.yaml",
        true, true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

var configuration = configurationBuilder.Build();

// Process command line arguments
if (args.Length > 0)
{
    switch (args[0])
    {
        case "sync-mongodb":
            await RunMongoDbSync(configuration);
            return;

        case "start-lmstudio-api":
            await StartLmStudioApi(args);
            return;

        case "analyze-chord":
            await RunAnalyzeChord(args);
            return;

        case "analyze-progression":
            await RunAnalyzeProgression(args);
            return;

        case "asset-import":
            await RunAssetImport(args, configuration);
            return;

        case "asset-list":
            await RunAssetList(args, configuration);
            return;

        case "asset-delete":
            await RunAssetDelete(args, configuration);
            return;

        case "gpu-voicing-search":
            await RunGpuVoicingSearch(args);
            return;
    }
}

WriteLine("No valid command specified. Available commands:");
WriteLine("  sync-mongodb           Synchronize MongoDB data");
WriteLine("  start-lmstudio-api     Start the LM Studio API server");
WriteLine("  analyze-chord          Analyze biomechanical playability of a chord");
WriteLine("  analyze-progression    Analyze biomechanical playability of a chord progression");
WriteLine("  asset-import           Import 3D assets (GLB files) into the asset library");
WriteLine("  asset-list             List all imported assets");
WriteLine("  asset-delete           Delete an asset by ID");
WriteLine("  gpu-voicing-search [N] GPU-accelerated semantic voicing search demo");
WriteLine("                         Optional: 1=Quick, 2=Performance, 3=Batch, 4=Interactive, 5=Stats");
WriteLine();
WriteLine("Examples:");
WriteLine("  dotnet run -- analyze-chord Cmaj7 --hand-size Medium --verbose");
WriteLine("  dotnet run -- analyze-progression C,Am,F,G --hand-size Large");
WriteLine("  dotnet run -- asset-import path/to/model.glb --category Gems --license \"CC BY 4.0\"");
WriteLine("  dotnet run -- gpu-voicing-search 2    # Run performance benchmark directly");
WriteLine("  dotnet run -- asset-import path/to/assets --directory --category Architecture");
WriteLine("  dotnet run -- asset-list --category Gems --verbose");
WriteLine("  dotnet run -- asset-delete <asset-id>");
return;

static async Task RunMongoDbSync(IConfigurationRoot config)
{
    var builder = Kernel.CreateBuilder();
    var services = builder.Services;

    // Add MongoDB configuration
    services.Configure<MongoDbSettings>(options =>
    {
        options.ConnectionString = "mongodb://localhost:27017";
        options.DatabaseName = "guitaralchemist";
    });

    // Add required services
    services.AddLogging(c => c.AddConsole());
    services.AddHttpClient();
    services.AddTransient<MongoDbService>();
    // TODO: AddSyncServices extension method doesn't exist - need to register sync services manually
    // services.AddSyncServices();
    services.AddTransient<Runner>();

    // Configure embedding services
    services.Configure<EmbeddingServiceSettings>(options =>
    {
        var embeddingSection = config.GetSection("EmbeddingService");
        options.ServiceType = Enum.Parse<EmbeddingServiceType>(
            embeddingSection["ServiceType"] ?? "OpenAi");
        options.ApiKey = embeddingSection["ApiKey"]; // Optional for Ollama
        options.ModelName = embeddingSection["ModelName"];
        options.OllamaHost = embeddingSection["OllamaHost"];
    });

    // TODO: Fix type mismatch - EmbeddingServiceFactory returns GA.Data.MongoDB.Services.Embeddings.IEmbeddingService
    // but SemanticSearchService expects GA.Business.Core.Fretboard.SemanticIndexing.SemanticSearchService.IEmbeddingService
    // services.AddSingleton<EmbeddingServiceFactory>();
    // services.AddScoped<SemanticSearchService.IEmbeddingService>(sp =>
    //     sp.GetRequiredService<EmbeddingServiceFactory>().CreateService());

    // TODO: Add LM Studio integration when GA.Business.Core.AI is available
    // services.Configure<LmStudioSettings>(options => { ... });
    // services.AddScoped<LmStudioIntegrationService>();

    var kernel = builder.Build();

    try
    {
        var mongoDbService = kernel.GetRequiredService<MongoDbService>();

        // TODO: Create indexes before syncing data - these methods don't exist on MongoDbService
        // await mongoDbService.CreateIndexesAsync();
        // await mongoDbService.CreateRagIndexesAsync();

        WriteLine("Starting MongoDB sync with embeddings...");
        var runner = kernel.GetRequiredService<Runner>();
        await runner.ExecuteAsync();

        WriteLine("\nMongoDB sync completed successfully!");
        WriteLine("You can now use LM Studio with the following model: NyxGleam/mistral-7b-instruct-v0.1.Q4_K_M");
        WriteLine(
            "Make sure to configure LM Studio to use the API endpoint: http://localhost:5000/api/LmStudio/context");
    }
    catch (Exception ex)
    {
        WriteLine($"Error during sync: {ex.Message}");
        WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

static async Task StartLmStudioApi(string[] args)
{
    try
    {
        WriteLine("Starting LM Studio API server...");
        WriteLine("Press Ctrl+C to stop the server.");

        //await LmStudioWebApi.StartAsync(args);
        await Task.CompletedTask;
    }
    catch (Exception ex)
    {
        WriteLine($"Error starting LM Studio API server: {ex.Message}");
        WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

static async Task RunAnalyzeChord(string[] args)
{
    try
    {
        if (args.Length < 2)
        {
            WriteLine("Usage: analyze-chord <chord-name> [--hand-size <size>] [--verbose]");
            WriteLine("Example: analyze-chord Cmaj7 --hand-size Medium --verbose");
            return;
        }

        var chordName = args[1];
        var handSize = GetArgValue(args, "--hand-size") ?? "Medium";
        var verbose = args.Contains("--verbose");

        var command = new BiomechanicalAnalysisCommand();
        await command.AnalyzeChordAsync(chordName, handSize, verbose);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
    }
}

static async Task RunAnalyzeProgression(string[] args)
{
    try
    {
        if (args.Length < 2)
        {
            WriteLine("Usage: analyze-progression <chords> [--hand-size <size>] [--verbose]");
            WriteLine("Example: analyze-progression C,Am,F,G --hand-size Large --verbose");
            return;
        }

        var chords = args[1];
        var handSize = GetArgValue(args, "--hand-size") ?? "Medium";
        var verbose = args.Contains("--verbose");

        var command = new BiomechanicalAnalysisCommand();
        await command.AnalyzeProgressionAsync(chords, handSize, verbose);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
    }
}

static async Task RunAssetImport(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildAssetServiceProvider(config);
        var command = serviceProvider.GetRequiredService<AssetImportCommand>();

        if (args.Length < 2)
        {
            WriteLine("Usage: asset-import <path> [options]");
            WriteLine("Options:");
            WriteLine("  --directory          Import all GLB files from directory");
            WriteLine("  --recursive          Include subdirectories (with --directory)");
            WriteLine("  --name <name>        Asset name (default: filename)");
            WriteLine(
                "  --category <cat>     Category (Architecture, AlchemyProps, Gems, Jars, Torches, Artifacts, Decorative)");
            WriteLine("  --license <license>  License information");
            WriteLine("  --source <source>    Source URL or description");
            WriteLine("  --author <author>    Original author/creator");
            WriteLine("  --verbose            Show detailed output");
            WriteLine();
            WriteLine("Examples:");
            WriteLine("  asset-import model.glb --category Gems --license \"CC BY 4.0\"");
            WriteLine("  asset-import assets/ --directory --category Architecture --recursive");
            return;
        }

        var path = args[1];
        var isDirectory = args.Contains("--directory");
        var recursive = args.Contains("--recursive");
        var verbose = args.Contains("--verbose");
        var name = GetArgValue(args, "--name");
        var categoryStr = GetArgValue(args, "--category");
        var license = GetArgValue(args, "--license");
        var source = GetArgValue(args, "--source");
        var author = GetArgValue(args, "--author");

        GA.Business.Assets.Assets.AssetCategory? category = null;
        if (categoryStr != null &&
            Enum.TryParse<GA.Business.Assets.Assets.AssetCategory>(categoryStr, true, out var cat))
        {
            category = cat;
        }

        int exitCode;
        if (isDirectory)
        {
            exitCode = await command.ImportDirectoryAsync(path, category, license, source, recursive, verbose);
        }
        else
        {
            exitCode = await command.ImportGlbAsync(path, name, category, license, source, author, null, verbose);
        }

        Environment.Exit(exitCode);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}

static async Task RunAssetList(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildAssetServiceProvider(config);
        var command = serviceProvider.GetRequiredService<AssetImportCommand>();

        var verbose = args.Contains("--verbose");
        var categoryStr = GetArgValue(args, "--category");

        GA.Business.Assets.Assets.AssetCategory? category = null;
        if (categoryStr != null &&
            Enum.TryParse<GA.Business.Assets.Assets.AssetCategory>(categoryStr, true, out var cat))
        {
            category = cat;
        }

        var exitCode = await command.ListAssetsAsync(category, verbose);
        Environment.Exit(exitCode);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}

static async Task RunAssetDelete(string[] args, IConfigurationRoot config)
{
    try
    {
        if (args.Length < 2)
        {
            WriteLine("Usage: asset-delete <asset-id> [--verbose]");
            return;
        }

        using var serviceProvider = BuildAssetServiceProvider(config);
        var command = serviceProvider.GetRequiredService<AssetImportCommand>();

        var assetId = args[1];
        var verbose = args.Contains("--verbose");

        var exitCode = await command.DeleteAssetAsync(assetId, verbose);
        Environment.Exit(exitCode);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        Environment.Exit(1);
    }
}

static ServiceProvider BuildAssetServiceProvider(IConfigurationRoot config)
{
    var services = new ServiceCollection();

    // Add MongoDB configuration
    services.Configure<MongoDbSettings>(options =>
    {
        options.ConnectionString = "mongodb://localhost:27017";
        options.DatabaseName = "guitaralchemist";
    });

    // Add required services
    services.AddLogging(c => c.AddConsole());
    services.AddHttpClient();
    services.AddTransient<MongoDbService>();
    // TODO: Add IAssetLibraryService implementation when available
    // services.AddTransient<IAssetLibraryService, AssetLibraryService>();
    services.AddTransient<AssetImportCommand>();

    return services.BuildServiceProvider();
}

static string? GetArgValue(string[] args, string argName)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i].Equals(argName, StringComparison.OrdinalIgnoreCase))
        {
            return args[i + 1];
        }
    }

    return null;
}

static async Task RunGpuVoicingSearch(string[] args)
{
    var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
    var logger = loggerFactory.CreateLogger<GpuVoicingSearchCommand>();
    var command = new GpuVoicingSearchCommand(logger);

    // Pass demo choice if provided (args[1] since args[0] is the command name)
    var demoChoice = args.Length > 1 ? args[1] : null;
    await command.ExecuteAsync(demoChoice);
}
