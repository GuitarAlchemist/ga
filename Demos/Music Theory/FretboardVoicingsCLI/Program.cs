namespace FretboardVoicingsCLI;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using GA.Business.Core.Analysis.Voicings;
using GA.Business.ML.Rag;
using GA.Domain.Core.Instruments;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Services.Fretboard.Voicings.Analysis;
using GA.Domain.Services.Fretboard.Voicings.Filtering;
using GA.Domain.Services.Fretboard.Voicings.Generation;
using Spectre.Console;

internal class Program
{
    /// <summary>
    ///     Tuning presets selectable from the command line via --tuning.
    /// </summary>
    private enum TuningPreset
    {
        Guitar,
        Bass,
        Ukulele
    }

    /// <summary>
    ///     Export mode options parsed from command-line arguments
    /// </summary>
    private sealed record ExportOptions(int? MaxVoicings, bool ShowHelp, TuningPreset Tuning);

    /// <summary>
    ///     Export-embeddings mode options parsed from command-line arguments.
    ///     When Tuning is null, all three instruments are exported.
    /// </summary>
    private sealed record ExportEmbeddingsOptions(
        int? MaxVoicings,
        bool ShowHelp,
        string OutputPath,
        TuningPreset? Tuning,
        bool NoDedup);

    private static async Task<int> Main(string[] args)
    {
        // Check for export-embeddings mode BEFORE export mode (--export-embeddings contains --export as prefix)
        if (TryParseExportEmbeddingsOptions(args, out var embeddingsOptions))
        {
            if (embeddingsOptions.ShowHelp)
            {
                DisplayExportEmbeddingsHelp();
                return 0;
            }

            return await RunExportEmbeddingsAsync(embeddingsOptions);
        }

        // Check for export mode BEFORE any AnsiConsole output
        if (TryParseExportOptions(args, out var exportOptions))
        {
            if (exportOptions.ShowHelp)
            {
                DisplayExportHelp();
                return 0;
            }

            return await RunExportAsync(exportOptions);
        }

        // === Interactive mode (existing behavior) ===
        AnsiConsole.Write(new FigletText("Fretboard Voicings").Color(Color.Green));
        AnsiConsole.MarkupLine("[dim]Finding all possible voicings on a 6-string guitar in standard tuning[/]\n");

        // Parse command-line arguments for filtering
        var filterCriteria = ParseArguments(args);

        // Create fretboard with standard tuning (6 strings, 24 frets, E2 A2 D3 G3 B3 E4)
        var fretboard = Fretboard.Default;

        // Create collection of all relative fret vectors (5-fret extent, 6 strings)
        var vectorCollection = new RelativeFretVectorCollection(6, 5);

        AnsiConsole.MarkupLine("[dim]Using optimized voicing generation with parallel processing[/]");
        AnsiConsole.MarkupLine(
            $"[dim]Vector collection: {vectorCollection.Count:N0} total vectors, {vectorCollection.PrimeForms.Count:N0} prime forms[/]\n");

        const int windowSize = 4; // 5 frets: [start, start+4]
        const int maxStartFret = 22 - windowSize; // 18

        var totalStartTime = DateTime.Now;

        // Generate all voicings using the core library (with parallel processing and deduplication)
        AnsiConsole.MarkupLine(
            $"[dim]Generating all voicings across {maxStartFret + 1} windows using {Environment.ProcessorCount} cores[/]\n");

        var allVoicings = VoicingGenerator.GenerateAllVoicings(
            fretboard,
            windowSize,
            2,
            true);

        AnsiConsole.MarkupLine($"[green]Generated {allVoicings.Count:N0} unique voicings[/]\n");

        // Show first 20 voicings to verify ordering starts at fret 0
        AnsiConsole.MarkupLine("[yellow]First 20 voicings (to verify ordering):[/]\n");
        var first20 = allVoicings.Take(20).ToList();
        foreach (var voicing in first20)
        {
            AnsiConsole.MarkupLine($"  {VoicingExtensions.GetPositionDiagram(voicing.Positions)}");
        }

        // Decompose voicings using the core library
        var decompositionStart = DateTime.Now;
        var allDecomposed = VoicingDecomposer.DecomposeVoicings(allVoicings, vectorCollection);
        var decompositionElapsed = DateTime.Now - decompositionStart;

        // Keep only prime forms
        var primeFormsOnly = allDecomposed.Where(d => d.PrimeForm != null).ToList();

        AnsiConsole.MarkupLine(
            $"\n[dim]Decomposition: {allDecomposed.Count:N0} voicings → {primeFormsOnly.Count:N0} prime forms ({decompositionElapsed.TotalMilliseconds:N0}ms)[/]\n");

        allDecomposed = primeFormsOnly;

        var totalElapsed = DateTime.Now - totalStartTime;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"[bold green]Total: {allDecomposed.Count:N0} unique prime form voicings across entire fretboard[/]");
        AnsiConsole.MarkupLine(
            $"[dim]Total time: {totalElapsed.TotalSeconds:N1}s ({totalElapsed.TotalMilliseconds:N0}ms)[/]");

