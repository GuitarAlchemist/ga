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
using GA.Business.Core.ChatBot;

var configurationBuilder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddYamlFile("appsettings.yaml", optional: false, reloadOnChange: true)
    .AddYamlFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.yaml", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .AddEnvironmentVariables();

var configuration = configurationBuilder.Build();

// Add MongoDB sync option
if (args.Length > 0 && args[0] == "sync-mongodb")
{
    await RunMongoDbSync(configuration);
    return;
}

WriteLine("No valid command specified. Available commands:");
WriteLine("  sync-mongodb    Synchronize MongoDB data");
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

    // Add GuitarAlchemist ChatBot
    services.AddGuitarAlchemistChatBot(options => {
        config.GetSection("ChatBot").Bind(options);
    });

    var kernel = builder.Build();

    try 
    {
        var mongoDbService = kernel.GetRequiredService<MongoDbService>();
        
        // Create indexes before syncing data
        await mongoDbService.CreateIndexesAsync();
        await mongoDbService.CreateRagIndexesAsync();
        
        var runner = kernel.GetRequiredService<Runner>();
        await runner.ExecuteAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Error during sync: {ex.Message}");
        WriteLine($"Stack trace: {ex.StackTrace}");
    }
}
