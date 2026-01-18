using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Services.Embeddings;

using GA.Business.Core.Fretboard.Voicings.Search;
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
WriteLine("  identify <diagram>     Identify what chord a fret diagram represents");
WriteLine("  similar <diagram>      Find alternative voicings for a chord shape");
WriteLine("  benchmark-similarity   Compare two voicings using musical embeddings");
WriteLine("  search-voicings        Search the voicing database with filters");
WriteLine("  index-voicings         Index all possible voicings into MongoDB");
WriteLine();
WriteLine("Examples:");
WriteLine("  dotnet run -- analyze-chord Cmaj7 --hand-size Medium --verbose");
WriteLine("  dotnet run -- search-voicings --name \"Cmaj7\" --no-barre");
WriteLine("  dotnet run -- identify x32010");

return;

// ... existing methods ...

static async Task RunBenchmarkQuality(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<BenchmarkQualityCommand>();

        await command.ExecuteAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
}

// ... existing RunChat ...

static async Task RunBenchmarks(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<RunBenchmarksCommand>();

        string? name = args.Length > 1 ? args[1] : null;
        if (name?.StartsWith("--") == true) name = null;

        await command.ExecuteAsync(name);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
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
    services.AddTransient<IndexVoicingsCommand>();
    services.AddTransient<QueryVoicingsCommand>();
    services.AddTransient<ValidateVoicingIndexCommand>();
    services.AddTransient<DemoVoicingCapabilitiesCommand>();
    services.AddTransient<SearchVoicingsCommand>();
    services.AddTransient<IdentifyCommand>();
    services.AddTransient<SimilarVoicingsCommand>();
    services.AddTransient<ChatCommand>();
    services.AddTransient<BenchmarkQualityCommand>();
    services.AddTransient<BenchmarkSimilarityCommand>();
    services.AddTransient<RunBenchmarksCommand>();

    // Tab Corpus
    services.AddTransient<GA.Business.Core.Tabs.ITabCorpusRepository, MongoTabCorpusRepository>();
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
    services.AddSingleton<IVoicingSearchStrategy, CpuVoicingSearchStrategy>();
    services.AddSingleton<VoicingIndexingService>();
    services.AddSingleton<EnhancedVoicingSearchService>();
    services.AddTransient<ExplainVoicingCommand>();

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
    // but SemanticSearchService expects GA.Business.Core.Fretboard.SemanticIndexing.SemanticSearchService.IEmbeddingService
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

    var demoChoice = args.Length > 1 ? args[1] : null;
    await command.ExecuteAsync(demoChoice);
}

async static Task RunHybridBenchmark(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var searchService = serviceProvider.GetRequiredService<EnhancedVoicingSearchService>();
        var mongoDbService = serviceProvider.GetRequiredService<MongoDbService>();
        var indexingService = serviceProvider.GetRequiredService<VoicingIndexingService>();
        var textEmbeddingGenerator = serviceProvider.GetRequiredService<GA.Business.ML.Embeddings.OnnxEmbeddingGenerator>();

        var verbose = args.Any(a => a == "--verbose" || a == "-v");
        var movable = args.Contains("--movable");

        var key = GetArgValue(args, "--key");

        int? maxFret = null;
        var maxFretArg = GetArgValue(args, "--max-fret");
        if (maxFretArg != null && int.TryParse(maxFretArg, out var mf))
        {
            maxFret = mf;
        }

        var limit = 1000;
        if (args.Contains("--all"))
        {
            limit = int.MaxValue;
        }
        else
        {
            var limitArg = GetArgValue(args, "--limit");
            if (limitArg != null && int.TryParse(limitArg, out var l))
            {
                limit = l;
            }
        }

        await new GaCLI.Commands.HybridBenchmarkCommand(searchService, mongoDbService, indexingService, textEmbeddingGenerator)
            .ExecuteAsync(verbose, key, maxFret, movable, limit);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
        AnsiConsole.WriteException(ex);
    }
}

