using GA.Business.Core.AI;
using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Extensions;
using GA.Data.MongoDB.Services.Embeddings;
using GaCLI;
using GaCLI.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using static System.Console;

var configurationBuilder = new ConfigurationBuilder();
var configuration = configurationBuilder
    .AddUserSecrets<Program>()
    .Build();

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
    services.AddTransient<MongoDbService>();
    services.AddSyncServices();
    services.AddTransient<Runner>();
    
    // Add embedding service
    services.AddTransient<IEmbeddingService, OpenAiEmbeddingService>();
    
    // Add OpenAI configuration
    services.Configure<OpenAiSettings>(options =>
    {
        options.ApiKey = config["OpenAI:ApiKey"] 
                         ?? throw new InvalidOperationException("OpenAI API key not found in configuration");
        options.ModelName = "text-embedding-ada-002";
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