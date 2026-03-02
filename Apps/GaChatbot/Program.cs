using GA.Domain.Services.Abstractions;
using GA.Domain.Core.Instruments;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using GaChatbot.Abstractions;
using GaChatbot.Services;
using GaChatbot.Models;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Musical.Enrichment;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Search;
using GA.Business.ML.Embeddings;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Configuration;
using GA.Business.ML.Extensions;
using GaChatbot.Extensions;
using GaChatbot.Services.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// ---- Core Chatbot Service Registration ----
builder.Services.AddGaChatbotServices();

// Override for remote index if needed
// builder.Services.AddSingleton<IVectorIndex>(sp => new GaApiClientVectorIndex(new HttpClient { BaseAddress = new Uri("http://localhost:5000") }, sp.GetRequiredService<ILogger<GaApiClientVectorIndex>>()));

var app = builder.Build();

// Run the interactive loop
Console.WriteLine("=== Spectral RAG Chatbot (MVP Phase 15 - Qdrant Backend) ===");

// Get DI-resolved services
var orchestrator = app.Services.GetRequiredService<ProductionOrchestrator>();

while (true)
{
    Console.WriteLine("\nType a query (or 'exit' to quit):");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input) || input.ToLower() == "exit") break;

    try
    {
        var response = await orchestrator.AnswerAsync(new ChatRequest(input));
        
        Console.WriteLine("\n[GA]: " + response.NaturalLanguageAnswer);
        
        if (response.Candidates.Any())
        {
            Console.WriteLine("\nTop Matches:");
            foreach (var c in response.Candidates.Take(3))
            {
                Console.WriteLine($"  • {c.DisplayName} ({c.Shape}) - {c.Score:F2}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
}
