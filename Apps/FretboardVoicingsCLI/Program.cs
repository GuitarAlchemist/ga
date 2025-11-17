namespace FretboardVoicingsCLI;

using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Voicings.Analysis;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard.Voicings.Filtering;
using GA.Business.Core.Fretboard.Voicings.Generation;
using Spectre.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        AnsiConsole.Write(new FigletText("Fretboard Voicings").Color(Color.Green));
        AnsiConsole.MarkupLine("[dim]Finding all possible voicings on a 6-string guitar in standard tuning[/]\n");

        // Parse command-line arguments for filtering
        var filterCriteria = ParseArguments(args);

        // Create fretboard with standard tuning (6 strings, 24 frets, E2 A2 D3 G3 B3 E4)
        var fretboard = Fretboard.Default;

        // Create collection of all relative fret vectors (5-fret extent, 6 strings)
        var vectorCollection = new RelativeFretVectorCollection(strCount: 6, fretExtent: 5);

        AnsiConsole.MarkupLine($"[dim]Using optimized voicing generation with parallel processing[/]");
        AnsiConsole.MarkupLine($"[dim]Vector collection: {vectorCollection.Count:N0} total vectors, {vectorCollection.PrimeForms.Count:N0} prime forms[/]\n");

        const int windowSize = 4; // 5 frets: [start, start+4]
        const int maxStartFret = 22 - windowSize; // 18

        var totalStartTime = DateTime.Now;

        // Generate all voicings using the core library (with parallel processing and deduplication)
        AnsiConsole.MarkupLine($"[dim]Generating all voicings across {maxStartFret + 1} windows using {Environment.ProcessorCount} cores[/]\n");

        var allVoicings = VoicingGenerator.GenerateAllVoicings(
            fretboard,
            windowSize: windowSize,
            minPlayedNotes: 2,
            parallel: true);

        AnsiConsole.MarkupLine($"[green]Generated {allVoicings.Count:N0} unique voicings[/]\n");

        // Show first 20 voicings to verify ordering starts at fret 0
        AnsiConsole.MarkupLine($"[yellow]First 20 voicings (to verify ordering):[/]\n");
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

        AnsiConsole.MarkupLine($"\n[dim]Decomposition: {allDecomposed.Count:N0} voicings → {primeFormsOnly.Count:N0} prime forms ({decompositionElapsed.TotalMilliseconds:N0}ms)[/]\n");

        allDecomposed = primeFormsOnly;

        var totalElapsed = DateTime.Now - totalStartTime;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[bold green]Total: {allDecomposed.Count:N0} unique prime form voicings across entire fretboard[/]");
        AnsiConsole.MarkupLine($"[dim]Total time: {totalElapsed.TotalSeconds:N1}s ({totalElapsed.TotalMilliseconds:N0}ms)[/]");

        // Display filter criteria
        DisplayFilterCriteria(filterCriteria);

        // Apply filters and show matching voicings
        AnsiConsole.MarkupLine($"[dim]Applying filters and showing up to {filterCriteria.MaxResults} matching voicings...[/]");

        var filteringStart = DateTime.Now;
        var primeFormVoicings = allDecomposed.Select(d => d.Voicing);
        var filteredVoicings = VoicingFilters.ApplyFilters(primeFormVoicings, filterCriteria);

        DisplayFilteredVoicings(filteredVoicings);

        var filteringElapsed = DateTime.Now - filteringStart;
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[dim]Filtering time: {filteringElapsed.TotalSeconds:F2}s ({filteringElapsed.TotalMilliseconds:N0}ms)[/]");

        AnsiConsole.MarkupLine("\n[green]Press any key to exit...[/]");
        Console.ReadKey();
    }

    /// <summary>
    /// Displays filtered voicings in YAML format with comprehensive musical analysis
    /// </summary>
    private static void DisplayFilteredVoicings(IEnumerable<(Voicing Voicing, MusicalVoicingAnalysis Analysis)> filteredVoicings)
    {
        var samples = filteredVoicings.ToList();

        Console.WriteLine("voicings:");

        foreach (var (voicing, analysis) in samples)
        {
            var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);

            // MIDI note numbers
            var midiNotes = string.Join(", ", analysis.MidiNotes.Select(n => n.Value.ToString()));

            // Note names with octave
            var noteNames = string.Join(", ", analysis.MidiNotes.Select(n => $"\"{n.ToSharpNote()}\""));

            // Pitch class set
            var pitchClasses = analysis.PitchClassSet.ToString();

            // Chord name
            var chordName = analysis.ChordId.ChordName ?? "Unknown";
            if (analysis.SymmetricalInfo != null)
            {
                chordName = $"{analysis.SymmetricalInfo.ScaleName} ({string.Join("/", analysis.SymmetricalInfo.PossibleRoots)})";
            }

            // Key and function
            var keyFunction = analysis.ChordId.FunctionalDescription ?? "Atonal";
            if (!analysis.ChordId.IsNaturallyOccurring && analysis.ChordId.ClosestKey != null)
            {
                keyFunction += " (chromatic)";
            }

            // Interval class vector
            var icv = analysis.IntervallicInfo.IntervalClassVector;

            // Features
            var features = string.Join(", ", analysis.IntervallicInfo.Features);
            if (string.IsNullOrEmpty(features))
            {
                features = "none";
            }

            // Output YAML format
            Console.WriteLine($"  - diagram: \"{diagram}\"");
            Console.WriteLine($"    midi_notes: [{midiNotes}]");
            Console.WriteLine($"    notes: [{noteNames}]");
            Console.WriteLine($"    pitch_classes: \"{pitchClasses}\"");
            Console.WriteLine($"    chord:");
            Console.WriteLine($"      name: \"{chordName}\"");
            if (analysis.ChordId.AlternateName != null && analysis.ChordId.AlternateName != chordName)
            {
                Console.WriteLine($"      alternate_name: \"{analysis.ChordId.AlternateName}\"");
            }
            if (analysis.ChordId.SlashChordInfo != null)
            {
                Console.WriteLine($"      slash_chord:");
                Console.WriteLine($"        notation: \"{analysis.ChordId.SlashChordInfo.Notation}\"");
                Console.WriteLine($"        type: \"{analysis.ChordId.SlashChordInfo.Type}\"");
                Console.WriteLine($"        is_common_inversion: {analysis.ChordId.SlashChordInfo.IsCommonInversion.ToString().ToLower()}");
            }
            Console.WriteLine($"      key_function: \"{keyFunction}\"");
            Console.WriteLine($"      naturally_occurring: {analysis.ChordId.IsNaturallyOccurring.ToString().ToLower()}");
            if (analysis.ChordId.RomanNumeral != null)
            {
                Console.WriteLine($"      roman_numeral: \"{analysis.ChordId.RomanNumeral}\"");
            }
            if (analysis.ChordId.Intervals != null)
            {
                Console.WriteLine($"      intervals:");
                Console.WriteLine($"        theoretical: [{string.Join(", ", analysis.ChordId.Intervals.Theoretical.Select(i => $"\"{i}\""))}]");
                Console.WriteLine($"        actual: [{string.Join(", ", analysis.ChordId.Intervals.Actual.Select(i => $"\"{i}\""))}]");
            }
            Console.WriteLine($"    voicing:");
            Console.WriteLine($"      type: \"{(analysis.VoicingCharacteristics.IsOpenVoicing ? "open" : "closed")}\"");
            Console.WriteLine($"      span_semitones: {analysis.VoicingCharacteristics.Span}");
            if (analysis.VoicingCharacteristics.IsRootless)
            {
                Console.WriteLine($"      rootless: true");
            }
            if (analysis.VoicingCharacteristics.DropVoicing != null)
            {
                Console.WriteLine($"      drop_voicing: \"{analysis.VoicingCharacteristics.DropVoicing}\"");
            }
            if (analysis.VoicingCharacteristics.Features.Count > 0)
            {
                Console.WriteLine($"      features: [{string.Join(", ", analysis.VoicingCharacteristics.Features.Select(f => $"\"{f}\""))}]");
            }
            if (analysis.ModeInfo != null)
            {
                Console.WriteLine($"    mode:");
                Console.WriteLine($"      name: \"{analysis.ModeInfo.ModeName}\"");
                if (analysis.ModeInfo.FamilyName != null)
                {
                    Console.WriteLine($"      family: \"{analysis.ModeInfo.FamilyName}\"");
                }
                Console.WriteLine($"      degree: {analysis.ModeInfo.DegreeInFamily}");
                Console.WriteLine($"      note_count: {analysis.ModeInfo.NoteCount}");
            }
            if (analysis.ChromaticNotes != null && analysis.ChromaticNotes.Count > 0)
            {
                Console.WriteLine($"    chromatic_notes: [{string.Join(", ", analysis.ChromaticNotes.Select(n => $"\"{n}\""))}]");
            }
            Console.WriteLine($"    analysis:");
            Console.WriteLine($"      interval_class_vector: \"{icv}\"");
            Console.WriteLine($"      features: [{string.Join(", ", analysis.IntervallicInfo.Features.Select(f => $"\"{f}\""))}]");
            if (analysis.SymmetricalInfo != null)
            {
                Console.WriteLine($"      symmetrical_scale:");
                Console.WriteLine($"        name: \"{analysis.SymmetricalInfo.ScaleName}\"");
                Console.WriteLine($"        possible_roots: [{string.Join(", ", analysis.SymmetricalInfo.PossibleRoots.Select(r => $"\"{r}\""))}]");
            }

            // Equivalence information
            if (analysis.EquivalenceInfo != null)
            {
                Console.WriteLine($"    equivalence:");
                Console.WriteLine($"      prime_form_id: \"{analysis.EquivalenceInfo.PrimeFormId}\"");
                Console.WriteLine($"      is_prime_form: {analysis.EquivalenceInfo.IsPrimeForm.ToString().ToLower()}");
                Console.WriteLine($"      translation_offset: {analysis.EquivalenceInfo.TranslationOffset}");
                Console.WriteLine($"      equivalence_class_size: {analysis.EquivalenceInfo.EquivalenceClassSize}");
            }

            // Physical layout
            Console.WriteLine($"    physical_layout:");
            Console.WriteLine($"      fret_positions: [{string.Join(", ", analysis.PhysicalLayout.FretPositions)}]");
            Console.WriteLine($"      strings_used: [{string.Join(", ", analysis.PhysicalLayout.StringsUsed)}]");
            Console.WriteLine($"      muted_strings: [{string.Join(", ", analysis.PhysicalLayout.MutedStrings)}]");
            Console.WriteLine($"      open_strings: [{string.Join(", ", analysis.PhysicalLayout.OpenStrings)}]");
            Console.WriteLine($"      fret_range:");
            Console.WriteLine($"        min: {analysis.PhysicalLayout.MinFret}");
            Console.WriteLine($"        max: {analysis.PhysicalLayout.MaxFret}");
            Console.WriteLine($"      hand_position: \"{analysis.PhysicalLayout.HandPosition}\"");

            // Playability
            Console.WriteLine($"    playability:");
            Console.WriteLine($"      difficulty: \"{analysis.PlayabilityInfo.Difficulty}\"");
            Console.WriteLine($"      hand_stretch: {analysis.PlayabilityInfo.HandStretch}");
            Console.WriteLine($"      barre_required: {analysis.PlayabilityInfo.BarreRequired.ToString().ToLower()}");
            Console.WriteLine($"      minimum_fingers: {analysis.PlayabilityInfo.MinimumFingers}");
            if (analysis.PlayabilityInfo.CagedShape != null)
            {
                Console.WriteLine($"      caged_shape: \"{analysis.PlayabilityInfo.CagedShape}\"");
            }

            // Semantic tags
            if (analysis.SemanticTags.Count > 0)
            {
                Console.WriteLine($"    semantic_tags: [{string.Join(", ", analysis.SemanticTags.Select(t => $"\"{t}\""))}]");
            }
        }
    }

    /// <summary>
    /// Parses command-line arguments to create filter criteria
    /// </summary>
    private static VoicingFilterCriteria ParseArguments(string[] args)
    {
        var criteria = new VoicingFilterCriteria();

        for (int i = 0; i < args.Length; i++)
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
    /// Displays the active filter criteria
    /// </summary>
    private static void DisplayFilterCriteria(VoicingFilterCriteria criteria)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[yellow]Active Filters:[/]");

        if (criteria.ChordType != null && criteria.ChordType != ChordTypeFilter.All)
            AnsiConsole.MarkupLine($"  [dim]Chord Type:[/] {criteria.ChordType}");

        if (criteria.VoicingType != null && criteria.VoicingType != VoicingTypeFilter.All)
            AnsiConsole.MarkupLine($"  [dim]Voicing Type:[/] {criteria.VoicingType}");

        if (criteria.Characteristics != null && criteria.Characteristics != VoicingCharacteristicFilter.All)
            AnsiConsole.MarkupLine($"  [dim]Characteristics:[/] {criteria.Characteristics}");

        if (criteria.KeyContext != null && criteria.KeyContext != KeyContextFilter.All)
            AnsiConsole.MarkupLine($"  [dim]Key Context:[/] {criteria.KeyContext}");

        if (criteria.FretRange != null && criteria.FretRange != FretRangeFilter.All)
            AnsiConsole.MarkupLine($"  [dim]Fret Range:[/] {criteria.FretRange}");

        if (criteria.NoteCount != null && criteria.NoteCount != NoteCountFilter.All)
            AnsiConsole.MarkupLine($"  [dim]Note Count:[/] {criteria.NoteCount}");

        AnsiConsole.MarkupLine($"  [dim]Max Results:[/] {criteria.MaxResults}");
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Displays help information
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
}

