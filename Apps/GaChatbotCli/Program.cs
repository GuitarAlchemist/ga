/// <summary>
/// GaChatbotCli — run the full production orchestration stack from the command line.
///
/// Usage:
///   dotnet run --project Apps/GaChatbotCli -- "parse Am7 for me"
///   dotnet run --project Apps/GaChatbotCli -- --json "what scale is in Am7?"
///
/// Prints routing metadata + response to stdout.
/// Set ANTHROPIC_API_KEY to enable SKILL.md-driven skills.
/// </summary>

using System.Text.Json;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Extensions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Embeddings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── Parse CLI arguments ────────────────────────────────────────────────────
bool jsonOutput = false;
string? message = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--json") { jsonOutput = true; continue; }
    if (args[i].StartsWith("--")) continue;
    message ??= args[i];
}

if (string.IsNullOrWhiteSpace(message))
{
    Console.Error.WriteLine("Usage: GaChatbotCli [--json] \"<message>\"");
    Console.Error.WriteLine("  --json   Output structured JSON instead of plain text");
    return 1;
}

// ── DI Container ───────────────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
if (!jsonOutput)
    builder.Logging.AddConsole(o => o.FormatterName = "simple");

builder.Services.AddChatbotOrchestration();

// In-memory vector index (no MongoDB required for CLI)
builder.Services.AddSingleton<IVectorIndex, InMemoryVectorIndex>();

// No narrator needed for CLI (we return raw answer)
// Narrator registration is optional — ProductionOrchestrator doesn't require one

var host = builder.Build();

// ── Execute ────────────────────────────────────────────────────────────────
using var scope = host.Services.CreateScope();
var orchestrator = scope.ServiceProvider.GetRequiredService<IHarmonicChatOrchestrator>();

try
{
    var response = await orchestrator.AnswerAsync(new ChatRequest(message));

    if (jsonOutput)
    {
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            answer   = response.NaturalLanguageAnswer,
            routing  = new
            {
                agentId       = response.Routing?.AgentId,
                confidence    = response.Routing?.Confidence,
                routingMethod = response.Routing?.RoutingMethod,
            },
            candidates = response.Candidates.Take(3).Select(c => new
            {
                c.DisplayName,
                c.Shape,
                c.Score,
            }),
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
    else
    {
        Console.WriteLine();
        Console.WriteLine("[GA]: " + response.NaturalLanguageAnswer);

        if (response.Routing is { } r)
            Console.WriteLine($"\n[routing: {r.AgentId} via {r.RoutingMethod} (conf={r.Confidence:F2})]");

        if (response.Candidates.Any())
        {
            Console.WriteLine("\nTop matches:");
            foreach (var c in response.Candidates.Take(3))
                Console.WriteLine($"  • {c.DisplayName} ({c.Shape})  score={c.Score:F2}");
        }
    }

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 2;
}
