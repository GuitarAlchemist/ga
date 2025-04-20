using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Extensions;
using GA.Data.MongoDB.Services.Embeddings;
using GaCLI.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using static System.Console;

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddYamlFile("appsettings.yaml", optional: false, reloadOnChange: true)
    .AddYamlFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.yaml", optional: true, reloadOnChange: true)
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
    }
}

WriteLine("No valid command specified. Available commands:");
WriteLine("  sync-mongodb        Synchronize MongoDB data");
WriteLine("  start-lmstudio-api  Start the LM Studio API server");
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
    services.AddSyncServices();
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

    services.AddSingleton<EmbeddingServiceFactory>();
    services.AddScoped<IEmbeddingService>(sp =>
        sp.GetRequiredService<EmbeddingServiceFactory>().CreateService());

    // Add LM Studio integration
    services.Configure<GA.Business.Core.AI.LmStudio.LmStudioSettings>(options =>
    {
        var lmStudioSection = config.GetSection("LmStudio");
        if (lmStudioSection.Exists())
        {
            options.ApiUrl = lmStudioSection["ApiUrl"] ?? options.ApiUrl;
            options.Model = lmStudioSection["Model"] ?? options.Model;
            options.SystemPrompt = lmStudioSection["SystemPrompt"] ?? options.SystemPrompt;

            if (int.TryParse(lmStudioSection["MaxTokens"], out var maxTokens))
                options.MaxTokens = maxTokens;

            if (float.TryParse(lmStudioSection["Temperature"], out var temperature))
                options.Temperature = temperature;

            if (float.TryParse(lmStudioSection["TopP"], out var topP))
                options.TopP = topP;
        }
    });

    services.AddScoped<GA.Business.Core.AI.LmStudio.LmStudioIntegrationService>();

    var kernel = builder.Build();

    try
    {
        var mongoDbService = kernel.GetRequiredService<MongoDbService>();

        // Create indexes before syncing data
        await mongoDbService.CreateIndexesAsync();
        await mongoDbService.CreateRagIndexesAsync();

        WriteLine("Starting MongoDB sync with embeddings...");
        var runner = kernel.GetRequiredService<Runner>();
        await runner.ExecuteAsync();

        WriteLine("\nMongoDB sync completed successfully!");
        WriteLine("You can now use LM Studio with the following model: NyxGleam/mistral-7b-instruct-v0.1.Q4_K_M");
        WriteLine("Make sure to configure LM Studio to use the API endpoint: http://localhost:5000/api/LmStudio/context");
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
