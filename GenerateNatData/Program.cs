using System.CommandLine;
using GenerateNatData;
using GenerateNatData.Phase1;
using GenerateNatData.Phase2;

var rootCommand = new RootCommand("Generate OPTIC-K embeddings for all ergonomic guitar voicings.");

// ── generate-vectors ──────────────────────────────────────────────────────────
var generateCmd = new Command("generate-vectors",
    "Phase 1 + 2: Enumerate all voicings and compute OPTIC-K embeddings.");

var minNotesOpt = new Option<int>("--min-notes") { Description = "Minimum number of played strings.", DefaultValueFactory = _ => 2 };
var maxSpanOpt  = new Option<int>("--max-span")  { Description = "Maximum fret span of fretting-hand notes.", DefaultValueFactory = _ => 5 };
var tuningOpt   = new Option<string>("--tuning") { Description = "Tuning ID (currently only EADGBE supported).", DefaultValueFactory = _ => "EADGBE" };
var fretsOpt    = new Option<int>("--frets")     { Description = "Number of frets on the fretboard.", DefaultValueFactory = _ => 24 };
var outputOpt   = new Option<string>("--output") { Description = "Output directory for all generated files.", DefaultValueFactory = _ => "./out" };
var dryRunOpt   = new Option<bool>("--dry-run")  { Description = "Enumerate and count voicings without writing files.", DefaultValueFactory = _ => false };

generateCmd.Add(minNotesOpt);
generateCmd.Add(maxSpanOpt);
generateCmd.Add(tuningOpt);
generateCmd.Add(fretsOpt);
generateCmd.Add(outputOpt);
generateCmd.Add(dryRunOpt);

generateCmd.SetAction(async (parseResult, ct) =>
{
    var minNotes  = parseResult.GetValue(minNotesOpt);
    var maxSpan   = parseResult.GetValue(maxSpanOpt);
    var tuning    = parseResult.GetValue(tuningOpt)!;
    var frets     = parseResult.GetValue(fretsOpt);
    var outputDir = parseResult.GetValue(outputOpt)!;
    var dryRun    = parseResult.GetValue(dryRunOpt);

    var config = new ConstraintConfig
    {
        MinNotesPlayed = minNotes,
        MaxFretSpan    = maxSpan,
        TuningId       = tuning,
        FretCount      = frets
    };

    var scratchFile = Path.Combine(outputDir, $"scratch-{config.GetStableHash():X8}.bin");

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    var count = await VoicingEnumerator.EnumerateAsync(config, scratchFile, dryRun, cts.Token);

    if (!dryRun)
    {
        var embedder = new BatchEmbedder();
        await embedder.EmbedAsync(scratchFile, outputDir, config, cts.Token);
    }

    Console.WriteLine($"\nDone. {count:N0} voicings processed.");
    return 0;
});

rootCommand.Add(generateCmd);

// ── embed-vectors ─────────────────────────────────────────────────────────────
var embedCmd = new Command("embed-vectors",
    "Phase 2 only: Compute OPTIC-K embeddings from an existing scratch file.");

var scratchArg     = new Argument<string>("scratch-file") { Description = "Path to scratch binary from generate-vectors." };
var embedOutputOpt = new Option<string>("--output") { Description = "Output directory.", DefaultValueFactory = _ => "./out" };

embedCmd.Add(scratchArg);
embedCmd.Add(embedOutputOpt);

embedCmd.SetAction(async (parseResult, ct) =>
{
    var scratchPath = parseResult.GetValue(scratchArg)!;
    var outputDir   = parseResult.GetValue(embedOutputOpt)!;

    if (!File.Exists(scratchPath))
    {
        Console.Error.WriteLine($"Error: scratch file not found: {scratchPath}");
        return 1;
    }

    var (count, _, _) = VoicingEnumerator.ReadHeader(scratchPath);
    Console.WriteLine($"[embed-vectors] {count:N0} voicings in {scratchPath}");

    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    var config = new ConstraintConfig();
    var embedder = new BatchEmbedder();
    await embedder.EmbedAsync(scratchPath, outputDir, config, cts.Token);

    Console.WriteLine($"\nDone. {count:N0} voicings embedded.");
    return 0;
});

rootCommand.Add(embedCmd);

return await new CommandLineConfiguration(rootCommand).InvokeAsync(args);
