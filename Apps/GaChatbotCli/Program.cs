/// <summary>
/// GaChatbotCli — run the full production orchestration stack from the command line.
///
/// Usage:
///   dotnet run --project Apps/GaChatbotCli -- "parse Am7 for me"
///   dotnet run --project Apps/GaChatbotCli -- --json "what scale is in Am7?"
///   dotnet run --project Apps/GaChatbotCli -- -i              (interactive mode)
///
/// Prints routing metadata + response to stdout.
/// Set ANTHROPIC_API_KEY to enable SKILL.md-driven skills.
/// </summary>

using System.Text.Json;
using Anthropic;
using GA.Business.Core.Orchestration.Abstractions;
using GA.Business.Core.Orchestration.Extensions;
using GA.Business.Core.Orchestration.Models;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Extensions;
using GA.Infrastructure.Documentation;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// ── Parse CLI arguments ────────────────────────────────────────────────────
bool jsonOutput = false;
bool interactive = false;
string? message = null;

for (int i = 0; i < args.Length; i++)
{
    if (args[i] is "--json") { jsonOutput = true; continue; }
    if (args[i] is "--interactive" or "-i") { interactive = true; continue; }
    if (args[i].StartsWith("--")) continue;
    message ??= args[i];
}

if (!interactive && string.IsNullOrWhiteSpace(message))
{
    Console.Error.WriteLine("Usage: GaChatbotCli [--json] [--interactive|-i] \"<message>\"");
    Console.Error.WriteLine("  --json          Output structured JSON instead of plain text");
    Console.Error.WriteLine("  --interactive   Start interactive multi-turn session");
    return 1;
}

// ── DI Container ───────────────────────────────────────────────────────────
var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
if (!jsonOutput)
    builder.Logging.AddConsole(o => o.FormatterName = "simple");

builder.Services.AddChatbotOrchestration();

// Register InMemoryVectorIndex first so AddGuitarAlchemistAI's TryAdd won't override it.
builder.Services.AddSingleton<IVectorIndex, InMemoryVectorIndex>();

// Register all ML services (agents, SpectralRetrievalService, VoicingExplanationService, etc.)
builder.Services.AddGuitarAlchemistAI();

// SchemaDiscoveryService is not in AddGuitarAlchemistAI — register it explicitly.
builder.Services.TryAddSingleton<SchemaDiscoveryService>();

// SemanticRouter accepts a nullable IEmbeddingGenerator but DI still resolves it.
// Register a no-op generator so keyword fallback routing is used instead of semantic routing.
builder.Services.TryAddSingleton<IEmbeddingGenerator<string, Embedding<float>>, NullEmbeddingGenerator>();

// IChatClient for the base agents (TheoryAgent, TabAgent, etc.) and SKILL.md skills.
// Prefer Anthropic when ANTHROPIC_API_KEY is set; fall back to Ollama for local dev.
var anthropicKeyForCli = builder.Configuration["Anthropic:ApiKey"]
    ?? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
builder.Services.TryAddSingleton<IChatClient>(_ =>
    !string.IsNullOrWhiteSpace(anthropicKeyForCli)
        ? new AnthropicClient { ApiKey = anthropicKeyForCli }
              .AsIChatClient("claude-sonnet-4-6")
              .AsBuilder()
              .UseFunctionInvocation()
              .Build()
        : (IChatClient)new OllamaChatClient(new Uri("http://localhost:11434"), "llama3.2"));

// Stub narrator for CLI — SpectralRagOrchestrator requires IGroundedNarrator but the CLI
// surfaces the raw orchestrator answer; the narrator result is never used here.
builder.Services.TryAddScoped<IGroundedNarrator, NullGroundedNarrator>();

var host = builder.Build();

// ── Execute ────────────────────────────────────────────────────────────────
using var scope = host.Services.CreateScope();
var orchestrator = scope.ServiceProvider.GetRequiredService<IHarmonicChatOrchestrator>();

try
{
    if (interactive)
        return await RunInteractiveAsync(orchestrator);

    return await RunSingleShotAsync(orchestrator, message!, jsonOutput);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Error: {ex.Message}");
    return 2;
}

// ── Single-shot mode (with streaming) ─────────────────────────────────────
static async Task<int> RunSingleShotAsync(IHarmonicChatOrchestrator orchestrator, string msg, bool json)
{
    if (json)
    {
        // JSON mode: stream AG-UI events to stderr, full JSON to stdout
        Console.Error.Write("[GA]: ");
        var response = await orchestrator.AnswerStreamingAsync(
            new ChatRequest(msg),
            async token => { Console.Error.Write(token); await Task.CompletedTask; });
        Console.Error.WriteLine();

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
        // Plain text mode: stream tokens to stdout
        Console.WriteLine();
        Console.Write("[GA]: ");
        var response = await orchestrator.AnswerStreamingAsync(
            new ChatRequest(msg),
            async token => { Console.Write(token); await Task.CompletedTask; });
        Console.WriteLine();

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

// ── Interactive mode ──────────────────────────────────────────────────────
static async Task<int> RunInteractiveAsync(IHarmonicChatOrchestrator orchestrator)
{
    var sessionId = Guid.NewGuid().ToString("N");
    var history = new List<ConversationTurn>();

    Console.WriteLine("Guitar Alchemist — interactive mode (type 'exit' or 'quit' to leave)");
    Console.WriteLine();

    while (true)
    {
        Console.Write("[You]: ");
        var input = Console.ReadLine();
        if (input is null or "exit" or "quit") break;
        if (string.IsNullOrWhiteSpace(input)) continue;

        history.Add(new ConversationTurn("user", input, DateTimeOffset.UtcNow));

        Console.Write("[GA]: ");
        var response = await orchestrator.AnswerStreamingAsync(
            new ChatRequest(input, sessionId, History: history),
            async token => { Console.Write(token); await Task.CompletedTask; });
        Console.WriteLine();

        history.Add(new ConversationTurn("assistant", response.NaturalLanguageAnswer, DateTimeOffset.UtcNow));

        // Keep history bounded
        if (history.Count > 20)
            history.RemoveRange(0, history.Count - 20);

        if (response.Routing is { } r)
            Console.WriteLine($"[routing: {r.AgentId} via {r.RoutingMethod} (conf={r.Confidence:F2})]");

        Console.WriteLine();
    }

    Console.WriteLine("Goodbye!");
    return 0;
}

// ── CLI-only stubs ────────────────────────────────────────────────────────
// SpectralRagOrchestrator requires IGroundedNarrator. The CLI surfaces the
// ProductionOrchestrator's NaturalLanguageAnswer directly, so the narrator
// result is unused — this stub satisfies the DI graph without an LLM call.
file sealed class NullGroundedNarrator : IGroundedNarrator
{
    public Task<string> NarrateAsync(string query, IReadOnlyList<CandidateVoicing> candidates) =>
        Task.FromResult(candidates.Count > 0
            ? $"{candidates.Count} chord matches found."
            : "No matches found.");
}

// No-op embedding generator — SemanticRouter falls back to keyword routing when null is resolved.
file sealed class NullEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    public EmbeddingGeneratorMetadata Metadata { get; } = new("null", null, null, null);

    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new GeneratedEmbeddings<Embedding<float>>([]));

    public object? GetService(Type serviceType, object? serviceKey) => null;

    public void Dispose() { }
}
