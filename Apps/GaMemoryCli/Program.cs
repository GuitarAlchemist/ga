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
    "curate"               => await RunCurateAsync(args[1..]),
    "diff"                 => RunDiff(args[1..]),
    "promote"              => RunPromote(args[1..]),
    "migrate-transcripts"  => RunMigrateTranscripts(args[1..]),
    "--help" or "-h" or "help" => PrintUsage(),
    _ => UnknownCommand(command),
};

// ── Commands ──────────────────────────────────────────────────────────────

static async Task<int> RunCurateAsync(string[] args)
{
    string? instructions = null;
    // Type-filter default: skip "response" entries. The chatbot's MemoryHook
    // writes one per chat turn — these are transient logs, not durable
    // knowledge. The curator is designed for fact / preference / focus
    // entries (the kind that benefit from merging duplicates + replacing
    // stale values). Operator can opt back in with --include-responses.
    // Discovered 2026-05-11: 100% of a real ~/.ga/memory.json was type=
    // response, dominating the curator input with content it has no useful
    // curation to do on.
    var includeResponses = false;
    var excludeTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "response" };
    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--instructions" && i + 1 < args.Length)
            instructions = args[++i];
        else if (args[i] == "--include-responses")
            includeResponses = true;
    }
    if (includeResponses) excludeTypes.Remove("response");

    var storePath = MemoryStore.DefaultStorePath;
    if (!File.Exists(storePath))
    {
        Console.Error.WriteLine($"No store at {storePath} — nothing to curate.");
        return 2;
    }

    var allEntries = LoadEntries(storePath);
    var entries = allEntries.Where(e => !excludeTypes.Contains(e.Type)).ToList();
    var skipped = allEntries.Count - entries.Count;
    Console.WriteLine(
        $"Loaded {allEntries.Count} entries from {storePath}" +
        (skipped > 0
            ? $" (filtered {skipped} entries with excluded types: {string.Join(", ", excludeTypes)}; " +
              "pass --include-responses to keep them)"
            : ""));

    if (entries.Count == 0)
    {
        Console.WriteLine();
        Console.WriteLine("Nothing left to curate after type filtering. This usually means the");
        Console.WriteLine("store is dominated by transient 'response' entries — the curator is");
        Console.WriteLine("designed for durable memory (fact / preference / focus). Either:");
        Console.WriteLine("  • Wait for the chatbot to accumulate non-response memory entries, or");
        Console.WriteLine("  • Pass --include-responses to curate them anyway (not recommended).");
        return 0;
    }

    var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();
    if (!AnthropicProvider.IsAvailable(config))
    {
        Console.Error.WriteLine(
            "ANTHROPIC_API_KEY is not set. Curation needs a real model — refusing to run.");
        return 3;
    }

    // 5-minute HTTP timeout — Sonnet 4.6 at MaxOutputTokens=32k can take
    // 60–150 s wall-clock to generate. The SDK default of 100 s was hit
    // during the first end-to-end smoke (2026-05-11).
    var chatClient = AnthropicProvider.CreateChatClient(
        config, AnthropicProvider.DefaultModel, timeout: TimeSpan.FromMinutes(5));
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

