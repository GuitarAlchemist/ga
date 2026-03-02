using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Services.Embeddings;

using GA.Business.Intelligence.SemanticIndexing;

using GaCLI.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Extensions;
using Spectre.Console;
using static System.Console;

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddYamlFile("appsettings.yaml", true, true)
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

        case "debug-tags":
            await RunDebugTags(configuration);
            return;

        case "embedding":
            await RunEmbedding(args, configuration);
            return;

        case "semantic-fretboard":
            await RunSemanticFretboard(args, configuration);
            return;

        case "index-voicings":
            await RunIndexVoicings(args, configuration);
            return;

        case "query-voicings":
            await RunQueryVoicings(args, configuration);
            return;

        case "validate-index":
            await RunValidateIndex(args, configuration);
            return;

        case "demo-voicings":
            await RunDemoVoicings(args, configuration);
            return;

        case "search-voicings":
            await RunSearchVoicings(args, configuration);
            return;

        case "identify":
            await RunIdentify(args, configuration);
            return;

        case "similar":
            await RunSimilar(args, configuration);
            return;

        case "benchmark-quality":
            await RunBenchmarkQuality(args, configuration);
            return;

        case "benchmark-similarity":
            await RunBenchmarkSimilarity(args, configuration);
            return;

        case "hybrid-benchmark":
            await RunHybridBenchmark(args, configuration);
            return;

        case "chat":
            await RunChat(args, configuration);
            return;

        case "explain":
            await RunExplainVoicing(args, configuration);
            return;

        case "ingest-corpus":
            await RunIngestCorpus(args, configuration);
            return;

        case "generate-naturalness-data":
            await RunGenerateNaturalnessData(args, configuration);
            return;

        case "train-naturalness-model":
            await RunTrainNaturalnessModel(args);
            return;

        case "run-benchmarks":
            await RunBenchmarks(args, configuration);
            return;

        case "plugin":
            await RunPlugin(args);
            return;

    }
}

WriteLine("No valid command specified. Available commands:");
WriteLine("  run-benchmarks         Run all registered automated benchmarks");
WriteLine("  sync-mongodb           Synchronize MongoDB data");
WriteLine("  analyze-chord          Analyze biomechanical playability of a chord");
WriteLine("  analyze-progression    Analyze biomechanical playability of a chord progression");
WriteLine("  asset-import           Import 3D assets (GLB files) into the asset library");
WriteLine("  asset-list             List all imported assets");
WriteLine("  asset-delete           Delete an asset by ID");
WriteLine("  gpu-voicing-search [N] GPU-accelerated semantic voicing search demo");
WriteLine("                         Optional: 1=Quick, 2=Performance, 3=Batch, 4=Interactive, 5=Stats");
WriteLine("  debug-tags             Show symbolic tag registry");
WriteLine("  embedding <text>       Run semantic embedding search");
WriteLine("  semantic-fretboard     Test semantic fretboard indexing and querying");
WriteLine("  identify <diagram>     Identify what chord a fret diagram represents");
WriteLine("  similar <diagram>      Find alternative voicings for a chord shape");
WriteLine("  benchmark-similarity   Compare two voicings using musical embeddings");
WriteLine("  search-voicings        Search the voicing database with filters");
WriteLine("  index-voicings         Index all possible voicings into MongoDB");
WriteLine("  plugin                 Plugin/agent marketplace command surface (MVP scaffold)");
WriteLine();
WriteLine("Examples:");
WriteLine("  dotnet run -- analyze-chord Cmaj7 --hand-size Medium --verbose");
WriteLine("  dotnet run -- search-voicings --name \"Cmaj7\" --no-barre");
WriteLine("  dotnet run -- identify x32010");
WriteLine("  dotnet run -- plugin --help");
WriteLine("  dotnet run -- plugin marketplace add <source> --scope project");
WriteLine("  dotnet run -- plugin install <plugin-ref> --scope local");

return;

// ... existing methods ...

static Task RunBenchmarkQuality(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}

// ... existing RunChat ...

static Task RunBenchmarks(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}

