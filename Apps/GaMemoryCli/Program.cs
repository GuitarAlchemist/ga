/// <summary>
/// GaMemoryCli — Dreams-lite curator over the chatbot MemoryStore.
///
/// Usage:
///   dotnet run --project Apps/GaMemoryCli -- curate [--instructions "..."]
///   dotnet run --project Apps/GaMemoryCli -- diff   &lt;candidate-path&gt;
///   dotnet run --project Apps/GaMemoryCli -- promote &lt;candidate-path&gt;
///
/// Set ANTHROPIC_API_KEY before `curate`. `diff` and `promote` are offline.
/// See docs/plans/2026-05-10-feat-dreams-lite-memory-curator-plan.md.
/// </summary>

using System.Text.Json;
using GA.Business.ML.Agents.Memory;
using GA.Business.ML.Agents.Memory.Curator;
using GA.Providers.Anthropic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

if (args.Length == 0)
{
    PrintUsage();
    return 1;
}

var command = args[0];
return command switch
{
    "curate"  => await RunCurateAsync(args[1..]),
    "diff"    => RunDiff(args[1..]),
    "promote" => RunPromote(args[1..]),
    "--help" or "-h" or "help" => PrintUsage(),
    _ => UnknownCommand(command),
};

// ── Commands ──────────────────────────────────────────────────────────────

static async Task<int> RunCurateAsync(string[] args)
{
    string? instructions = null;
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--instructions" && i + 1 < args.Length)
            instructions = args[++i];
    }

    var storePath = MemoryStore.DefaultStorePath;
    if (!File.Exists(storePath))
    {
        Console.Error.WriteLine($"No store at {storePath} — nothing to curate.");
        return 2;
    }

    var entries = LoadEntries(storePath);
    Console.WriteLine($"Loaded {entries.Count} entries from {storePath}.");

    var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    if (!AnthropicProvider.IsAvailable(config))
    {
        Console.Error.WriteLine(
            "ANTHROPIC_API_KEY is not set. Curation needs a real model — refusing to run.");
        return 3;
    }

    var chatClient = AnthropicProvider.CreateChatClient(config, AnthropicProvider.DefaultModel);
    var curator = new MemoryCurator(chatClient,
        LoggerFactory.Create(b => b.AddSimpleConsole()).CreateLogger<MemoryCurator>());

    var runId = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6];
    var ledgerDir = Path.Combine(
        Directory.GetCurrentDirectory(), "state", "quality", "memory-curator",
        DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"));
    Directory.CreateDirectory(ledgerDir);

    var startedAt = DateTimeOffset.UtcNow;
    MemoryCurationResult result;
    try
    {
        result = await curator.CurateAsync(new MemoryCurationRequest(
            ExistingEntries: entries,
            RecentTranscripts: [],  // v0.1: no transcript plumbing yet
            Instructions: instructions));
    }
    catch (MemoryCurationException ex)
    {
        WriteLedgerFailure(ledgerDir, runId, startedAt, storePath, entries.Count, ex);
        Console.Error.WriteLine($"Curation refused: {ex.Message}");
        return 4;
    }

    var candidatePath = Path.Combine(ledgerDir, $"memory.v2-candidate-{runId}.json");
    File.WriteAllText(candidatePath,
        JsonSerializer.Serialize(result.CandidateEntries, new JsonSerializerOptions { WriteIndented = true }));

    WriteLedgerSuccess(ledgerDir, runId, startedAt, storePath, entries.Count,
        candidatePath, result);

    Console.WriteLine();
    Console.WriteLine($"Candidate written: {candidatePath}");
    PrintDiffSummary(result.Diff);
    Console.WriteLine();
    Console.WriteLine($"Review with: ga-memory diff {candidatePath}");
    Console.WriteLine($"Promote with: ga-memory promote {candidatePath}");
    return 0;
}

static int RunDiff(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: ga-memory diff <candidate-path>");
        return 1;
    }

    var candidatePath = args[0];
    if (!File.Exists(candidatePath))
    {
        Console.Error.WriteLine($"Candidate not found: {candidatePath}");
        return 2;
    }

    // Locate the ledger entry by sibling pattern: run-<id>.json next to the candidate.
    var runId = ExtractRunId(candidatePath);
    var ledgerPath = Path.Combine(Path.GetDirectoryName(candidatePath)!, $"run-{runId}.json");
    if (!File.Exists(ledgerPath))
    {
        Console.Error.WriteLine($"Ledger entry not found at {ledgerPath} — cannot show diff.");
        return 3;
    }

    var ledger = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(ledgerPath));
    if (ledger.TryGetProperty("result", out var resultElem) &&
        resultElem.TryGetProperty("diffSummary", out var summary))
    {
        Console.WriteLine("Diff summary:");
        foreach (var p in summary.EnumerateObject())
            Console.WriteLine($"  {p.Name,-10} {p.Value.GetInt32()}");
    }
    else
    {
        Console.Error.WriteLine("Ledger missing diffSummary.");
        return 4;
    }
    return 0;
}

