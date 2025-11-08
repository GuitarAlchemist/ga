namespace AdvancedFretboardAnalysisDemo;

using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Biomechanics;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes.Primitives;
using Spectre.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Fretboard Analysis")
                .LeftJustified()
                .Color(Color.Red));

        AnsiConsole.MarkupLine("[bold]Advanced Guitar Fretboard Analysis & Biomechanics[/]\n");

        // Create a standard guitar fretboard
        var fretboard = Fretboard.CreateStandardGuitar();

        DemonstrateChordVoicingAnalysis(fretboard);
        DemonstrateBiomechanicalAnalysis(fretboard);
        DemonstrateErgonomicOptimization(fretboard);
        DemonstrateScalePatternAnalysis(fretboard);
        DemonstrateAdvancedFingeringTechniques(fretboard);

        AnsiConsole.MarkupLine("\n[green]Analysis completed![/]");

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
            // Console input not available
        }
    }

    private static void DemonstrateChordVoicingAnalysis(Fretboard fretboard)
    {
        AnsiConsole.MarkupLine("[bold blue]🎸 Chord Voicing Analysis[/]\n");

        var chordVoicings = new[]
        {
            ("C Major (Open)", CreatePositions(new[] { (3, 2), (2, 1), (1, 0) })),
            ("G Major (Open)", CreatePositions(new[] { (6, 3), (1, 3), (5, 2) })),
            ("F Major (Barre)", CreatePositions(new[] { (6, 1), (5, 1), (4, 3), (3, 3), (2, 2), (1, 1) })),
            ("Cmaj7 (Jazz)", CreatePositions(new[] { (4, 3), (3, 5), (2, 5), (1, 4) })),
            ("Em7b5 (Jazz)", CreatePositions(new[] { (4, 2), (3, 2), (2, 3), (1, 3) }))
        };

        var table = new Table();
        table.AddColumn("Chord");
        table.AddColumn("Difficulty");
        table.AddColumn("Stretch Factor");
        table.AddColumn("Barre Complexity");
        table.AddColumn("Playability");
        table.AddColumn("Recommendations");

        foreach (var (name, positions) in chordVoicings)
        {
            var analysis = FretboardChordAnalyzer.AnalyzeChordVoicing(positions, fretboard);

            table.AddRow(
                name,
                FormatDifficulty(analysis.Ergonomics.DifficultyScore),
                FormatStretch(analysis.Ergonomics.StretchFactor),
                FormatBarre(analysis.Ergonomics.BarreComplexity),
                analysis.Ergonomics.IsPlayable ? "[green]✓[/]" : "[red]✗[/]",
                string.Join(", ", analysis.Recommendations.Take(2))
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateBiomechanicalAnalysis(Fretboard fretboard)
    {
        AnsiConsole.MarkupLine("[bold blue]🦴 Biomechanical Analysis[/]\n");

        var handModel = HandModel.CreateStandardAdult();
        var testPositions = CreatePositions(new[] { (6, 1), (5, 1), (4, 3), (3, 3), (2, 2), (1, 1) }); // F major barre

        AnsiConsole.MarkupLine("[yellow]Analyzing F Major Barre Chord...[/]\n");

        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.AddColumn("Assessment");
        table.AddColumn("Recommendation");

        // Simulate biomechanical analysis
        var fingerSpread = CalculateFingerSpread(testPositions);
        var wristAngle = CalculateWristAngle(testPositions);
        var fingerPressure = CalculateFingerPressure(testPositions);
        var handStrain = CalculateHandStrain(testPositions);

        table.AddRow("Finger Spread", $"{fingerSpread:F1}cm",
            fingerSpread > 8 ? "[red]High[/]" : "[green]Normal[/]",
            fingerSpread > 8 ? "Practice finger independence" : "Good positioning");

        table.AddRow("Wrist Angle", $"{wristAngle:F1}°",
            wristAngle > 30 ? "[red]Excessive[/]" : "[green]Healthy[/]",
            wristAngle > 30 ? "Adjust guitar position" : "Maintain current posture");

        table.AddRow("Finger Pressure", $"{fingerPressure:F1}N",
            fingerPressure > 15 ? "[red]High[/]" : "[green]Moderate[/]",
            fingerPressure > 15 ? "Relax grip, use lighter touch" : "Good pressure control");

        table.AddRow("Overall Strain", $"{handStrain:F1}/10",
            handStrain > 7 ? "[red]High Risk[/]" : "[green]Safe[/]",
            handStrain > 7 ? "Take breaks, warm up" : "Sustainable playing");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateErgonomicOptimization(Fretboard fretboard)
    {
        AnsiConsole.MarkupLine("[bold blue]⚡ Ergonomic Optimization[/]\n");

        AnsiConsole.MarkupLine("[yellow]Comparing different voicings of C Major 7th chord...[/]\n");

        var voicings = new[]
        {
            ("Open Position", CreatePositions(new[] { (4, 2), (3, 0), (2, 1), (1, 0) })),
            ("8th Fret Barre", CreatePositions(new[] { (4, 10), (3, 9), (2, 9), (1, 8) })),
            ("Jazz Voicing", CreatePositions(new[] { (4, 3), (3, 5), (2, 5), (1, 4) })),
            ("Rootless Voicing", CreatePositions(new[] { (4, 3), (3, 5), (2, 4), (1, 3) }))
        };

        var table = new Table();
        table.AddColumn("Voicing");
        table.AddColumn("Overall Score");
        table.AddColumn("Ergonomics");
        table.AddColumn("Harmonics");
        table.AddColumn("Best For");

        foreach (var (name, positions) in voicings)
        {
            var analysis = FretboardChordAnalyzer.AnalyzeChordVoicing(positions, fretboard);

            table.AddRow(
                name,
                $"{analysis.OverallScore:F2}",
                FormatScore(1.0 - analysis.Ergonomics.DifficultyScore),
                FormatScore(analysis.Harmonics.Consonance),
                GetBestUseCase(name)
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateScalePatternAnalysis(Fretboard fretboard)
    {
        AnsiConsole.MarkupLine("[bold blue]🎵 Scale Pattern Analysis[/]\n");

        var scalePatterns = new[]
        {
            ("G Major - Pattern 1",
                CreateScalePositions(new[] { (6, 3), (5, 0), (5, 2), (4, 0), (4, 2), (3, 0), (3, 2) })),
            ("G Major - Pattern 2",
                CreateScalePositions(new[] { (5, 2), (5, 5), (4, 2), (4, 4), (3, 2), (3, 4), (2, 2) })),
            ("G Pentatonic", CreateScalePositions(new[] { (6, 3), (5, 0), (4, 0), (3, 0), (2, 3), (1, 3) })),
            ("G Blues Scale", CreateScalePositions(new[] { (6, 3), (5, 0), (4, 1), (4, 2), (3, 0), (2, 3) }))
        };

        var table = new Table();
        table.AddColumn("Scale Pattern");
        table.AddColumn("Fret Span");
        table.AddColumn("String Changes");
        table.AddColumn("Difficulty");
        table.AddColumn("Technique Focus");

        foreach (var (name, positions) in scalePatterns)
        {
            var fretSpan = CalculateFretSpan(positions);
            var stringChanges = CalculateStringChanges(positions);
            var difficulty = CalculatePatternDifficulty(positions);
            var technique = GetTechniqueFocus(name);

            table.AddRow(
                name,
                $"{fretSpan} frets",
                stringChanges.ToString(),
                FormatDifficulty(difficulty),
                technique
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DemonstrateAdvancedFingeringTechniques(Fretboard fretboard)
    {
        AnsiConsole.MarkupLine("[bold blue]🎯 Advanced Fingering Techniques[/]\n");

        var techniques = new[]
        {
            ("Legato Run", "Smooth connected notes using hammer-ons and pull-offs"),
            ("Sweep Picking", "Fluid arpeggiated motion across multiple strings"),
            ("Tapping", "Two-handed technique for extended range"),
            ("String Skipping", "Non-adjacent string patterns for unique sounds"),
            ("Hybrid Picking", "Combination of pick and fingers for complex textures")
        };

        var table = new Table();
        table.AddColumn("Technique");
        table.AddColumn("Description");
        table.AddColumn("Difficulty");
        table.AddColumn("Practice Focus");
        table.AddColumn("Musical Application");

        foreach (var (name, description) in techniques)
        {
            var difficulty = GetTechniqueDifficulty(name);
            var focus = GetPracticeFocus(name);
            var application = GetMusicalApplication(name);

            table.AddRow(name, description, difficulty, focus, application);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    // Helper methods
    private static ImmutableArray<Position> CreatePositions(IEnumerable<(int String, int Fret)> positions)
    {
        return positions.Select(p => new Position.Played(
            new PositionLocation(new Str(p.String), new Fret(p.Fret)),
            new MidiNote(60 + p.Fret) // Simplified MIDI note calculation
        )).ToImmutableArray<Position>();
    }

    private static ImmutableArray<Position> CreateScalePositions(IEnumerable<(int String, int Fret)> positions)
    {
        return CreatePositions(positions);
    }

    private static string FormatDifficulty(double score)
    {
        return score switch
        {
            < 0.3 => "[green]Easy[/]",
            < 0.6 => "[yellow]Medium[/]",
            < 0.8 => "[orange3]Hard[/]",
            _ => "[red]Very Hard[/]"
        };
    }

    private static string FormatStretch(double factor)
    {
        return $"{factor:F2}";
    }

    private static string FormatBarre(double complexity)
    {
        return complexity > 0.5 ? "[yellow]Required[/]" : "[green]None[/]";
    }

    private static string FormatScore(double score)
    {
        return $"{score:F2}";
    }

    private static string GetBestUseCase(string voicing)
    {
        return voicing switch
        {
            "Open Position" => "Beginner, Strumming",
            "8th Fret Barre" => "Rock, Power",
            "Jazz Voicing" => "Jazz, Sophisticated",
            "Rootless Voicing" => "Comping, Advanced",
            _ => "General"
        };
    }

    // Simplified biomechanical calculations
    private static double CalculateFingerSpread(ImmutableArray<Position> positions)
    {
        return positions.Any()
            ? positions.Max(p => p.Location.Fret.Value) - positions.Min(p => p.Location.Fret.Value) + 2.0
            : 0;
    }

    private static double CalculateWristAngle(ImmutableArray<Position> positions)
    {
        return Math.Min(45, positions.Count() * 3.5 + 15);
    }

    private static double CalculateFingerPressure(ImmutableArray<Position> positions)
    {
        return positions.Count() * 2.5 + (positions.Any(p => p.Location.Fret.Value > 12) ? 5 : 0);
    }

    private static double CalculateHandStrain(ImmutableArray<Position> positions)
    {
        return Math.Min(10, positions.Count() * 0.8 + CalculateFingerSpread(positions) * 0.3);
    }

    private static int CalculateFretSpan(ImmutableArray<Position> positions)
    {
        return positions.Any()
            ? positions.Max(p => p.Location.Fret.Value) - positions.Min(p => p.Location.Fret.Value) + 1
            : 0;
    }

    private static int CalculateStringChanges(ImmutableArray<Position> positions)
    {
        return positions.Count() > 1 ? positions.Count() - 1 : 0;
    }

    private static double CalculatePatternDifficulty(ImmutableArray<Position> positions)
    {
        return Math.Min(1.0, CalculateFretSpan(positions) * 0.1 + CalculateStringChanges(positions) * 0.05);
    }

    private static string GetTechniqueFocus(string pattern)
    {
        return pattern switch
        {
            var p when p.Contains("Pentatonic") => "Bending, Expression",
            var p when p.Contains("Blues") => "Vibrato, Feel",
            var p when p.Contains("Pattern 1") => "Foundation, Accuracy",
            var p when p.Contains("Pattern 2") => "Position Shifts",
            _ => "General Technique"
        };
    }

    private static string GetTechniqueDifficulty(string technique)
    {
        return technique switch
        {
            "Legato Run" => "[yellow]Intermediate[/]",
            "Sweep Picking" => "[red]Advanced[/]",
            "Tapping" => "[red]Advanced[/]",
            "String Skipping" => "[orange3]Intermediate+[/]",
            "Hybrid Picking" => "[yellow]Intermediate[/]",
            _ => "[green]Beginner[/]"
        };
    }

    private static string GetPracticeFocus(string technique)
    {
        return technique switch
        {
            "Legato Run" => "Left hand strength, timing",
            "Sweep Picking" => "Right hand economy, muting",
            "Tapping" => "Two-hand coordination",
            "String Skipping" => "Accuracy, clean execution",
            "Hybrid Picking" => "Pick/finger independence",
            _ => "General technique"
        };
    }

    private static string GetMusicalApplication(string technique)
    {
        return technique switch
        {
            "Legato Run" => "Lead guitar, melodic lines",
            "Sweep Picking" => "Arpeggios, neo-classical",
            "Tapping" => "Extended range, modern rock",
            "String Skipping" => "Unique textures, jazz fusion",
            "Hybrid Picking" => "Country, fingerstyle rock",
            _ => "Various styles"
        };
    }
}
