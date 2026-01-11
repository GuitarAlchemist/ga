namespace ChordNamingDemo;

using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Chords.Analysis.Atonal;
using GA.Business.Core.Intervals;
using GA.Business.Core.Intervals.Primitives;
using Spectre.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Chord Naming Demo")
                .LeftJustified()
                .Color(Color.Green));

        AnsiConsole.MarkupLine("[bold]Guitar Alchemist Enhanced Chord Naming System[/]\n");

        DemonstrateBasicChordExtensions();
        DemonstrateSlashChords();
        DemonstrateQuartalChords();
        DemonstrateAtonalAnalysis();
        DemonstrateHybridAnalysis();
        DemonstrateKeyAwareNaming();
        DemonstrateProgressionAnalysis();
        DemonstrateComprehensiveNaming();

        AnsiConsole.MarkupLine("\n[green]Demo completed![/]");

        // Only try to read key if console is available
        try
        {
            if (!Console.IsInputRedirected && Environment.UserInteractive)
            {
                AnsiConsole.MarkupLine("[dim]Press any key to exit...[/]");
                Console.ReadKey();
            }
        }
        catch (InvalidOperationException)
        {
            // Console input not available, just continue
        }
    }

    private static void DemonstrateBasicChordExtensions()
    {
        AnsiConsole.MarkupLine("[bold blue]1. Basic Chord Extensions[/]");

        var table = new Table();
        table.AddColumn("Root");
        table.AddColumn("Quality");
        table.AddColumn("Extension");
        table.AddColumn("Generated Name");
        table.Border(TableBorder.Rounded);

        // Test various chord types
        var testCases = new[]
        {
            (PitchClass.FromValue(0), ChordQuality.Major, ChordExtension.Triad, "C"),
            (PitchClass.FromValue(0), ChordQuality.Minor, ChordExtension.Triad, "Cm"),
            (PitchClass.FromValue(0), ChordQuality.Major, ChordExtension.Seventh, "Cmaj7"),
            (PitchClass.FromValue(0), ChordQuality.Minor, ChordExtension.Seventh, "Cm7"),
            (PitchClass.FromValue(0), ChordQuality.Major, ChordExtension.Ninth, "Cmaj9"),
            (PitchClass.FromValue(0), ChordQuality.Minor, ChordExtension.Ninth, "Cm9"),
            (PitchClass.FromValue(0), ChordQuality.Suspended, ChordExtension.Sus4, "Csus4"),
            (PitchClass.FromValue(0), ChordQuality.Suspended, ChordExtension.Sus2, "Csus2"),
            (PitchClass.FromValue(0), ChordQuality.Major, ChordExtension.Add9, "Cadd9"),
            (PitchClass.FromValue(0), ChordQuality.Major, ChordExtension.SixNine, "C6/9")
        };

        foreach (var (root, quality, extension, expected) in testCases)
        {
            var generated = BasicChordExtensionsService.GenerateChordName(root, quality, extension);
            var color = generated == expected ? "green" : "red";

            table.AddRow(
                GetNoteName(root),
                quality.ToString(),
                extension.ToString(),
                $"[{color}]{generated}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateSlashChords()
    {
        AnsiConsole.MarkupLine("[bold yellow]2. Slash Chord Naming[/]");

        // Create a simple major triad template for demonstration
        var majorTriad = CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Triad);

        var table = new Table();
        table.AddColumn("Chord");
        table.AddColumn("Bass Note");
        table.AddColumn("Generated Name");
        table.AddColumn("Type");
        table.Border(TableBorder.Rounded);

        var slashChordTests = new[]
        {
            (PitchClass.FromValue(0), PitchClass.FromValue(4), "First Inversion"), // C/E
            (PitchClass.FromValue(0), PitchClass.FromValue(7), "Second Inversion"), // C/G
            (PitchClass.FromValue(0), PitchClass.FromValue(5), "Slash Chord"), // C/F
            (PitchClass.FromValue(0), PitchClass.FromValue(2), "Slash Chord"), // C/D
            (PitchClass.FromValue(9), PitchClass.FromValue(0), "Relative Major Bass") // A/C
        };

        foreach (var (root, bass, type) in slashChordTests)
        {
            var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(majorTriad, root, bass);

            table.AddRow(
                GetNoteName(root),
                GetNoteName(bass),
                $"[cyan]{comprehensive.SlashChord ?? comprehensive.Primary}[/]",
                type);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateQuartalChords()
    {
        AnsiConsole.MarkupLine("[bold magenta]3. Quartal Chord Naming[/]");

        // Create a quartal chord template for demonstration
        var quartalChord =
            CreateSimpleChordTemplate(ChordQuality.Suspended, ChordExtension.Sus4, ChordStackingType.Quartal);

        var table = new Table();
        table.AddColumn("Root");
        table.AddColumn("Generated Name");
        table.AddColumn("Description");
        table.Border(TableBorder.Rounded);

        var quartalTests = new[]
        {
            PitchClass.FromValue(0), // C
            PitchClass.FromValue(2), // D
            PitchClass.FromValue(5), // F
            PitchClass.FromValue(7) // G
        };

        foreach (var root in quartalTests)
        {
            var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(quartalChord, root);

            table.AddRow(
                GetNoteName(root),
                $"[magenta]{comprehensive.Quartal ?? comprehensive.Primary}[/]",
                "Quartal harmony chord");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }


    private static void DemonstrateAtonalAnalysis()
    {
        AnsiConsole.MarkupLine("[bold red]4. Atonal Chord Analysis[/]");

        var table = new Table();
        table.AddColumn("Chord Type");
        table.AddColumn("Atonal Name");
        table.AddColumn("Set Theory Info");
        table.AddColumn("Description");
        table.Border(TableBorder.Rounded);

        // Create complex chords that require atonal analysis
        var complexChords = new[]
        {
            (CreateComplexChordTemplate(7), "7-note cluster"),
            (CreateSymmetricalChordTemplate(), "Symmetrical structure"),
            (CreateSimpleChordTemplate(ChordQuality.Suspended, ChordExtension.Sus4, ChordStackingType.Quartal),
                "Quartal harmony")
        };

        foreach (var (template, description) in complexChords)
        {
            var root = PitchClass.FromValue(0); // C

            if (AtonalChordAnalysisService.RequiresAtonalAnalysis(template))
            {
                var analysis = AtonalChordAnalysisService.AnalyzeAtonally(template, root);

                table.AddRow(
                    description,
                    $"[red]{analysis.SuggestedName.EscapeMarkup()}[/]",
                    $"Prime: {analysis.PrimeForm.EscapeMarkup()}, Forte: {analysis.ForteNumber.EscapeMarkup()}",
                    analysis.TheoreticalDescription.EscapeMarkup());
            }
            else
            {
                table.AddRow(
                    description,
                    "[dim]Not complex enough[/]",
                    "[dim]N/A[/]",
                    "[dim]Uses tonal analysis[/]");
            }
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateHybridAnalysis()
    {
        AnsiConsole.MarkupLine("[bold purple]5. Hybrid Tonal-Atonal Analysis[/]");

        var table = new Table();
        table.AddColumn("Chord");
        table.AddColumn("Strategy Used");
        table.AddColumn("Recommended Name");
        table.AddColumn("Reasoning");
        table.Border(TableBorder.Rounded);

        var testChords = new[]
        {
            (CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Seventh), "Simple Maj7"),
            (CreateComplexChordTemplate(5), "5-note complex"),
            (CreateSymmetricalChordTemplate(), "Symmetrical"),
            (CreateSimpleChordTemplate(ChordQuality.Suspended, ChordExtension.Sus4, ChordStackingType.Quartal),
                "Quartal")
        };

        foreach (var (template, description) in testChords)
        {
            var root = PitchClass.FromValue(0); // C
            var analysis = HybridChordNamingService.AnalyzeChord(template, root);

            var strategyColor = analysis.StrategyUsed switch
            {
                HybridChordNamingService.AnalysisStrategy.Tonal => "green",
                HybridChordNamingService.AnalysisStrategy.Atonal => "red",
                HybridChordNamingService.AnalysisStrategy.Hybrid => "yellow",
                _ => "white"
            };

            table.AddRow(
                description,
                $"[{strategyColor}]{analysis.StrategyUsed}[/]",
                $"[cyan]{analysis.RecommendedName.EscapeMarkup()}[/]",
                analysis.Reasoning.EscapeMarkup());
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateKeyAwareNaming()
    {
        AnsiConsole.MarkupLine("[bold green]6. Key-Aware Chord Naming[/]");

        var table = new Table();
        table.AddColumn("Chord");
        table.AddColumn("Most Probable Key");
        table.AddColumn("Key-Aware Name");
        table.AddColumn("Roman Numeral");
        table.AddColumn("Function");
        table.Border(TableBorder.Rounded);

        var testChords = new[]
        {
            (CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Triad), PitchClass.FromValue(0), "C Major"),
            (CreateSimpleChordTemplate(ChordQuality.Minor, ChordExtension.Seventh), PitchClass.FromValue(2),
                "D Minor 7"),
            (CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Seventh), PitchClass.FromValue(7),
                "G Dominant 7"),
            (CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Seventh), PitchClass.FromValue(5),
                "F Major 7")
        };

        foreach (var (template, root, description) in testChords)
        {
            var analysis = KeyAwareChordNamingService.AnalyzeInAllKeys(template, root);
            var mostProbable = analysis.MostProbableKey;

            table.AddRow(
                description,
                $"[cyan]{mostProbable.Key}[/]",
                $"[green]{mostProbable.ChordName.EscapeMarkup()}[/]",
                $"[yellow]{mostProbable.RomanNumeral.EscapeMarkup()}[/]",
                $"[magenta]{mostProbable.Function}[/]");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateProgressionAnalysis()
    {
        AnsiConsole.MarkupLine("[bold blue]7. Chord Progression Analysis[/]");

        // Create a common progression: C - Am - F - G
        var progression = new[]
        {
            (CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Triad), PitchClass.FromValue(0)), // C
            (CreateSimpleChordTemplate(ChordQuality.Minor, ChordExtension.Triad), PitchClass.FromValue(9)), // Am
            (CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Triad), PitchClass.FromValue(5)), // F
            (CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Triad), PitchClass.FromValue(7)) // G
        };

        var analysis = KeyProbabilityAnalyzer.AnalyzeProgression(progression);

        AnsiConsole.MarkupLine("[bold]Progression: C - Am - F - G[/]");
        AnsiConsole.MarkupLine($"Most Probable Key: [cyan]{analysis.MostProbableKey.Key}[/]");
        AnsiConsole.MarkupLine($"Tonal Strength: [yellow]{analysis.OverallTonalStrength:P0}[/]");

        if (analysis.DetectedProgressions.Any())
        {
            AnsiConsole.MarkupLine("\n[bold]Detected Progressions:[/]");
            foreach (var prog in analysis.DetectedProgressions)
            {
                AnsiConsole.MarkupLine($"  • [green]{prog.Name}[/] (strength: {prog.Strength:P0})");
            }
        }

        AnsiConsole.MarkupLine("\n[bold]Top 3 Key Candidates:[/]");
        var table = new Table();
        table.AddColumn("Key");
        table.AddColumn("Probability");
        table.AddColumn("Diatonic Score");
        table.AddColumn("Functional Score");
        table.Border(TableBorder.Rounded);

        foreach (var result in analysis.KeyProbabilities.Take(3))
        {
            table.AddRow(
                result.Key.ToString(),
                $"{result.Probability:P0}",
                $"{result.DiatonicScore:P0}",
                $"{result.FunctionalScore:P0}");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateComprehensiveNaming()
    {
        AnsiConsole.MarkupLine("[bold cyan]8. Comprehensive Chord Naming[/]");

        var majorSeventh = CreateSimpleChordTemplate(ChordQuality.Major, ChordExtension.Seventh);

        var table = new Table();
        table.AddColumn("Aspect");
        table.AddColumn("Name");
        table.Border(TableBorder.Rounded);

        var comprehensive = ChordTemplateNamingService.GenerateComprehensiveNames(
            majorSeventh, PitchClass.FromValue(0), PitchClass.FromValue(4)); // C/E

        table.AddRow("Primary", $"[green]{comprehensive.Primary}[/]");
        table.AddRow("Slash Chord", $"[yellow]{comprehensive.SlashChord ?? "N/A"}[/]");
        table.AddRow("Quartal", $"[magenta]{comprehensive.Quartal ?? "N/A"}[/]");
        table.AddRow("With Alterations", $"[orange1]{comprehensive.WithAlterations ?? "N/A"}[/]");
        table.AddRow("Enharmonic", $"[blue]{comprehensive.EnharmonicEquivalent ?? "N/A"}[/]");
        table.AddRow("Atonal", $"[red]{comprehensive.AtonalName ?? "N/A"}[/]");
        table.AddRow("Key-Aware", $"[cyan]{comprehensive.KeyAwareName ?? "N/A"}[/]");
        table.AddRow("Most Probable Key", $"[lime]{comprehensive.MostProbableKey ?? "N/A"}[/]");

        if (comprehensive.Alternates.Any())
        {
            table.AddRow("Alternates", $"[cyan]{string.Join(", ", comprehensive.Alternates)}[/]");
        }

        AnsiConsole.Write(table);

        // Demonstrate all naming options
        AnsiConsole.MarkupLine("\n[bold]All Naming Options:[/]");
        var allOptions =
            ChordTemplateNamingService.GetAllNamingOptions(majorSeventh, PitchClass.FromValue(0),
                PitchClass.FromValue(4));
        foreach (var option in allOptions)
        {
            AnsiConsole.MarkupLine($"  • [white]{option}[/]");
        }

        AnsiConsole.WriteLine();
    }

    // Helper methods
    private static string GetNoteName(PitchClass pitchClass)
    {
        return pitchClass.Value switch
        {
            0 => "C", 1 => "C#", 2 => "D", 3 => "D#", 4 => "E", 5 => "F",
            6 => "F#", 7 => "G", 8 => "G#", 9 => "A", 10 => "A#", 11 => "B",
            _ => "?"
        };
    }

    private static ChordTemplate CreateSimpleChordTemplate(
        ChordQuality quality,
        ChordExtension extension,
        ChordStackingType stackingType = ChordStackingType.Tertian)
    {
        // Use the factory to create a proper chord template
        // For demonstration, we'll use a simple analytical chord
        var intervals = new List<ChordFormulaInterval>();

        // Add basic intervals based on quality and extension
        if (quality == ChordQuality.Major)
        {
            intervals.Add(new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(4)),
                ChordFunction.Third));
            intervals.Add(new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(7)),
                ChordFunction.Fifth));
        }
        else if (quality == ChordQuality.Minor)
        {
            intervals.Add(new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(3)),
                ChordFunction.Third));
            intervals.Add(new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(7)),
                ChordFunction.Fifth));
        }

        if (extension == ChordExtension.Seventh)
        {
            intervals.Add(new ChordFormulaInterval(new Interval.Chromatic(Semitones.FromValue(11)),
                ChordFunction.Seventh));
        }

        var formula = new ChordFormula($"{quality} {extension}", intervals, stackingType);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Demo");
    }

    private static ChordTemplate CreateComplexChordTemplate(int noteCount)
    {
        // Create a complex chord with many intervals for atonal analysis
        var intervals = new List<ChordFormulaInterval>();

        // Add semitone clusters and complex intervals
        for (var i = 1; i < noteCount; i++)
        {
            var semitones = i switch
            {
                1 => 1, // Minor 2nd
                2 => 3, // Minor 3rd
                3 => 6, // Tritone
                4 => 8, // Minor 6th
                5 => 10, // Minor 7th
                6 => 11, // Major 7th
                _ => i
            };

            intervals.Add(new ChordFormulaInterval(
                new Interval.Chromatic(Semitones.FromValue(semitones)),
                ChordFunction.Root));
        }

        var formula = new ChordFormula($"Complex {noteCount}-note", intervals, ChordStackingType.Mixed);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Complex");
    }

    private static ChordTemplate CreateSymmetricalChordTemplate()
    {
        // Create a symmetrical chord (diminished 7th pattern)
        var intervals = new List<ChordFormulaInterval>
        {
            new(new Interval.Chromatic(Semitones.FromValue(3)), ChordFunction.Third), // Minor 3rd
            new(new Interval.Chromatic(Semitones.FromValue(6)), ChordFunction.Fifth), // Tritone
            new(new Interval.Chromatic(Semitones.FromValue(9)), ChordFunction.Seventh) // Minor 6th
        };

        var formula = new ChordFormula("Symmetrical diminished", intervals, ChordStackingType.Mixed);
        return ChordTemplate.Analytical.FromSetTheory(formula, "Symmetrical");
    }
}
