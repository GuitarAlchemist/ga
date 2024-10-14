using GA.Business.Core.AI;
using GA.Business.Core.Instruments;
using GA.Business.Core.Tonal;
using GA.Core.Extensions;
using GaCLI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using OpenAI.ChatGpt;
using OpenAI.ChatGpt.Models.ChatCompletion.Messaging;
using OpenAI_API;
using static System.Console;

var configurationBuilder = new ConfigurationBuilder();
var configuration = configurationBuilder.AddUserSecrets<Program>().Build();

// WriteLine(Assets.Print());

// FindInstruments();

// await OpenAiSimpleCompletionAsync();
// await ChatGptSimpleCompletionAsync();
await SemanticKernelPromptLoop(ChatCompletionConfig.FromSection(configuration.GetSection("OpenAi")));

// -----------------------------------------------------------

#region Ai

async Task OpenAiSimpleCompletionAsync(ChatCompletionConfig config)
{
    config.Deconstruct(out var modelId, out var apiKey);
    
    var openai = new OpenAIAPI(apiKey);
    var request = "What is the relative minor of C major?";
    WriteLine($"> {request}");
    var response = await openai.Completions.CreateCompletionAsync(request);
    WriteLine($"[Response] {response}");
}

async Task ChatGptSimpleCompletionAsync(ChatCompletionConfig config)
{
    config.Deconstruct(out var modelId, out var apiKey);
    
    using var openAiClient = new OpenAiClient(apiKey);
    var request = "What is the relative minor of C major?";
    var response = await openAiClient.GetChatCompletions(new UserMessage(request), maxTokens: 80);
    WriteLine(response);
}

static async Task SemanticKernelPromptLoop(ChatCompletionConfig config)
{
    config.Deconstruct(out var modelId, out var apiKey);

    var builder = Kernel.CreateBuilder()
                        .AddOpenAIChatCompletion(modelId, apiKey);
    builder.Services.AddLogging(c => c.SetMinimumLevel(LogLevel.Trace));
    builder.Plugins.AddFromType<GaKeyPlugin>();
    var kernel = builder.Build();

    // Test the plugin
    var keySignatures = await kernel.InvokeAsync(nameof(GaKeyPlugin),nameof(GaKeyPlugin.GetKeySignatures));
    WriteLine($"Key signatures => {keySignatures}");    
    
    var keys = await kernel.InvokeAsync(nameof(GaKeyPlugin), nameof(GaKeyPlugin.GetKeys));
    WriteLine($"Keys => {keys}");    
    
    var keyNotes = await kernel.InvokeAsync(nameof(GaKeyPlugin), nameof(GaKeyPlugin.GetAccidentedNotesInKey), new KernelArguments { ["key"] = Key.Major.G });
    WriteLine($"Key of G => {keyNotes}");

    // Create chat history
    ChatHistory history = [];

    //var request = "What is the relative minor of G major?";
    var request = "What are the accidentals in the key of G";
    WriteLine($"> {request}");
    var response = await kernel.InvokePromptAsync(request);
    WriteLine($"[Response] {response}");
    
    // Start the conversation
    Write("User > ");
    while (ReadLine() is { } userInput)
    {
        history.AddUserMessage(userInput);

        // Get the response from the AI
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var result = chatCompletionService.GetStreamingChatMessageContentsAsync(
            history,
            executionSettings: new OpenAIPromptExecutionSettings
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions // Enable auto function calling
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

#endregion

void Experiment1()
{
    var fretboard = Fretboard.Default;
    var fretRange = Fret.Range(Fret.Open, 12);
    var rp = fretboard.RelativePositions;
    var count = 0;
    foreach (var startFret in fretRange)
    {
        foreach (var relativeFretVector in rp)
        {
            if (!relativeFretVector.IsPrime) continue;
            var fretVector = relativeFretVector.ToFretVector(startFret);
            var positionLocations = fretVector.PositionLocations;
            var fretVectorPositions = fretboard.Positions.Played.FromLocations(positionLocations);

            var aa = fretVectorPositions.Select(played => played.MidiNote.ToPitch()).ToImmutableList();

            count++;
        }
    }
}

void Experiment2()
{
    var combinations = PitchClass.Items.ToCombinations();
    var majorTriad = combinations[145];
    var majorTriadIntervalVector = majorTriad.ToIntervalClassVector();

    var majorScale = combinations[2741];
    var majorScaleIntervalPattern = majorScale.ToIntervalStructure();
    var majorScaleIntervalVector = majorScale.ToIntervalClassVector();

    var icvLookup = combinations.ToLookup(pitchClasses => pitchClasses.ToIntervalClassVector());
    var majorScaleMembers = icvLookup[majorScaleIntervalVector].ToImmutableList();
    var majorTriadMembers = icvLookup[majorTriadIntervalVector].ToImmutableList();

    var isLookup = combinations.ToLookup(pitchClasses => pitchClasses.ToIntervalStructure());
    var isMembers = isLookup[majorScaleIntervalPattern].ToImmutableList();

    _ = 1;
}

void FindInstruments(string name = "Guitar")
{
    var guitar = InstrumentFinder.Instance[name];
}


/*
foreach (var vector in Fretboard.Default.RelativePositions)
{
    Console.WriteLine(vector);
}

*/