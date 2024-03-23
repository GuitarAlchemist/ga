using GaCLI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using OpenAI.ChatGpt;
using OpenAI.ChatGpt.Models.ChatCompletion.Messaging;
using OpenAI_API;
using static System.Console;

var configurationBuilder = new ConfigurationBuilder();
var configuration = configurationBuilder.AddUserSecrets<Program>().Build();

// await OpenAiSimpleCompletionAsync();
// await ChatGptSimpleCompletionAsync();
await SemanticKernelFirstPrompt(ChatCompletionConfig.FromSection(configuration.GetSection("OpenAi")));

// -----------------------------------------------------------

#region Ai

async Task OpenAiSimpleCompletionAsync(ChatCompletionConfig config)
{
    config.Deconstruct(out var modelId, out var endpoint, out var apiKey);
    
    var openai = new OpenAIAPI(apiKey);
    var request = "What is the relative minor of C major?";
    WriteLine($"> {request}");
    var response = await openai.Completions.CreateCompletionAsync(request);
    WriteLine($"[Response] {response}");
}

async Task ChatGptSimpleCompletionAsync(ChatCompletionConfig config)
{
    config.Deconstruct(out var modelId, out var endpoint, out var apiKey);
    
    using var openAiClient = new OpenAiClient(apiKey);
    var request = "What is the relative minor of C major?";
    var response = await openAiClient.GetChatCompletions(new UserMessage(request), maxTokens: 80);
    WriteLine(response);
}

async Task SemanticKernelFirstPrompt(ChatCompletionConfig config)
{
    config.Deconstruct(out var modelId, out var endpoint, out var apiKey);
    
    var kernel = Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(modelId, apiKey)
        .Build();

    var request = "What is the relative minor of G major?";
    WriteLine($"> {request}");
    var response = await kernel.InvokePromptAsync(request);
    WriteLine($"[Response] {response}");
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
    var combinations = new Combinations<PitchClass>();
    var majorTriad = combinations[145];
    var majorTriadIntervalVector = majorTriad.ToIntervalClassVector();

    var majorScale = combinations[2741];
    var majorScaleIntervalPattern = majorScale.ToIntervalPattern();
    var majorScaleIntervalVector = majorScale.ToIntervalClassVector();

    var icvLookup = combinations.ToLookup(pitchClasses => pitchClasses.ToIntervalClassVector());
    var majorScaleMembers = icvLookup[majorScaleIntervalVector].ToImmutableList();
    var majorTriadMembers = icvLookup[majorTriadIntervalVector].ToImmutableList();

    var isLookup = combinations.ToLookup(pitchClasses => pitchClasses.ToIntervalPattern());
    var isMembers = isLookup[majorScaleIntervalPattern].ToImmutableList();

    _ = 1;
}



/*
foreach (var vector in Fretboard.Default.RelativePositions)
{
    Console.WriteLine(vector);
}

*/