static async Task RunIndexVoicings(string[] args, IConfigurationRoot config)
{
    try
    {
        // Check arguments
        var indexAll = args.Contains("--all");
        var limitArg = GetArgValue(args, "--limit");
        var limit = limitArg != null ? int.Parse(limitArg) : 1000;

        var windowArg = GetArgValue(args, "--window");
        var window = windowArg != null ? int.Parse(windowArg) : 4;

        var minNotesArg = GetArgValue(args, "--min-notes");
        var minNotes = minNotesArg != null ? int.Parse(minNotesArg) : 2;

        var force = args.Contains("--force");
        var drop = args.Contains("--drop");
        var seed = args.Contains("--seed");

        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<IndexVoicingsCommand>();

        await command.ExecuteAsync(indexAll, limit, window, minNotes, force, drop, seed);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
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

static async Task RunSearchVoicings(string[] args, IConfigurationRoot config)
{
    var options = new SearchVoicingsCommand.ValidatedOptions();

    // Manual simplistic arg-parsing for the demo
    for (int i = 0; i < args.Length; i++)
    {
        var arg = args[i];
        if (arg == "--name" && i + 1 < args.Length) options.ChordName = args[++i];
        if (arg == "--tag" && i + 1 < args.Length) options.Tag = args[++i];
        if (arg == "--forte" && i + 1 < args.Length) options.ForteCode = args[++i];
        if (arg == "--diff" && i + 1 < args.Length) options.Difficulty = args[++i];
        if (arg == "--min-fret" && i + 1 < args.Length) options.MinFret = int.Parse(args[++i]);
        if (arg == "--max-fret" && i + 1 < args.Length) options.MaxFret = int.Parse(args[++i]);
        if (arg == "--min-stretch" && i + 1 < args.Length) options.MinStretch = int.Parse(args[++i]);
        if (arg == "--max-stretch" && i + 1 < args.Length) options.MaxStretch = int.Parse(args[++i]);
        if (arg == "--no-barre") options.NoBarre = true;
        if (arg == "--limit" && i + 1 < args.Length) options.Limit = int.Parse(args[++i]);

        // NEW options for enhanced search
        if (arg == "--function" && i + 1 < args.Length) options.HarmonicFunction = args[++i];
        if (arg == "--guide-tones") options.HasGuideTones = true;
        if (arg == "--rootless") options.IsRootless = true;
        if (arg == "--register" && i + 1 < args.Length) options.Register = args[++i];
        if (arg == "--string-set" && i + 1 < args.Length) options.StringSet = args[++i];
        if (arg == "--detailed" || arg == "-d") options.Detailed = true;
        if (arg == "--similar-to" && i + 1 < args.Length) options.SimilarTo = args[++i];
    }

    using var serviceProvider = BuildScratchServiceProvider(config);
    var command = serviceProvider.GetRequiredService<SearchVoicingsCommand>();

    await command.ExecuteAsync(options);
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

static async Task RunExplainVoicing(string[] args, IConfigurationRoot config)
{
    try
    {
        if (args.Length < 2)
        {
            WriteLine("Usage: explain <diagram> [--verbose]");
            return;
        }

        var diagram = args[1];
        var verbose = args.Contains("--verbose");

        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<ExplainVoicingCommand>();

        await command.ExecuteAsync(diagram, verbose);
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
    }
}

static async Task RunChat(string[] args, IConfigurationRoot config)
{
    using var serviceProvider = BuildScratchServiceProvider(config);
    var command = serviceProvider.GetRequiredService<ChatCommand>();
    await command.ExecuteAsync();
}

static async Task RunBenchmarkSimilarity(string[] args, IConfigurationRoot config)
{
    try
    {
        using var serviceProvider = BuildScratchServiceProvider(config);
        var command = serviceProvider.GetRequiredService<BenchmarkSimilarityCommand>();

        await command.ExecuteAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Error: {ex.Message}");
        WriteLine(ex.StackTrace);
    }
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