/// <summary>
/// One-shot migration: drains legacy type=response entries from
/// ~/.ga/memory.json into ~/.ga/transcripts.json. Default is dry-run;
/// pass --apply to commit. Idempotent — re-running after --apply finds
/// nothing to do. Backs up both files to <c>.bak-{stamp}</c> before
/// writing. See <see cref="LegacyResponseMigration"/> for design notes.
/// </summary>
static int RunMigrateTranscripts(string[] args)
{
    var apply       = false;
    var memoryPath  = MemoryStore.DefaultStorePath;
    var transcriptPath = ChatTranscriptStore.DefaultStorePath;

    for (var i = 0; i < args.Length; i++)
    {
        if (args[i] == "--apply") apply = true;
        else if (args[i] == "--memory-path" && i + 1 < args.Length) memoryPath = args[++i];
        else if (args[i] == "--transcripts-path" && i + 1 < args.Length) transcriptPath = args[++i];
        else if (args[i] == "--dry-run") apply = false;   // explicit no-op alias for readability
    }

    if (!File.Exists(memoryPath))
    {
        Console.Error.WriteLine($"No memory store at {memoryPath} — nothing to migrate.");
        return 2;
    }

    var memoryEntries = LoadEntries(memoryPath);
    var existingTurns = LoadTranscriptTurns(transcriptPath);

    var plan = LegacyResponseMigration.Plan(memoryEntries, existingTurns);

    Console.WriteLine($"memory store : {memoryPath}  ({memoryEntries.Count} entries)");
    Console.WriteLine($"transcripts  : {transcriptPath}  ({existingTurns.Count} turns)");
    Console.WriteLine();
    Console.WriteLine("Plan:");
    Console.WriteLine($"  type=response → migrate    : {plan.EntriesToMigrate.Count}");
    Console.WriteLine($"  type=response → already-migrated, will be dropped : {plan.EntriesAlreadyMigrated.Count}");
    Console.WriteLine($"  non-response → keep        : {plan.EntriesToKeep.Count}");
    Console.WriteLine($"  new transcript turns       : {plan.NewTranscriptTurns.Count} (SessionId='{LegacyResponseMigration.LegacySessionId}', Role='assistant')");

    if (plan.IsNoOp)
    {
        Console.WriteLine();
        Console.WriteLine("Nothing to do — memory.json contains no type=response entries.");
        return 0;
    }

    if (!apply)
    {
        Console.WriteLine();
        Console.WriteLine("Dry-run (no files written). Pass --apply to commit.");
        return 0;
    }

    // ── --apply path ─────────────────────────────────────────────────────
    var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
    var memoryBackup = memoryPath + ".bak-" + stamp;
    var transcriptBackup = File.Exists(transcriptPath) ? transcriptPath + ".bak-" + stamp : null;

    Console.WriteLine();
    Console.WriteLine("Backing up:");
    File.Copy(memoryPath, memoryBackup, overwrite: false);
    Console.WriteLine($"  {memoryPath} -> {memoryBackup}");
    if (transcriptBackup is not null)
    {
        File.Copy(transcriptPath, transcriptBackup, overwrite: false);
        Console.WriteLine($"  {transcriptPath} -> {transcriptBackup}");
    }

    // Merge existing + new transcript turns, write atomically.
    var mergedTurns = existingTurns.Concat(plan.NewTranscriptTurns).ToList();
    AtomicWrite(transcriptPath, JsonSerializer.Serialize(mergedTurns, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Wrote {mergedTurns.Count} turns to {transcriptPath} (was {existingTurns.Count}).");

    // Rewrite memory.json with the kept entries only.
    AtomicWrite(memoryPath, JsonSerializer.Serialize(plan.EntriesToKeep, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Wrote {plan.EntriesToKeep.Count} entries to {memoryPath} (was {memoryEntries.Count}; removed {memoryEntries.Count - plan.EntriesToKeep.Count}).");

    Console.WriteLine();
    Console.WriteLine("Done. Re-run `ga-memory migrate-transcripts` to verify it's a no-op.");
    Console.WriteLine($"Backups retained at {memoryBackup}" + (transcriptBackup is not null ? $" and {transcriptBackup}" : "") + " — delete after you've verified the chatbot still boots.");
    return 0;
}

static IReadOnlyList<TranscriptTurnEntry> LoadTranscriptTurns(string path)
{
    if (!File.Exists(path)) return [];
    var json = File.ReadAllText(path);
    var turns = JsonSerializer.Deserialize<List<TranscriptTurnEntry>>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    return turns ?? [];
}

static void AtomicWrite(string path, string content)
{
    var dir = Path.GetDirectoryName(path);
    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        Directory.CreateDirectory(dir);
    var tmp = path + ".tmp";
    File.WriteAllText(tmp, content);
    File.Move(tmp, path, overwrite: true);
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
    Console.WriteLine("  curate [--instructions \"...\"] [--include-responses]");
    Console.WriteLine("                                  Run the curator, write a candidate.");
    Console.WriteLine("                                  By default skips type=response entries");
    Console.WriteLine("                                  (transient chat logs). Pass");
    Console.WriteLine("                                  --include-responses to curate them.");
    Console.WriteLine("  diff <candidate-path>           Show the diff summary for a candidate");
    Console.WriteLine("  promote <candidate-path>        Atomic swap (with .bak backup)");
    Console.WriteLine("  migrate-transcripts [--apply]   Drain legacy type=response entries from");
    Console.WriteLine("                                  memory.json into transcripts.json. Default");
    Console.WriteLine("                                  is dry-run; --apply commits with .bak backups.");
    Console.WriteLine("                                  Idempotent; safe to re-run.");
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
