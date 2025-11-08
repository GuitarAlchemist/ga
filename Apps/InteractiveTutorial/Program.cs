namespace InteractiveTutorial;

using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Scales;
using Spectre.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Guitar Alchemist")
                .LeftJustified()
                .Color(Color.Green));

        AnsiConsole.Write(
            new FigletText("Interactive Tutorial")
                .LeftJustified()
                .Color(Color.Cyan1));

        AnsiConsole.MarkupLine("[bold]Welcome to the Guitar Alchemist Interactive Learning Experience![/]\n");

        ShowMainMenu();
    }

    private static void ShowMainMenu()
    {
        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold blue]Choose a tutorial:[/]")
                    .AddChoices("🎵 Music Theory Fundamentals", "🎸 Fretboard Mastery", "🎼 Chord Progressions",
                        "🔬 Advanced Analysis", "🤖 AI-Powered Features", "⚡ Performance Optimization",
                        "📚 Complete Walkthrough", "❌ Exit"));

            switch (choice)
            {
                case "🎵 Music Theory Fundamentals":
                    MusicTheoryTutorial();
                    break;
                case "🎸 Fretboard Mastery":
                    FretboardTutorial();
                    break;
                case "🎼 Chord Progressions":
                    ChordProgressionTutorial();
                    break;
                case "🔬 Advanced Analysis":
                    AdvancedAnalysisTutorial();
                    break;
                case "🤖 AI-Powered Features":
                    AIFeaturesTutorial();
                    break;
                case "⚡ Performance Optimization":
                    PerformanceTutorial();
                    break;
                case "📚 Complete Walkthrough":
                    CompleteWalkthrough();
                    break;
                case "❌ Exit":
                    AnsiConsole.MarkupLine("[green]Thanks for using Guitar Alchemist![/]");
                    return;
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Press any key to return to main menu...[/]");
            Console.ReadKey();
            Console.Clear();
        }
    }

    private static void MusicTheoryTutorial()
    {
        AnsiConsole.MarkupLine("[bold blue]🎵 Music Theory Fundamentals Tutorial[/]\n");

        var steps = new[]
        {
            ("Understanding Pitch Classes", () => DemonstratePitchClasses()),
            ("Building Scales", () => DemonstrateScales()),
            ("Constructing Chords", () => DemonstrateChords()),
            ("Interval Relationships", () => DemonstrateIntervals()),
            ("Key Signatures", () => DemonstrateKeySignatures())
        };

        RunStepByStepTutorial(steps);
    }

    private static void FretboardTutorial()
    {
        AnsiConsole.MarkupLine("[bold blue]🎸 Fretboard Mastery Tutorial[/]\n");

        var steps = new[]
        {
            ("Guitar Tuning & Setup", () => DemonstrateTuning()),
            ("Finding Notes on Fretboard", () => DemonstrateFretboardNotes()),
            ("Chord Shapes & Voicings", () => DemonstrateChordShapes()),
            ("Scale Patterns", () => DemonstrateScalePatterns()),
            ("Advanced Techniques", () => DemonstrateAdvancedTechniques())
        };

        RunStepByStepTutorial(steps);
    }

    private static void ChordProgressionTutorial()
    {
        AnsiConsole.MarkupLine("[bold blue]🎼 Chord Progressions Tutorial[/]\n");

        var steps = new[]
        {
            ("Common Progressions", () => DemonstrateCommonProgressions()),
            ("Functional Harmony", () => DemonstrateFunctionalHarmony()),
            ("Voice Leading", () => DemonstrateVoiceLeading()),
            ("Chord Substitutions", () => DemonstrateSubstitutions()),
            ("Creating Your Own", () => DemonstrateProgressionCreation())
        };

        RunStepByStepTutorial(steps);
    }

    private static void AdvancedAnalysisTutorial()
    {
        AnsiConsole.MarkupLine("[bold blue]🔬 Advanced Analysis Tutorial[/]\n");

        var steps = new[]
        {
            ("Set Theory Basics", () => DemonstrateSetTheory()),
            ("Spectral Analysis", () => DemonstrateSpectralAnalysis()),
            ("Biomechanical Modeling", () => DemonstrateBiomechanics()),
            ("Harmonic Complexity", () => DemonstrateComplexity()),
            ("Mathematical Relationships", () => DemonstrateMathematics())
        };

        RunStepByStepTutorial(steps);
    }

    private static void AIFeaturesTutorial()
    {
        AnsiConsole.MarkupLine("[bold blue]🤖 AI-Powered Features Tutorial[/]\n");

        var steps = new[]
        {
            ("Semantic Search", () => DemonstrateSemanticSearch()),
            ("Natural Language Queries", () => DemonstrateNLQueries()),
            ("Intelligent Recommendations", () => DemonstrateRecommendations()),
            ("Style Recognition", () => DemonstrateStyleRecognition()),
            ("Automated Analysis", () => DemonstrateAutomation())
        };

        RunStepByStepTutorial(steps);
    }

    private static void PerformanceTutorial()
    {
        AnsiConsole.MarkupLine("[bold blue]⚡ Performance Optimization Tutorial[/]\n");

        var steps = new[]
        {
            ("SIMD Vectorization", () => DemonstrateSIMD()),
            ("Parallel Processing", () => DemonstrateParallel()),
            ("Memory Optimization", () => DemonstrateMemory()),
            ("Real-time Processing", () => DemonstrateRealTime()),
            ("Benchmarking", () => DemonstrateBenchmarking())
        };

        RunStepByStepTutorial(steps);
    }

    private static void CompleteWalkthrough()
    {
        AnsiConsole.MarkupLine("[bold blue]📚 Complete Guitar Alchemist Walkthrough[/]\n");

        AnsiConsole.MarkupLine("This comprehensive tutorial will guide you through all major features:");
        AnsiConsole.MarkupLine("• Music theory foundations");
        AnsiConsole.MarkupLine("• Fretboard analysis");
        AnsiConsole.MarkupLine("• AI integration");
        AnsiConsole.MarkupLine("• Performance optimization");
        AnsiConsole.MarkupLine("• Real-world applications\n");

        if (AnsiConsole.Confirm("This will take about 30 minutes. Continue?"))
        {
            MusicTheoryTutorial();
            FretboardTutorial();
            ChordProgressionTutorial();
            AdvancedAnalysisTutorial();
            AIFeaturesTutorial();
            PerformanceTutorial();

            AnsiConsole.MarkupLine(
                "[bold green]🎉 Congratulations! You've completed the full Guitar Alchemist tutorial![/]");
        }
    }

    private static void RunStepByStepTutorial((string title, Action demo)[] steps)
    {
        for (var i = 0; i < steps.Length; i++)
        {
            var (title, demo) = steps[i];

            AnsiConsole.MarkupLine($"[bold yellow]Step {i + 1}/{steps.Length}: {title}[/]\n");

            demo();

            if (i < steps.Length - 1)
            {
                AnsiConsole.WriteLine();
                if (!AnsiConsole.Confirm("Continue to next step?"))
                {
                    break;
                }

                AnsiConsole.WriteLine();
            }
        }
    }

    // Tutorial demonstration methods
    private static void DemonstratePitchClasses()
    {
        AnsiConsole.MarkupLine("Pitch classes are the 12 unique note names in Western music:");

        var table = new Table();
        table.AddColumn("Pitch Class");
        table.AddColumn("Number");
        table.AddColumn("Enharmonic");

        for (var i = 0; i < 12; i++)
        {
            var pc = new PitchClass(i);
            table.AddRow(pc.ToString(), i.ToString(), GetEnharmonic(i));
        }

        AnsiConsole.Write(table);
    }

    private static void DemonstrateScales()
    {
        AnsiConsole.MarkupLine("Let's build a C Major scale:");

        var cMajor = Scale.Major.WithRoot(PitchClass.C);
        AnsiConsole.MarkupLine($"C Major scale: {string.Join(" - ", cMajor.Notes)}");
        AnsiConsole.MarkupLine("Pattern: W-W-H-W-W-W-H (W=Whole step, H=Half step)");
    }

    private static void DemonstrateChords()
    {
        AnsiConsole.MarkupLine("Building chords from scale degrees:");

        var chords = new[]
        {
            ("C Major", new[] { PitchClass.C, PitchClass.E, PitchClass.G }),
            ("D minor", new[] { PitchClass.D, PitchClass.F, PitchClass.A }),
            ("E minor", new[] { PitchClass.E, PitchClass.G, PitchClass.B })
        };

        foreach (var (name, notes) in chords)
        {
            AnsiConsole.MarkupLine($"{name}: {string.Join(" - ", notes.Select(n => n.ToString()))}");
        }
    }

    private static void DemonstrateIntervals()
    {
        AnsiConsole.MarkupLine("Intervals from C:");
        AnsiConsole.MarkupLine("C to D = Major 2nd (2 semitones)");
        AnsiConsole.MarkupLine("C to E = Major 3rd (4 semitones)");
        AnsiConsole.MarkupLine("C to G = Perfect 5th (7 semitones)");
    }

    private static void DemonstrateKeySignatures()
    {
        AnsiConsole.MarkupLine("Key signatures tell us which notes to sharp or flat:");
        AnsiConsole.MarkupLine("C Major: No sharps or flats");
        AnsiConsole.MarkupLine("G Major: F# (1 sharp)");
        AnsiConsole.MarkupLine("F Major: Bb (1 flat)");
    }

    private static void DemonstrateTuning()
    {
        AnsiConsole.MarkupLine("Standard guitar tuning (low to high):");
        var tuning = Tuning.Default;
        for (var i = 1; i <= 6; i++)
        {
            var note = tuning[new Str(i)];
            AnsiConsole.MarkupLine($"String {i}: {note}");
        }
    }

    private static void DemonstrateFretboardNotes()
    {
        AnsiConsole.MarkupLine("Finding notes on the fretboard:");
        AnsiConsole.MarkupLine("• Each fret raises the pitch by one semitone");
        AnsiConsole.MarkupLine("• 12th fret = same note as open string, one octave higher");
        AnsiConsole.MarkupLine("• Use octave shapes to find notes across the neck");
    }

    private static void DemonstrateChordShapes()
    {
        AnsiConsole.MarkupLine("Common chord shapes:");
        AnsiConsole.MarkupLine("• Open chords: Use open strings");
        AnsiConsole.MarkupLine("• Barre chords: Index finger across multiple strings");
        AnsiConsole.MarkupLine("• Jazz voicings: Complex harmony, fewer strings");
    }

    private static void DemonstrateScalePatterns()
    {
        AnsiConsole.MarkupLine("Scale patterns help you play scales across the fretboard:");
        AnsiConsole.MarkupLine("• Pattern 1: Starting with root on 6th string");
        AnsiConsole.MarkupLine("• Pattern 2: Starting with 2nd degree");
        AnsiConsole.MarkupLine("• Connect patterns for full fretboard coverage");
    }

    private static void DemonstrateAdvancedTechniques()
    {
        AnsiConsole.MarkupLine("Advanced guitar techniques:");
        AnsiConsole.MarkupLine("• Legato: Hammer-ons and pull-offs");
        AnsiConsole.MarkupLine("• Sweep picking: Fluid arpeggiated motion");
        AnsiConsole.MarkupLine("• Tapping: Two-handed technique");
    }

    // Simplified implementations for other demo methods
    private static void DemonstrateCommonProgressions()
    {
        AnsiConsole.MarkupLine("Common progressions: I-V-vi-IV, ii-V-I, vi-IV-I-V");
    }

    private static void DemonstrateFunctionalHarmony()
    {
        AnsiConsole.MarkupLine("Tonic (stable), Predominant (motion), Dominant (tension)");
    }

    private static void DemonstrateVoiceLeading()
    {
        AnsiConsole.MarkupLine("Smooth voice leading: Move notes by smallest intervals");
    }

    private static void DemonstrateSubstitutions()
    {
        AnsiConsole.MarkupLine("Tritone substitution: Replace V7 with bII7");
    }

    private static void DemonstrateProgressionCreation()
    {
        AnsiConsole.MarkupLine("Start with I, add tension with V, resolve or continue");
    }

    private static void DemonstrateSetTheory()
    {
        AnsiConsole.MarkupLine("Pitch class sets: Mathematical analysis of harmony");
    }

    private static void DemonstrateSpectralAnalysis()
    {
        AnsiConsole.MarkupLine("Frequency analysis of musical sounds");
    }

    private static void DemonstrateBiomechanics()
    {
        AnsiConsole.MarkupLine("Hand position and finger movement optimization");
    }

    private static void DemonstrateComplexity()
    {
        AnsiConsole.MarkupLine("Measuring harmonic complexity and tension");
    }

    private static void DemonstrateMathematics()
    {
        AnsiConsole.MarkupLine("Mathematical relationships in music theory");
    }

    private static void DemonstrateSemanticSearch()
    {
        AnsiConsole.MarkupLine("Find chords by describing their sound or mood");
    }

    private static void DemonstrateNLQueries()
    {
        AnsiConsole.MarkupLine("Ask questions in natural language about music theory");
    }

    private static void DemonstrateRecommendations()
    {
        AnsiConsole.MarkupLine("AI suggests practice routines and chord progressions");
    }

    private static void DemonstrateStyleRecognition()
    {
        AnsiConsole.MarkupLine("Automatically identify musical styles and genres");
    }

    private static void DemonstrateAutomation()
    {
        AnsiConsole.MarkupLine("Automated analysis of songs and compositions");
    }

    private static void DemonstrateSIMD()
    {
        AnsiConsole.MarkupLine("Vector operations for ultra-fast calculations");
    }

    private static void DemonstrateParallel()
    {
        AnsiConsole.MarkupLine("Multi-core processing for large datasets");
    }

    private static void DemonstrateMemory()
    {
        AnsiConsole.MarkupLine("Memory-efficient algorithms and data structures");
    }

    private static void DemonstrateRealTime()
    {
        AnsiConsole.MarkupLine("Real-time audio processing and analysis");
    }

    private static void DemonstrateBenchmarking()
    {
        AnsiConsole.MarkupLine("Performance measurement and optimization");
    }

    private static string GetEnharmonic(int pitchClass)
    {
        return pitchClass switch
        {
            1 => "Db",
            3 => "Eb",
            6 => "Gb",
            8 => "Ab",
            10 => "Bb",
            _ => ""
        };
    }
}