static Task RunDebugTags(IConfigurationRoot config)
{
    throw new NotImplementedException();
}

static async Task RunPlugin(string[] args)
{
    var command = new PluginCommand();
    await command.ExecuteAsync(args);
}

static async Task RunEmbedding(string[] args, IConfigurationRoot config)
{
    if (args.Length < 2)
    {
        WriteLine("Usage: embedding <text>");
        return;
    }

    var text = string.Join(" ", args.Skip(1));

    using var serviceProvider = BuildScratchServiceProvider(config);
    var command = serviceProvider.GetRequiredService<EmbeddingCommand>();

    await command.ExecuteAsync(text);
}

static async Task RunSemanticFretboard(string[] args, IConfigurationRoot config)
{
    var options = new SemanticFretboardOptions
    {
        ShouldIndex = args.Contains("--index"),
        Interactive = args.Contains("--interactive")
    };

    var tuning = GetArgValue(args, "--tuning");
    if (!string.IsNullOrWhiteSpace(tuning))
    {
        options.Tuning = tuning;
    }

    var maxFretArg = GetArgValue(args, "--max-fret");
    if (maxFretArg != null && int.TryParse(maxFretArg, out var maxFret))
    {
        options.MaxFret = maxFret;
    }

    options.Query = GetArgValue(args, "--query");

    using var serviceProvider = BuildScratchServiceProvider(config);
    var command = serviceProvider.GetRequiredService<SemanticFretboardCommand>();

    await command.ExecuteAsync(options);
}

static ServiceProvider BuildScratchServiceProvider(IConfigurationRoot config)
{
    var services = new ServiceCollection();
    services.AddLogging(c => c.AddConsole());
    services.Configure<MongoDbSettings>(options =>
    {
        options.ConnectionString = config.GetValue<string>("MongoDbSettings:ConnectionString") ?? "mongodb://localhost:27017";
        options.DatabaseName = config.GetValue<string>("MongoDbSettings:DatabaseName") ?? "guitaralchemist";
    });
    services.AddTransient<MongoDbService>();
    // services.AddTransient<IndexVoicingsCommand>();
    services.AddTransient<QueryVoicingsCommand>();
    services.AddTransient<ValidateVoicingIndexCommand>();
    services.AddTransient<DemoVoicingCapabilitiesCommand>();
    // services.AddTransient<SearchVoicingsCommand>();
    services.AddTransient<IdentifyCommand>();
    services.AddTransient<SimilarVoicingsCommand>();
    // services.AddTransient<ChatCommand>();
    // services.AddTransient<BenchmarkQualityCommand>();
    // services.AddTransient<BenchmarkSimilarityCommand>();
    // services.AddTransient<RunBenchmarksCommand>();
    // services.AddTransient<DebugTagsCommand>();
    services.AddTransient<SemanticSearchService>();
    services.AddTransient<EmbeddingCommand>();
    services.AddTransient<SemanticFretboardService>();
    services.AddTransient<SemanticFretboardCommand>();
    // Tab Corpus
    services.AddTransient<GA.Domain.Repositories.ITabCorpusRepository, MongoTabCorpusRepository>();
    services.AddTransient<GA.Business.ML.Tabs.TabCorpusService>();
    services.AddTransient<IngestCorpusCommand>();

    // AI Services
    services.AddGuitarAlchemistAI();

    // Vector Store (Qdrant Spike)
    services.AddSingleton<IVectorIndex>(sp => new QdrantVectorIndex("localhost", 6334));

    services.AddSingleton<GA.Business.ML.Embeddings.OnnxEmbeddingGenerator>(sp =>
    {
         // Propose: Load from config, but for now use same hardcoded paths or a factory
         var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "all-MiniLM-L6-v2.onnx");
         var vocabPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "vocab.txt");
         return new GA.Business.ML.Embeddings.OnnxEmbeddingGenerator(modelPath, vocabPath);
    });

    services.AddSingleton<GA.Business.ML.Musical.Explanation.VoicingExplanationService>();
    // services.AddSingleton<IVoicingSearchStrategy, CpuVoicingSearchStrategy>();
    // services.AddSingleton<VoicingIndexingService>();
    // services.AddSingleton<EnhancedVoicingSearchService>();
    // services.AddTransient<ExplainVoicingCommand>();

    // Naturalness
    services.AddTransient<GA.Business.ML.Naturalness.NaturalnessTrainingDataGenerator>();
    services.AddTransient<GenerateNaturalnessDataCommand>();

    return services.BuildServiceProvider();
}

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
    // but SemanticSearchService expects GA.Domain.Instruments.Fretboard.SemanticIndexing.SemanticSearchService.IEmbeddingService
    // services.AddSingleton<EmbeddingServiceFactory>();
    // services.AddScoped<SemanticSearchService.IEmbeddingService>(sp =>
    //     sp.GetRequiredService<EmbeddingServiceFactory>().CreateService());

    // TODO: Add LM Studio integration when GA.Business.ML is available
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

