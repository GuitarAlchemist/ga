namespace FretboardChordTest;

using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Analysis;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Notes.Primitives;
using Spectre.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Fretboard Chord Test")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold]Comprehensive Guitar Fretboard Chord Naming Test[/]\n");

        // Run all tests automatically for demonstration
        AnsiConsole.MarkupLine("[bold]Running all tests automatically...[/]\n");

        RunKnownChordTest();
        RunQuickTest();

        AnsiConsole.MarkupLine("\n[green]Test completed! Press any key to exit...[/]");
        try
        {
            if (!Console.IsInputRedirected && Environment.UserInteractive)
            {
                Console.ReadKey();
            }
        }
        catch (InvalidOperationException)
        {
            // Console input not available
        }
    }

    private static void RunQuickTest()
    {
        AnsiConsole.MarkupLine("[bold yellow]Quick Test: First 3 frets, 3-4 note chords[/]\n");

        var fretboard = Fretboard.Default;
        var testResults = new List<FretboardChordAnalyzer.FretboardChordAnalysis>();

        AnsiConsole.Status()
            .Start("Analyzing chord voicings...", ctx =>
            {
                var count = 0;
                foreach (var analysis in fretboard.GenerateAllFiveFretSpanChords(3))
                {
                    var noteCount = analysis.Voicing.Length;
                    if (noteCount >= 3 && noteCount <= 4 && analysis.Ergonomics.IsPlayable)
                    {
                        testResults.Add(analysis);
                        count++;

                        if (count >= 50)
                        {
                            break; // Limit for quick test
                        }

                        ctx.Status($"Found {count} chord voicings...");
                    }
                }
            });

        DisplayResults(testResults, "Quick Test Results");
    }

    private static void RunMediumTest()
    {
        AnsiConsole.MarkupLine("[bold orange1]Medium Test: First 7 frets, 3-5 note chords[/]\n");

        var fretboard = Fretboard.Default;
        var testResults = new List<FretboardChordAnalyzer.FretboardChordAnalysis>();

        AnsiConsole.Status()
            .Start("Analyzing chord voicings...", ctx =>
            {
                var count = 0;
                foreach (var analysis in fretboard.GenerateAllFiveFretSpanChords(7))
                {
                    var noteCount = analysis.Voicing.Length;
                    if (noteCount >= 3 && noteCount <= 5 && analysis.Ergonomics.IsPlayable)
                    {
                        testResults.Add(analysis);
                        count++;

                        if (count >= 200)
                        {
                            break; // Limit for medium test
                        }

                        ctx.Status($"Found {count} chord voicings...");
                    }
                }
            });

        DisplayResults(testResults, "Medium Test Results");
    }

    private static void RunFullTest()
    {
        AnsiConsole.MarkupLine("[bold red]Full Test: All 24 frets, 3-6 note chords[/]\n");
        AnsiConsole.MarkupLine("[yellow]Warning: This will take several minutes and generate thousands of results![/]");

        var fretboard = Fretboard.Default;
        var testResults = new List<FretboardChordAnalyzer.FretboardChordAnalysis>();

        AnsiConsole.Status()
            .Start("Analyzing chord voicings...", ctx =>
            {
                var count = 0;
                foreach (var analysis in fretboard.GenerateAllFiveFretSpanChords(24))
                {
                    var noteCount = analysis.Voicing.Length;
                    if (noteCount >= 3 && noteCount <= 6 && analysis.Ergonomics.IsPlayable)
                    {
                        testResults.Add(analysis);
                        count++;

                        if (count >= 1000)
                        {
                            break; // Limit to prevent overwhelming output
                        }

                        if (count % 50 == 0)
                        {
                            ctx.Status($"Found {count} chord voicings...");
                        }
                    }
                }
            });

        DisplayResults(testResults, "Full Test Results (Limited to 1000)");
    }

    private static void RunSpecificRangeTest()
    {
        var startFret = 5;
        var endFret = 10;

        AnsiConsole.MarkupLine($"[bold cyan]Testing frets {startFret} to {endFret}[/]\n");

        var fretboard = Fretboard.Default;
        var testResults = new List<FretboardChordAnalyzer.FretboardChordAnalysis>();

        AnsiConsole.Status()
            .Start("Analyzing chord voicings...", ctx =>
            {
                var count = 0;
                foreach (var analysis in fretboard.GenerateAllFiveFretSpanChords(endFret))
                {
                    var lowestFret = analysis.Voicing.Min(p => p.Location.Fret.Value);
                    var highestFret = analysis.Voicing.Max(p => p.Location.Fret.Value);
                    if (lowestFret >= startFret && highestFret <= endFret && analysis.Ergonomics.IsPlayable)
                    {
                        testResults.Add(analysis);
                        count++;

                        if (count >= 100)
                        {
                            break;
                        }

                        ctx.Status($"Found {count} chord voicings...");
                    }
                }
            });

        DisplayResults(testResults, $"Frets {startFret}-{endFret} Test Results");
    }

    private static void RunKnownChordTest()
    {
        AnsiConsole.MarkupLine("[bold green]Testing Known Chord Voicings[/]\n");

        var knownChords = GetKnownChordVoicings();
        var testResults = new List<FretboardChordAnalyzer.FretboardChordAnalysis>();

        foreach (var (name, positions) in knownChords)
        {
            var analysis = FretboardChordAnalyzer.AnalyzeChordVoicing(positions, Fretboard.Default);
            testResults.Add(analysis);
        }

        DisplayKnownChordResults(testResults, knownChords);
    }

    private static void DisplayResults(List<FretboardChordAnalyzer.FretboardChordAnalysis> results, string title)
    {
        AnsiConsole.MarkupLine($"[bold]{title}[/]");
        AnsiConsole.MarkupLine($"Found {results.Count} playable chord voicings\n");

        // Group by difficulty score
        var byDifficulty = results.GroupBy(r =>
            r.Ergonomics.DifficultyScore switch
            {
                < 0.3 => "Beginner",
                < 0.5 => "Intermediate",
                < 0.7 => "Advanced",
                _ => "Expert"
            }).ToList();

        foreach (var group in byDifficulty)
        {
            var color = group.Key switch
            {
                "Beginner" => "green",
                "Intermediate" => "yellow",
                "Advanced" => "orange1",
                "Expert" => "red",
                _ => "white"
            };

            AnsiConsole.MarkupLine($"[{color}]{group.Key}: {group.Count()} chords[/]");
        }

        AnsiConsole.WriteLine();

        // Show sample results
        var sampleResults = results.Take(20).ToList();

        var table = new Table();
        table.AddColumn("Fret Range");
        table.AddColumn("Chord Symbol");
        table.AddColumn("Difficulty");
        table.AddColumn("Score");
        table.AddColumn("Notes");
        table.Border(TableBorder.Rounded);

        foreach (var result in sampleResults)
        {
            var lowestFret = result.Voicing.Min(p => p.Location.Fret.Value);
            var highestFret = result.Voicing.Max(p => p.Location.Fret.Value);
            var difficulty = result.Ergonomics.DifficultyScore switch
            {
                < 0.3 => "Beginner",
                < 0.5 => "Intermediate",
                < 0.7 => "Advanced",
                _ => "Expert"
            };
            var difficultyColor = difficulty switch
            {
                "Beginner" => "green",
                "Intermediate" => "yellow",
                "Advanced" => "orange1",
                "Expert" => "red",
                _ => "white"
            };

            table.AddRow(
                $"{lowestFret}-{highestFret}",
                result.Harmonics.ChordSymbol.EscapeMarkup(),
                $"[{difficultyColor}]{difficulty}[/]",
                $"{result.OverallScore:F2}",
                $"{result.Voicing.Length} notes");
        }

        AnsiConsole.Write(table);

        if (results.Count > 20)
        {
            AnsiConsole.MarkupLine($"\n[dim]Showing first 20 of {results.Count} results[/]");
        }
    }

    private static void DisplayKnownChordResults(List<FretboardChordAnalyzer.FretboardChordAnalysis> results,
        List<(string name, ImmutableList<Position> positions)> knownChords)
    {
        AnsiConsole.MarkupLine("[bold green]Known Chord Voicings Analysis[/]\n");

        var table = new Table();
        table.AddColumn("Expected");
        table.AddColumn("Chord Symbol");
        table.AddColumn("Difficulty");
        table.AddColumn("Score");
        table.AddColumn("Playable");
        table.Border(TableBorder.Rounded);

        for (var i = 0; i < results.Count && i < knownChords.Count; i++)
        {
            var expected = knownChords[i].name;
            var result = results[i];
            var difficulty = result.Ergonomics.DifficultyScore switch
            {
                < 0.3 => "Beginner",
                < 0.5 => "Intermediate",
                < 0.7 => "Advanced",
                _ => "Expert"
            };

            table.AddRow(
                expected,
                result.Harmonics.ChordSymbol.EscapeMarkup(),
                $"[yellow]{difficulty}[/]",
                $"{result.OverallScore:F2}",
                result.Ergonomics.IsPlayable ? "[green]✓[/]" : "[red]✗[/]");
        }

        AnsiConsole.Write(table);
    }

    private static List<(string name, ImmutableList<Position> positions)> GetKnownChordVoicings()
    {
        // Define some well-known chord voicings for testing
        return
        [
            ("C Major", CreatePositions([(1, 3), (2, 2), (3, 0), (4, 1), (5, 0)])),
            ("G Major", CreatePositions([(1, 3), (2, 2), (3, 0), (4, 0), (5, 0), (6, 3)])),
            ("D Major", CreatePositions([(1, 2), (2, 2), (3, 3), (4, 0)])),
            ("A Major", CreatePositions([(1, 0), (2, 2), (3, 2), (4, 2), (5, 0)])),
            ("E Major", CreatePositions([(1, 0), (2, 2), (3, 2), (4, 1), (5, 0), (6, 0)])),
            ("F Major Barre", CreatePositions([(1, 1), (2, 1), (3, 3), (4, 3), (5, 2), (6, 1)]))
        ];
    }

    private static ImmutableList<Position> CreatePositions(IEnumerable<(int str, int fret)> stringFretPairs)
    {
        var positions = new List<Position>();

        foreach (var (str, fret) in stringFretPairs)
        {
            var stringObj = Str.FromValue(str);
            var fretObj = Fret.FromValue(fret);
            var location = new PositionLocation(stringObj, fretObj);
            positions.Add(new Position.Played(location, MidiNote.FromValue(60))); // Dummy MIDI note
        }

        return [.. positions];
    }
}
