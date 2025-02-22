#pragma warning disable SKEXP0070

using GA.Business.Core.AI;
using GA.Data.MongoDB.Configuration;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Services.DocumentServices;
using GA.Data.MongoDB.Extensions;
using GaCLI;
using GaCLI.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using static System.Console;

var configurationBuilder = new ConfigurationBuilder();
var configuration = configurationBuilder.AddUserSecrets<Program>().Build();

// Add MongoDB sync option
if (args.Length > 0 && args[0] == "sync-mongodb")
{
    await RunMongoDbSync();
    return;
}

var ollamaBaseUri = new Uri("http://localhost:11434");

//await CheckOllamaModels();
//await SemanticKernelPromptLoop();
return;

void DisplayError(params string[] messages)
{
    // Utility method to abstract repetitive error message logic
    foreach (var message in messages)
    {
        WriteLine($"Error: {message}");
    }
}

async Task<IEnumerable<string>> GetAvailableModels()
{
    // Method handles fetching models for better readability and potential reuse.
    return await OllamaApiClient.GetAvailableModelsAsync(ollamaBaseUri);
}

async Task CheckOllamaModels()
{
    try
    {
        var availableModels = await GetAvailableModels();
        var noModelsAvailable = !availableModels.Any();
        if (noModelsAvailable)
        {
            DisplayError(
                "No Ollama models found. Please ensure you have downloaded at least one model.",
                "You can download a model using: 'ollama pull llama2'"
            );
            return;
        }

        {
            DisplayError(
                "No Ollama models found. Please ensure you have downloaded at least one model.",
                "You can download a model using: 'ollama pull llama2'"
            );
            return;
        }

        WriteLine($"Available Ollama models: {string.Join(", ", availableModels)}");
    }
    catch (HttpRequestException ex)
    {
        DisplayError(
            "Cannot connect to Ollama. Please ensure Ollama is running on http://localhost:11434",
            $"Error details: {ex.Message}",
            "To start Ollama, open a terminal and run: 'ollama serve'"
        );
    }
}

static async Task RunMongoDbSync()
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
    
    var kernel = builder.Build();

    try 
    {
        var runner = kernel.GetRequiredService<Runner>();
        await runner.ExecuteAsync();
    }
    catch (Exception ex)
    {
        WriteLine($"Error during sync: {ex.Message}");
        WriteLine($"Stack trace: {ex.StackTrace}");
    }
}

// -----------------------------------------------------------
static async Task SemanticKernelPromptLoop()
{
    var ollamaBaseUri = new Uri("http://localhost:11434");
    var ollamaAvailableModels = await OllamaApiClient.GetAvailableModelsAsync(ollamaBaseUri);

    // Select the first available model instead of hardcoding "llama3:latest"
    var modelName = ollamaAvailableModels.FirstOrDefault() ??
                    throw new InvalidOperationException("No Ollama models available");

    var builder = Kernel.CreateBuilder();
    builder.Services.AddOllamaChatCompletion(modelName, ollamaBaseUri);
    builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
    builder.Plugins.AddFromType<GaKeyPlugin>();
    var kernel = builder.Build();

    // Provide context about the available plugin
    var pluginContext = @"
    You have access to a GaKeyPlugin with the following functions:
    - get_keys: Get all musical keys

    Use these functions when appropriate to answer music theory questions.
    ";

    // Create chat history
    ChatHistory history = [];
    history.AddSystemMessage(pluginContext);

    // Get all keys
    {
        var request =
            "Please get all musical keys using get_keys function from GaKeyPlugin, ensure a plugin function is executed";
        WriteLine($"> {request}");
        var response = await kernel.InvokePromptAsync(request);
        WriteLine($"[Response] {response}");
    }

    //// Get notes in the key of G
    //{
    //    var request = "Please get notes in the key of G";
    //    WriteLine($"> {request}");
    //    var response = await kernel.InvokePromptAsync(request);
    //    WriteLine($"[Response] {response}");
    //}

    // Start the conversation
    Write("User > ");
    while (ReadLine() is { } userInput)
    {
        history.AddUserMessage(userInput);

        // Get the response from the AI
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var result = chatCompletionService.GetStreamingChatMessageContentsAsync(
            history,
            executionSettings: new PromptExecutionSettings
            {
            },
            kernel: kernel);

        // Stream the results
        var fullMessage = "";
        var first = true;
        await foreach (var content in result)
        {
            if (content.Role.HasValue && first)
            {
                Write("Assistant > ");
                first = false;
            }

            Write(content.Content);
            fullMessage += content.Content;
        }

        WriteLine();

        // Add the message from the agent to the chat history
        history.AddAssistantMessage(fullMessage);

        // Get user input again
        Write("User > ");
    }
}