static Task RunAnalyzeChord(string[] args)
{
    throw new NotImplementedException();
}

static Task RunAnalyzeProgression(string[] args)
{
    throw new NotImplementedException();
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

static Task RunGpuVoicingSearch(string[] args)
{
    throw new NotImplementedException();
}

async static Task RunHybridBenchmark(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}

static Task RunIndexVoicings(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}

static async Task RunQueryVoicings(string[] args, IConfigurationRoot config)
{
    try
    {
        var chordName = args.Length > 1 ? args[1] : "C Major";
        if (chordName.StartsWith("--")) chordName = "C Major"; // Handle flags if passed as 2nd arg by mistake

        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<QueryVoicingsCommand>();

        await command.ExecuteAsync(chordName);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
}

static async Task RunValidateIndex(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<ValidateVoicingIndexCommand>();

        await command.ExecuteAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
}

static async Task RunDemoVoicings(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<DemoVoicingCapabilitiesCommand>();

        await command.ExecuteAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
}

static Task RunSearchVoicings(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}

static async Task RunIdentify(string[] args, IConfigurationRoot config)
{
    try
    {
        if (args.Length < 2)
        {
            WriteLine("Usage: identify <diagram> [--verbose|-v]");
            WriteLine("Examples:");
            WriteLine("  identify x32010");
            WriteLine("  identify x-3-2-0-1-0 --verbose");
            return;
        }

        var diagram = args[1];
        var verbose = args.Contains("--verbose") || args.Contains("-v");

        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<IdentifyCommand>();

        await command.ExecuteAsync(diagram, verbose);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
    }
}

static async Task RunSimilar(string[] args, IConfigurationRoot config)
{
    try
    {
        if (args.Length < 2)
        {
            WriteLine("Usage: similar <diagram> [--same-bass] [--limit <N>]");
            return;
        }

        var diagram = args[1];
        var sameBass = args.Contains("--same-bass");
        var limit = 5;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--limit" && i + 1 < args.Length && int.TryParse(args[i+1], out var l))
                limit = l;
        }

        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<SimilarVoicingsCommand>();

        await command.ExecuteAsync(diagram, sameBass, limit);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
    }
}

static Task RunExplainVoicing(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}

static Task RunChat(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}

static Task RunBenchmarkSimilarity(string[] args, IConfigurationRoot config)
{
    throw new NotImplementedException();
}






static async Task RunIngestCorpus(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<IngestCorpusCommand>();
        await command.ExecuteAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
    }

    }

static async Task RunGenerateNaturalnessData(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<GenerateNaturalnessDataCommand>();

        var output = GetArgValue(args, "--output") ?? "naturalness_data.csv";
        var limitArg = GetArgValue(args, "--limit");
        var limit = limitArg != null ? int.Parse(limitArg) : 1000;

        await command.ExecuteAsync(output, limit);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
    }
}

static async Task RunTrainNaturalnessModel(string[] args)
{
    try
    {
        var dataPath = GetArgValue(args, "--data") ?? "naturalness_data.csv";
        var outputPath = GetArgValue(args, "--output") ?? "models/naturalness_ranker.onnx";

        var command = new TrainNaturalnessModelCommand();
        await command.ExecuteAsync(dataPath, outputPath);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
}