static int RunPromote(string[] args)
{
    if (args.Length == 0)
    {
        Console.Error.WriteLine("Usage: ga-memory promote <candidate-path>");
        return 1;
    }

    var candidatePath = args[0];
    if (!File.Exists(candidatePath))
    {
        Console.Error.WriteLine($"Candidate not found: {candidatePath}");
        return 2;
    }

    var livePath = MemoryStore.DefaultStorePath;
    var backupPath = livePath + ".bak-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");

    Console.WriteLine($"About to promote:");
    Console.WriteLine($"  live store : {livePath}");
    Console.WriteLine($"  candidate  : {candidatePath}");
    Console.WriteLine($"  backup     : {backupPath}");
    Console.Write("Proceed? [y/N] ");
    var confirm = Console.ReadLine()?.Trim();
    if (!string.Equals(confirm, "y", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("Aborted.");
        return 0;
    }

    if (File.Exists(livePath))
        File.Move(livePath, backupPath);
    File.Copy(candidatePath, livePath);

    Console.WriteLine($"Promoted. Backup at {backupPath} — keep until you've verified the new store is working.");
    return 0;
}

// ── Helpers ───────────────────────────────────────────────────────────────

static int PrintUsage()
{
    Console.WriteLine("ga-memory — Dreams-lite curator over the chatbot MemoryStore");
    Console.WriteLine();
    Console.WriteLine("Commands:");
    Console.WriteLine("  curate [--instructions \"...\"]   Run the curator, write a candidate");
    Console.WriteLine("  diff <candidate-path>           Show the diff summary for a candidate");
    Console.WriteLine("  promote <candidate-path>        Atomic swap (with .bak backup)");
    Console.WriteLine();
    Console.WriteLine("Env:");
    Console.WriteLine("  ANTHROPIC_API_KEY              required for `curate` (Sonnet 4.6 by default)");
    return 0;
}

static int UnknownCommand(string cmd)
{
    Console.Error.WriteLine($"Unknown command: {cmd}");
    PrintUsage();
    return 1;
}

static IReadOnlyList<MemoryEntry> LoadEntries(string storePath)
{
    var json = File.ReadAllText(storePath);
    var entries = JsonSerializer.Deserialize<List<MemoryEntry>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return entries ?? new List<MemoryEntry>();
}

static void PrintDiffSummary(CurationDiff diff)
{
    Console.WriteLine();
    Console.WriteLine("Diff:");
    Console.WriteLine($"  kept     {diff.Kept.Count}");
    Console.WriteLine($"  merged   {diff.Merged.Count}");
    Console.WriteLine($"  replaced {diff.Replaced.Count}");
    Console.WriteLine($"  new      {diff.NewItems.Count}");
    Console.WriteLine($"  dropped  {diff.Dropped.Count}");
}

static string ExtractRunId(string candidatePath)
{
    // memory.v2-candidate-<id>.json → <id>
    var name = Path.GetFileNameWithoutExtension(candidatePath);
    const string prefix = "memory.v2-candidate-";
    return name.StartsWith(prefix, StringComparison.Ordinal)
        ? name[prefix.Length..]
        : name;
}

static void WriteLedgerSuccess(
    string ledgerDir, string runId, DateTimeOffset startedAt,
    string storePath, int entryCount, string candidatePath, MemoryCurationResult result)
{
    var ledger = new
    {
        runId,
        startedAt,
        completedAt = DateTimeOffset.UtcNow,
        status = "completed",
        modelId = result.ModelId,
        inputs = new { storePath, entryCount, transcriptCount = 0, instructionsLength = 0 },
        result = new
        {
            candidatePath,
            diffSummary = new
            {
                kept     = result.Diff.Kept.Count,
                merged   = result.Diff.Merged.Count,
                replaced = result.Diff.Replaced.Count,
                newItems = result.Diff.NewItems.Count,
                dropped  = result.Diff.Dropped.Count,
            },
            tokens = new { input = result.InputTokens, output = result.OutputTokens },
        },
    };
    File.WriteAllText(Path.Combine(ledgerDir, $"run-{runId}.json"),
        JsonSerializer.Serialize(ledger, new JsonSerializerOptions { WriteIndented = true }));
}

static void WriteLedgerFailure(
    string ledgerDir, string runId, DateTimeOffset startedAt,
    string storePath, int entryCount, MemoryCurationException ex)
{
    var ledger = new
    {
        runId,
        startedAt,
        completedAt = DateTimeOffset.UtcNow,
        status = "failed",
        inputs = new { storePath, entryCount, transcriptCount = 0, instructionsLength = 0 },
        error = new { type = "validation_failed", message = ex.Message },
    };
    File.WriteAllText(Path.Combine(ledgerDir, $"run-{runId}.json"),
        JsonSerializer.Serialize(ledger, new JsonSerializerOptions { WriteIndented = true }));
}