        // Display filter criteria
        DisplayFilterCriteria(filterCriteria);

        // Apply filters and show matching voicings
        AnsiConsole.MarkupLine(
            $"[dim]Applying filters and showing up to {filterCriteria.MaxResults} matching voicings...[/]");

        var filteringStart = DateTime.Now;
        var primeFormVoicings = allDecomposed.Select(d => d.Voicing);
        var filteredVoicings = VoicingFilters.ApplyFilters(primeFormVoicings, filterCriteria);

        DisplayFilteredVoicings(filteredVoicings);

        var filteringElapsed = DateTime.Now - filteringStart;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(
            $"[dim]Filtering time: {filteringElapsed.TotalSeconds:F2}s ({filteringElapsed.TotalMilliseconds:N0}ms)[/]");

        AnsiConsole.MarkupLine("\n[green]Press any key to exit...[/]");
        Console.ReadKey();
        return 0;
    }

    /// <summary>
    ///     Displays filtered voicings in YAML format with comprehensive musical analysis
    /// </summary>
    private static void DisplayFilteredVoicings(
        IEnumerable<(Voicing Voicing, MusicalVoicingAnalysis Analysis)> filteredVoicings)
    {
        var samples = filteredVoicings.ToList();

        Console.WriteLine("voicings:");

        foreach (var (voicing, analysis) in samples)
        {
            var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
            var midiNotes = string.Join(", ", analysis.MidiNotes);

            // Fix: handle int[] midi notes conversion to note names
            var noteNames =
                string.Join(", ",
                    analysis.MidiNotes.Select(n =>
                        $"\"{(MidiNote)n}\"")); // MidiNote.ToString() prints value? No, I need note name.
            // MidiNote has ToSharpNote().
            // So: ((MidiNote)n).ToSharpNote().
            noteNames = string.Join(", ", analysis.MidiNotes.Select(n => $"\"{((MidiNote)n).ToSharpNote()}\""));

            var pitchClasses = analysis.PitchClassSet.ToString();
            var chordName = analysis.ChordId.ChordName ?? "Unknown";

            if (analysis.SymmetricalInfo != null)
            {
                chordName = analysis.SymmetricalInfo.ScaleName; // Fixed property access
            }

            var keyFunction = analysis.ChordId.FunctionalDescription ?? "Atonal";
            if (!analysis.ChordId.IsNaturallyOccurring && analysis.ChordId.ClosestKey != null)
            {
                keyFunction += " (chromatic)";
            }

            var icv = analysis.IntervallicInfo.IntervalClassVector;
            var features = string.Join(", ", analysis.IntervallicInfo.Features);
            if (string.IsNullOrEmpty(features))
            {
                features = "none";
            }

            Console.WriteLine($"  - diagram: \"{diagram}\"");
            Console.WriteLine($"    midi_notes: [{midiNotes}]");
            Console.WriteLine($"    notes: [{noteNames}]");
            Console.WriteLine($"    pitch_classes: \"{pitchClasses}\"");
            Console.WriteLine("    chord:");
            Console.WriteLine($"      name: \"{chordName}\"");

            if (analysis.AlternateChordNames != null)
            {
                var alt = analysis.AlternateChordNames.FirstOrDefault(n => n != chordName);
                if (alt != null)
                {
                    Console.WriteLine($"      alternate_name: \"{alt}\"");
                }
            }

            if (analysis.ChordId.SlashChordInfo != null)
            {
                Console.WriteLine("      slash_chord:");
                Console.WriteLine($"        info: \"{analysis.ChordId.SlashChordInfo}\"");
            }

            Console.WriteLine($"      key_function: \"{keyFunction}\"");
            Console.WriteLine(
                $"      naturally_occurring: {analysis.ChordId.IsNaturallyOccurring.ToString().ToLower()}");

            Console.WriteLine($"      intervals: [{string.Join(", ", analysis.IntervallicInfo.Intervals)}]");

            Console.WriteLine("    voicing:");
            Console.WriteLine($"      type: \"{(analysis.VoicingCharacteristics.IsOpenVoicing ? "open" : "closed")}\"");
            Console.WriteLine($"      span_semitones: {analysis.VoicingCharacteristics.IntervalSpread}");
            if (analysis.VoicingCharacteristics.IsRootless)
            {
                Console.WriteLine("      rootless: true");
            }

            if (analysis.VoicingCharacteristics.DropVoicing != null)
            {
                Console.WriteLine($"      drop_voicing: \"{analysis.VoicingCharacteristics.DropVoicing}\"");
            }

            if (analysis.VoicingCharacteristics.Features.Count > 0)
            {
                Console.WriteLine($"      features: [{string.Join(", ", analysis.VoicingCharacteristics.Features)}]");
            }

            if (analysis.ModeInfo != null)
            {
                Console.WriteLine("    mode:");
                Console.WriteLine($"      name: \"{analysis.ModeInfo.ModeName}\"");
                if (analysis.ModeInfo.FamilyName != null)
                {
                    Console.WriteLine($"      family: \"{analysis.ModeInfo.FamilyName}\"");
                }

                Console.WriteLine($"      degree: {analysis.ModeInfo.Degree}");
            }

            Console.WriteLine("    analysis:");
            Console.WriteLine($"      interval_class_vector: \"{icv}\"");
            Console.WriteLine($"      features: [{features}]");
            if (analysis.SymmetricalInfo != null)
            {
                Console.WriteLine("      symmetrical_scale:");
                Console.WriteLine($"        name: \"{analysis.SymmetricalInfo.ScaleName}\"");
            }

            // Equivalence information
            if (analysis.EquivalenceInfo != null)
            {
                Console.WriteLine("    equivalence:");
                Console.WriteLine($"      prime_form_id: \"{analysis.EquivalenceInfo.PrimeFormId}\"");
                Console.WriteLine($"      translation_offset: {analysis.EquivalenceInfo.TranslationOffset}");
            }

            // Physical layout
            Console.WriteLine("    physical_layout:");
            Console.WriteLine($"      fret_positions: [{string.Join(", ", analysis.PhysicalLayout.FretPositions)}]");
            Console.WriteLine($"      strings_used: [{string.Join(", ", analysis.PhysicalLayout.StringsUsed)}]");
            Console.WriteLine($"      muted_strings: [{string.Join(", ", analysis.PhysicalLayout.MutedStrings)}]");
            Console.WriteLine($"      open_strings: [{string.Join(", ", analysis.PhysicalLayout.OpenStrings)}]");
            Console.WriteLine("      fret_range:");
            Console.WriteLine($"        min: {analysis.PhysicalLayout.MinFret}");
            Console.WriteLine($"        max: {analysis.PhysicalLayout.MaxFret}");
            Console.WriteLine($"      hand_position: \"{analysis.PhysicalLayout.HandPosition}\"");

            // Playability
            Console.WriteLine("    playability:");
            Console.WriteLine($"      difficulty: \"{analysis.PlayabilityInfo.Difficulty}\"");
            Console.WriteLine($"      hand_stretch: {analysis.PlayabilityInfo.HandStretch}");
            Console.WriteLine($"      barre_required: {analysis.PlayabilityInfo.BarreRequired.ToString().ToLower()}");
            Console.WriteLine($"      minimum_fingers: {analysis.PlayabilityInfo.MinimumFingers}");
            if (analysis.PlayabilityInfo.CagedShape != null)
            {
                Console.WriteLine($"      caged_shape: \"{analysis.PlayabilityInfo.CagedShape}\"");
            }

            // Semantic tags
            if (analysis.SemanticTags.Length > 0)
            {
                Console.WriteLine(
                    $"    semantic_tags: [{string.Join(", ", analysis.SemanticTags.Select(t => $"\"{t}\""))}]");
            }
        }
    }

    /// <summary>
    ///     Parses command-line arguments to create filter criteria
    /// </summary>
    private static VoicingFilterCriteria ParseArguments(string[] args)
    {
        var criteria = new VoicingFilterCriteria();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLower();

            switch (arg)
            {
                // Chord type filters
                case "--triads":
                    criteria.ChordType = ChordTypeFilter.Triads;
                    break;
                case "--seventh":
                case "--7th":
                    criteria.ChordType = ChordTypeFilter.SeventhChords;
                    break;
                case "--extended":
                    criteria.ChordType = ChordTypeFilter.ExtendedChords;
                    break;
                case "--major":
                    criteria.ChordType = ChordTypeFilter.MajorChords;
                    break;
                case "--minor":
                    criteria.ChordType = ChordTypeFilter.MinorChords;
                    break;
                case "--dominant":
                    criteria.ChordType = ChordTypeFilter.DominantChords;
                    break;

                // Voicing type filters
                case "--drop2":
                    criteria.VoicingType = VoicingTypeFilter.Drop2;
                    break;
                case "--drop3":
                    criteria.VoicingType = VoicingTypeFilter.Drop3;
                    break;
                case "--rootless":
                    criteria.VoicingType = VoicingTypeFilter.Rootless;
                    break;
                case "--shell":
                    criteria.VoicingType = VoicingTypeFilter.ShellVoicings;
                    break;

                // Voicing characteristics
                case "--open":
                    criteria.Characteristics = VoicingCharacteristicFilter.OpenVoicingsOnly;
                    break;
                case "--closed":
                    criteria.Characteristics = VoicingCharacteristicFilter.ClosedVoicingsOnly;
                    break;

                // Key context
                case "--diatonic":
                    criteria.KeyContext = KeyContextFilter.DiatonicOnly;
                    break;
                case "--chromatic":
                    criteria.KeyContext = KeyContextFilter.ChromaticOnly;
                    break;
                case "--key-c":
                    criteria.KeyContext = KeyContextFilter.InKeyOfC;
                    break;
                case "--key-g":
                    criteria.KeyContext = KeyContextFilter.InKeyOfG;
                    break;

                // Fret range
                case "--open-position":
                    criteria.FretRange = FretRangeFilter.OpenPosition;
                    break;
                case "--middle-position":
                    criteria.FretRange = FretRangeFilter.MiddlePosition;
                    break;
                case "--upper-position":
                    criteria.FretRange = FretRangeFilter.UpperPosition;
                    break;

                // Note count
                case "--2-notes":
                    criteria.NoteCount = NoteCountFilter.TwoNotes;
                    break;
                case "--3-notes":
                    criteria.NoteCount = NoteCountFilter.ThreeNotes;
                    break;
                case "--4-notes":
                    criteria.NoteCount = NoteCountFilter.FourNotes;
                    break;

                // Max results
                case "--max":
                case "--limit":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out var maxResults))
                    {
                        criteria.MaxResults = maxResults;
                        i++; // Skip next argument
                    }

                    break;

                case "--help":
                case "-h":
                    DisplayHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        return criteria;
    }

    /// <summary>
    ///     Displays the active filter criteria
    /// </summary>
    private static void DisplayFilterCriteria(VoicingFilterCriteria criteria)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Active Filters:[/]");

        if (criteria.ChordType != null && criteria.ChordType != ChordTypeFilter.All)
        {
            AnsiConsole.MarkupLine($"  [dim]Chord Type:[/] {criteria.ChordType}");
        }

        if (criteria.VoicingType != null && criteria.VoicingType != VoicingTypeFilter.All)
        {
            AnsiConsole.MarkupLine($"  [dim]Voicing Type:[/] {criteria.VoicingType}");
        }

        if (criteria.Characteristics != null && criteria.Characteristics != VoicingCharacteristicFilter.All)
        {
            AnsiConsole.MarkupLine($"  [dim]Characteristics:[/] {criteria.Characteristics}");
        }

        if (criteria.KeyContext != null && criteria.KeyContext != KeyContextFilter.All)
        {
            AnsiConsole.MarkupLine($"  [dim]Key Context:[/] {criteria.KeyContext}");
        }

        if (criteria.FretRange != null && criteria.FretRange != FretRangeFilter.All)
        {
            AnsiConsole.MarkupLine($"  [dim]Fret Range:[/] {criteria.FretRange}");
        }

        if (criteria.NoteCount != null && criteria.NoteCount != NoteCountFilter.All)
        {
            AnsiConsole.MarkupLine($"  [dim]Note Count:[/] {criteria.NoteCount}");
        }

        AnsiConsole.MarkupLine($"  [dim]Max Results:[/] {criteria.MaxResults}");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    ///     Displays help information
    /// </summary>
    private static void DisplayHelp()
    {
        AnsiConsole.MarkupLine("[bold yellow]Fretboard Voicings CLI - Filter Options[/]\n");
        AnsiConsole.MarkupLine("[underline]Chord Type Filters:[/]");
        AnsiConsole.MarkupLine("  --triads          Show only triads");
        AnsiConsole.MarkupLine("  --seventh, --7th  Show only seventh chords");
        AnsiConsole.MarkupLine("  --extended        Show only extended chords (9th, 11th, 13th)");
        AnsiConsole.MarkupLine("  --major           Show only major chords");
        AnsiConsole.MarkupLine("  --minor           Show only minor chords");
        AnsiConsole.MarkupLine("  --dominant        Show only dominant chords\n");

        AnsiConsole.MarkupLine("[underline]Voicing Type Filters:[/]");
        AnsiConsole.MarkupLine("  --drop2           Show only Drop-2 voicings");
        AnsiConsole.MarkupLine("  --drop3           Show only Drop-3 voicings");
        AnsiConsole.MarkupLine("  --rootless        Show only rootless voicings");
        AnsiConsole.MarkupLine("  --shell           Show only shell voicings\n");

        AnsiConsole.MarkupLine("[underline]Voicing Characteristics:[/]");
        AnsiConsole.MarkupLine("  --open            Show only open voicings");
        AnsiConsole.MarkupLine("  --closed          Show only closed voicings\n");

        AnsiConsole.MarkupLine("[underline]Key Context:[/]");
        AnsiConsole.MarkupLine("  --diatonic        Show only diatonic voicings");
        AnsiConsole.MarkupLine("  --chromatic       Show only chromatic voicings");
        AnsiConsole.MarkupLine("  --key-c           Show voicings in key of C");
        AnsiConsole.MarkupLine("  --key-g           Show voicings in key of G\n");

        AnsiConsole.MarkupLine("[underline]Fret Range:[/]");
        AnsiConsole.MarkupLine("  --open-position   Show voicings in open position (frets 0-4)");
        AnsiConsole.MarkupLine("  --middle-position Show voicings in middle position (frets 5-12)");
        AnsiConsole.MarkupLine("  --upper-position  Show voicings in upper position (frets 12+)\n");

        AnsiConsole.MarkupLine("[underline]Note Count:[/]");
        AnsiConsole.MarkupLine("  --2-notes         Show 2-note voicings");
        AnsiConsole.MarkupLine("  --3-notes         Show 3-note voicings (triads)");
        AnsiConsole.MarkupLine("  --4-notes         Show 4-note voicings (seventh chords)\n");

        AnsiConsole.MarkupLine("[underline]Other Options:[/]");
        AnsiConsole.MarkupLine("  --max, --limit N  Maximum number of results to show (default: 50)");
        AnsiConsole.MarkupLine("  --help, -h        Show this help message\n");

        AnsiConsole.MarkupLine("[bold green]Examples:[/]");
        AnsiConsole.MarkupLine("  FretboardVoicingsCLI --drop2 --seventh --max 20");
        AnsiConsole.MarkupLine("  FretboardVoicingsCLI --triads --open-position --key-c");
        AnsiConsole.MarkupLine("  FretboardVoicingsCLI --rootless --dominant --diatonic");
    }

    #region Export Mode (JSONL)

    /// <summary>
    ///     Attempts to parse export mode options from command-line arguments.
    ///     Returns true if --export flag is present.
    /// </summary>
    private static bool TryParseExportOptions(string[] args, out ExportOptions options)
    {
        options = default!;
        var hasExport = args.Any(a => a.Equals("--export", StringComparison.OrdinalIgnoreCase));
        if (!hasExport) return false;

        var showHelp = args.Any(a => a.Equals("--export-help", StringComparison.OrdinalIgnoreCase));
        int? maxVoicings = null;
        var tuning = TuningPreset.Guitar;

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--export-max", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var max)) maxVoicings = max;
            }
            else if (args[i].Equals("--tuning", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var raw = args[i + 1].Trim().ToLowerInvariant();
                tuning = raw switch
                {
                    "guitar" => TuningPreset.Guitar,
                    "bass" => TuningPreset.Bass,
                    "ukulele" or "uke" => TuningPreset.Ukulele,
                    _ => throw new ArgumentException(
                        $"Unknown --tuning value '{args[i + 1]}'. Expected guitar|bass|ukulele.")
                };
            }
        }

        options = new ExportOptions(maxVoicings, showHelp, tuning);
        return true;
    }

    /// <summary>
    ///     Runs export mode: streams voicings to stdout as JSONL (one JSON object per line).
    ///     Skips decomposition, analysis, and filtering for performance.
    /// </summary>
    private static async Task<int> RunExportAsync(ExportOptions options)
    {
        // Select tuning + fret count by preset. Fret counts are typical for each
        // instrument: 24 for electric guitar, 21 for bass, 15 for ukulele.
        var (tuning, fretCount) = options.Tuning switch
        {
            TuningPreset.Guitar => (GA.Domain.Core.Instruments.Tuning.Default, 24),
            TuningPreset.Bass => (GA.Domain.Core.Instruments.Tuning.Bass, 21),
            TuningPreset.Ukulele => (GA.Domain.Core.Instruments.Tuning.Ukulele, 15),
            _ => (GA.Domain.Core.Instruments.Tuning.Default, 24)
        };
        var fretboard = new Fretboard(tuning, fretCount);
        const int windowSize = 4;
        const int minPlayedNotes = 2;

        var count = 0;
        var maxVoicings = options.MaxVoicings ?? int.MaxValue;

        await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(
                           fretboard, windowSize, minPlayedNotes, parallel: true))
        {
            if (count >= maxVoicings) break;

            var dto = new
            {
                instrument = options.Tuning.ToString().ToLowerInvariant(),
                stringCount = fretboard.StringCount,
                diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions),
                frets = voicing.Positions.Select(p => p switch
                {
                    Position.Muted => "x",
                    Position.Played played => played.Location.Fret.Value.ToString(),
                    _ => "?"
                }).ToArray(),
                midiNotes = voicing.Notes.Select(n => (int)n).ToArray(),
                minFret = VoicingExtensions.GetMinFret(voicing.Positions),
                maxFret = VoicingExtensions.GetMaxFret(voicing.Positions),
                fretSpan = VoicingExtensions.GetFretSpan(voicing.Positions)
            };

            Console.WriteLine(JsonSerializer.Serialize(dto));
            count++;
        }

        return 0;
    }

    /// <summary>
    ///     Displays export mode help to stderr (keeping stdout clean for JSONL).
    /// </summary>
    private static void DisplayExportHelp()
    {
        Console.Error.WriteLine("Fretboard Voicings CLI - Export Mode (JSONL)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  FretboardVoicingsCLI --export [--export-max N]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --export              Enable export mode (JSONL to stdout)");
        Console.Error.WriteLine("  --export-max N        Limit output to N voicings");
        Console.Error.WriteLine("  --tuning P            Tuning preset: guitar (default, 24 frets),");
        Console.Error.WriteLine("                        bass (21 frets), ukulele (15 frets)");
        Console.Error.WriteLine("  --export-help         Show this help message");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Output Format (one JSON object per line):");
        Console.Error.WriteLine("  {\"diagram\":\"x-3-2-0-1-0\",\"frets\":[\"x\",\"3\",\"2\",\"0\",\"1\",\"0\"],");
        Console.Error.WriteLine("   \"midiNotes\":[48,52,55,60,64],\"minFret\":1,\"maxFret\":3,\"fretSpan\":2}");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  FretboardVoicingsCLI --export > voicings.jsonl");
        Console.Error.WriteLine("  FretboardVoicingsCLI --export --export-max 1000 > sample.jsonl");
    }

    #endregion

    #region Export Embeddings Mode (OPTIC-K v4 binary)

    /// <summary>
    ///     Attempts to parse export-embeddings mode options from command-line arguments.
    ///     Returns true if --export-embeddings flag is present.
    /// </summary>
    private static bool TryParseExportEmbeddingsOptions(string[] args, out ExportEmbeddingsOptions options)
    {
        options = default!;
        var hasFlag = args.Any(a => a.Equals("--export-embeddings", StringComparison.OrdinalIgnoreCase));
        if (!hasFlag) return false;

        var showHelp = args.Any(a => a.Equals("--export-embeddings-help", StringComparison.OrdinalIgnoreCase));
        var noDedup = args.Any(a => a.Equals("--no-dedup", StringComparison.OrdinalIgnoreCase));
        int? maxVoicings = null;
        TuningPreset? tuning = null;
        var outputPath = Path.Combine("state", "voicings", "optick.index");

        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--export-max", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                if (int.TryParse(args[i + 1], out var max)) maxVoicings = max;
            }
            else if (args[i].Equals("--tuning", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                var raw = args[i + 1].Trim().ToLowerInvariant();
                tuning = raw switch
                {
                    "guitar" => TuningPreset.Guitar,
                    "bass" => TuningPreset.Bass,
                    "ukulele" or "uke" => TuningPreset.Ukulele,
                    _ => throw new ArgumentException(
                        $"Unknown --tuning value '{args[i + 1]}'. Expected guitar|bass|ukulele.")
                };
            }
            else if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outputPath = args[i + 1];
            }
        }

        options = new ExportEmbeddingsOptions(maxVoicings, showHelp, outputPath, tuning, noDedup);
        return true;
    }

    /// <summary>
    ///     Runs export-embeddings mode: generates OPTIC-K v4 binary index with 112-dim compact embeddings
    ///     for all voicings across one or all instruments.
    /// </summary>
    private static async Task<int> RunExportEmbeddingsAsync(ExportEmbeddingsOptions options)
    {
        var sw = Stopwatch.StartNew();

        // Determine which instruments to process
        var presets = options.Tuning.HasValue
            ? [options.Tuning.Value]
            : new[] { TuningPreset.Guitar, TuningPreset.Bass, TuningPreset.Ukulele };

        Console.Error.WriteLine($"OPTIC-K v4 embedding export — instruments: {string.Join(", ", presets.Select(p => p.ToString().ToLowerInvariant()))}");
        if (options.MaxVoicings.HasValue)
            Console.Error.WriteLine($"  --export-max {options.MaxVoicings.Value} (per instrument)");
        Console.Error.WriteLine($"  output: {options.OutputPath}");
        Console.Error.WriteLine($"  dedup: {(options.NoDedup ? "OFF (--no-dedup)" : "ON (structural prime forms)")}");
        Console.Error.WriteLine();

        // Create the embedding generator once
        var generator = EmbeddingServiceProvider.CreateEmbeddingGenerator();

        var allEntries = new List<VoicingEntry>();
        var totalCount = 0;
        var totalRaw = 0;

        foreach (var preset in presets)
        {
            var instrumentName = preset.ToString().ToLowerInvariant();
            var (tuning, fretCount) = preset switch
            {
                TuningPreset.Guitar => (Tuning.Default, 24),
                TuningPreset.Bass => (Tuning.Bass, 21),
                TuningPreset.Ukulele => (Tuning.Ukulele, 15),
                _ => (Tuning.Default, 24)
            };
            var fretboard = new Fretboard(tuning, fretCount);

            const int windowSize = 4;
            const int minPlayedNotes = 2;
            var maxVoicings = options.MaxVoicings ?? int.MaxValue;

            // === Phase 1: Generate + dedup ===
            //
            // Collect all voicings first. If dedup is enabled we group by a structural
            // key (relative-fret signature + pitch-class bitmask) and keep the single
            // cheapest-to-play representative per group. The expensive embedding pass
            // then runs only on survivors.
            //
            // Why not VoicingDecomposer.DecomposeVoicings? That API hard-codes string
            // count = 6 inside the internal VariationsWithRepetitions lookup, which
            // throws on 4-string bass/ukulele. VoicingDecomposer.GetRelativeFrets
            // (the helper it builds on) is string-count-independent, so we use it
            // directly here and pair it with a pitch-class bitmask for musical identity.

            var phase1Sw = Stopwatch.StartNew();
            Console.Error.Write($"  {instrumentName}: generating voicings...");

            var rawCount = 0;
            var survivors = new Dictionary<(string, int), DedupCandidate>();
            var flatList = new List<Voicing>(); // used when dedup is off

            await foreach (var voicing in VoicingGenerator.GenerateAllVoicingsAsync(
                               fretboard, windowSize, minPlayedNotes, parallel: true))
            {
                if (rawCount >= maxVoicings) break;
                rawCount++;

                if (options.NoDedup)
                {
                    flatList.Add(voicing);
                }
                else
                {
                    var relFrets = VoicingDecomposer.GetRelativeFrets(voicing.Positions);
                    if (relFrets == null) continue; // malformed voicing, skip

                    // Structural key: fret shape (string-count-independent) + pitch-class bitmask.
                    var shape = BuildRelativeFretSignature(relFrets);
                    var pcMask = BuildPitchClassMask(voicing.Notes);
                    var key = (shape, pcMask);

                    // Cheap playability cost used only for picking a representative — we
                    // do NOT run the full VoicingAnalyzer here (that's the point of dedup).
                    var cost = CheapPlayabilityCost(voicing.Positions);

                    if (!survivors.TryGetValue(key, out var existing) || cost < existing.Cost)
                    {
                        survivors[key] = new DedupCandidate(voicing, cost);
                    }
                }

                if (rawCount % 10000 == 0)
                    Console.Error.Write($"\r  {instrumentName}: generating voicings... {rawCount:N0}");
            }

            var selected = options.NoDedup
                ? flatList
                : survivors.Values.Select(c => c.Voicing).ToList();
            phase1Sw.Stop();

            if (options.NoDedup)
            {
                Console.Error.WriteLine($"\r  {instrumentName}: {rawCount:N0} raw voicings (dedup off) in {phase1Sw.Elapsed.TotalSeconds:N1}s");
            }
            else
            {
                var pct = rawCount > 0 ? 100.0 * selected.Count / rawCount : 0.0;
                Console.Error.WriteLine(
                    $"\r  {instrumentName}: {rawCount:N0} raw → {selected.Count:N0} unique ({pct:N1}%) in {phase1Sw.Elapsed.TotalSeconds:N1}s");
            }

            // === Phase 2: Expensive analysis + embedding on survivors only ===
            var phase2Sw = Stopwatch.StartNew();
            var instrumentCount = 0;
            Console.Error.Write($"  {instrumentName}: embedding {selected.Count:N0} survivors...");

            foreach (var voicing in selected)
            {
                // Analyze the voicing
                var analysis = VoicingAnalyzer.Analyze(voicing);

                // Use the pitch-class prime form as the canonical prime-form id. This is
                // cheaper and more stable than re-deriving a RelativeFretVector prime form
                // for non-6-string instruments, and it's what downstream consumers key on.
                var primeFormId = analysis.PitchClassSet.PrimeForm?.Id.Value.ToString();

                // Create RAG document for embedding generation
                var doc = VoicingDocumentFactory.FromAnalysis(
                    voicing,
                    analysis,
                    tuningId: instrumentName,
                    primeFormId: primeFormId);

                // Generate the 228-dim embedding
                var embedding = await generator.GenerateEmbeddingAsync(doc);

                // Build the entry for the binary index
                var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
                var midiNotes = voicing.Notes.Select(n => n.Value).ToArray();
                var chordName = analysis.ChordId.ChordName;

                allEntries.Add(new VoicingEntry(embedding, diagram, instrumentName, midiNotes, chordName));
                instrumentCount++;

                // Progress indicator every 1000 voicings
                if (instrumentCount % 1000 == 0)
                    Console.Error.Write($"\r  {instrumentName}: {instrumentCount:N0}/{selected.Count:N0} embedded...");
            }

            phase2Sw.Stop();
            Console.Error.WriteLine(
                $"\r  {instrumentName}: embedded {instrumentCount:N0} in {phase2Sw.Elapsed.TotalSeconds:N1}s");
            totalCount += instrumentCount;
            totalRaw += rawCount;
        }

        // Ensure output directory exists
        var outputDir = Path.GetDirectoryName(options.OutputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        // Write the binary index
        Console.Error.Write($"  Writing OPTIC-K v4 index ({totalCount:N0} entries)...");
        using (var writer = new OptickIndexWriter(options.OutputPath))
        {
            writer.WriteIndex(allEntries);
        }

        var fileInfo = new FileInfo(options.OutputPath);
        sw.Stop();

        Console.Error.WriteLine($" done.");
        Console.Error.WriteLine();
        if (!options.NoDedup && totalRaw > 0)
        {
            var totalPct = 100.0 * totalCount / totalRaw;
            Console.Error.WriteLine(
                $"  Total: {totalRaw:N0} raw → {totalCount:N0} unique ({totalPct:N1}%), "
                + $"{fileInfo.Length / 1024.0 / 1024.0:N1} MB, {sw.Elapsed.TotalSeconds:N1}s");
        }
        else
        {
            Console.Error.WriteLine(
                $"  Total: {totalCount:N0} voicings, {fileInfo.Length / 1024.0 / 1024.0:N1} MB, {sw.Elapsed.TotalSeconds:N1}s");
        }
        Console.Error.WriteLine($"  Output: {Path.GetFullPath(options.OutputPath)}");

        return 0;
    }

    /// <summary>
    ///     Internal record used during dedup to track the cheapest-to-play representative
    ///     of a structural voicing group.
    /// </summary>
    private readonly record struct DedupCandidate(Voicing Voicing, int Cost);

    /// <summary>
    ///     Builds a compact string key from a relative-fret vector (string-count-independent).
    ///     Muted strings encode as "x"; played/open frets encode as integers. The result is
    ///     intended purely as a dictionary key.
    /// </summary>
    private static string BuildRelativeFretSignature(RelativeFret[] relativeFrets)
    {
        var sb = new StringBuilder(relativeFrets.Length * 3);
        for (var i = 0; i < relativeFrets.Length; i++)
        {
            if (i > 0) sb.Append('-');
            var rf = relativeFrets[i];
            // RelativeFret.Min is used by GetRelativeFrets to encode muted strings.
            if (rf.Equals(RelativeFret.Min)) sb.Append('x');
            else sb.Append(rf.Value);
        }
        return sb.ToString();
    }

    /// <summary>
    ///     Builds a 12-bit pitch-class bitmask (bit n set iff pitch class n is present).
    ///     Cheap alternative to constructing a full PitchClassSet; keeps dedup CPU-light.
    /// </summary>
    private static int BuildPitchClassMask(MidiNote[] notes)
    {
        var mask = 0;
        foreach (var n in notes)
        {
            mask |= 1 << (n.Value % 12);
        }
        return mask;
    }

    /// <summary>
    ///     Cheap playability cost used solely as a tiebreaker when picking a representative
    ///     voicing from a dedup group. Prefers lower fret position, then more open/played
    ///     strings (fewer muted strings), then tighter fret span. Does NOT call the full
    ///     VoicingPhysicalAnalyzer — that would defeat the purpose of dedup.
    /// </summary>
    private static int CheapPlayabilityCost(Position[] positions)
    {
        var minPlayedFret = int.MaxValue;
        var maxPlayedFret = 0;
        var mutedCount = 0;

        foreach (var p in positions)
        {
            switch (p)
            {
                case Position.Muted:
                    mutedCount++;
                    break;
                case Position.Played played:
                    var fret = played.Location.Fret.Value;
                    if (fret > 0)
                    {
                        if (fret < minPlayedFret) minPlayedFret = fret;
                        if (fret > maxPlayedFret) maxPlayedFret = fret;
                    }
                    break;
            }
        }

        if (minPlayedFret == int.MaxValue) minPlayedFret = 0; // all-open or all-muted

        // Weighted sum: fret position dominates, then mutes, then span.
        return minPlayedFret * 100 + mutedCount * 10 + (maxPlayedFret - minPlayedFret);
    }

    /// <summary>
    ///     Displays export-embeddings mode help to stderr.
    /// </summary>
    private static void DisplayExportEmbeddingsHelp()
    {
        Console.Error.WriteLine("Fretboard Voicings CLI - Export Embeddings Mode (OPTIC-K v4 binary)");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  FretboardVoicingsCLI --export-embeddings [options]");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Options:");
        Console.Error.WriteLine("  --export-embeddings         Enable embedding export mode");
        Console.Error.WriteLine("  --output PATH               Output file path (default: state/voicings/optick.index)");
        Console.Error.WriteLine("  --tuning P                  Single instrument: guitar, bass, or ukulele");
        Console.Error.WriteLine("                              (default: all three instruments)");
        Console.Error.WriteLine("  --export-max N              Limit voicings per instrument (for testing)");
        Console.Error.WriteLine("  --no-dedup                  Disable structural dedup (default: ON).");
        Console.Error.WriteLine("                              With dedup ON, voicings are grouped by");
        Console.Error.WriteLine("                              (relative-fret shape, pitch-class bitmask)");
        Console.Error.WriteLine("                              and only the cheapest-to-play survivor per");
        Console.Error.WriteLine("                              group is embedded.");
        Console.Error.WriteLine("  --export-embeddings-help    Show this help message");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Output Format:");
        Console.Error.WriteLine("  OPTIC-K v4 binary index with 112-dim compact embeddings per voicing,");
        Console.Error.WriteLine("  sqrt-weight scaled and L2-normalized, with msgpack metadata.");
        Console.Error.WriteLine("  Instruments are sorted: guitar, bass, ukulele.");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  FretboardVoicingsCLI --export-embeddings");
        Console.Error.WriteLine("  FretboardVoicingsCLI --export-embeddings --tuning guitar --export-max 500");
        Console.Error.WriteLine("  FretboardVoicingsCLI --export-embeddings --output my-index.optk");
    }

    #endregion
}
