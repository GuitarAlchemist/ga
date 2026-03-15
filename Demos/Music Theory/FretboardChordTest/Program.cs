namespace FretboardChordTest;

using System.Collections.Immutable;
using GA.Domain.Core.Instruments.Fretboard.Analysis;
using GA.Domain.Core.Instruments.Positions;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using GA.Domain.Services.Fretboard.Analysis;
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
        var results = new List<FiveFretSpanChord>();

        AnsiConsole.Status()
            .Start("Analyzing chord voicings...", ctx =>
            {
                var count = 0;
                foreach (var chord in FretboardChordAnalyzer.GenerateAllFiveFretSpanChords(fretboard, maxFret: 3))
                {
                    var noteCount = chord.Positions.Count(p => p is Position.Played);
                    if (noteCount is >= 3 and <= 4)
                    {
                        results.Add(chord);
                        count++;

                        if (count >= 50) break;
                        ctx.Status($"Found {count} chord voicings...");
                    }
                }
            });

        DisplaySpanResults(results, "Quick Test Results");
    }

    private static void DisplaySpanResults(List<FiveFretSpanChord> results, string title)
    {
        AnsiConsole.MarkupLine($"[bold]{title}[/]");
        AnsiConsole.MarkupLine($"Found {results.Count} chord voicings\n");

        var table = new Table();
        table.AddColumn("Fret Range");
        table.AddColumn("Chord Name");
        table.AddColumn("Pattern");
        table.AddColumn("Notes");
        table.Border(TableBorder.Rounded);

        foreach (var result in results.Take(20))
        {
            var noteCount = result.Positions.Count(p => p is Position.Played);
            table.AddRow(
                $"{result.LowestFret}-{result.HighestFret}",
                result.ChordName.EscapeMarkup(),
                result.Invariant.PatternId.ToPatternString(),
                $"{noteCount} notes");
        }

        AnsiConsole.Write(table);

        if (results.Count > 20)
            AnsiConsole.MarkupLine($"\n[dim]Showing first 20 of {results.Count} results[/]");
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
                result.ChordName.EscapeMarkup(),
                $"[yellow]{difficulty}[/]",
                $"{result.OverallScore:F2}",
                result.Ergonomics.IsPlayable ? "[green]Y[/]" : "[red]N[/]");
        }

        AnsiConsole.Write(table);
    }

    private static List<(string name, ImmutableList<Position> positions)> GetKnownChordVoicings() =>
        [
            ("C Major", CreatePositions([(1, 3), (2, 2), (3, 0), (4, 1), (5, 0)])),
            ("G Major", CreatePositions([(1, 3), (2, 2), (3, 0), (4, 0), (5, 0), (6, 3)])),
            ("D Major", CreatePositions([(1, 2), (2, 2), (3, 3), (4, 0)])),
            ("A Major", CreatePositions([(1, 0), (2, 2), (3, 2), (4, 2), (5, 0)])),
            ("E Major", CreatePositions([(1, 0), (2, 2), (3, 2), (4, 1), (5, 0), (6, 0)])),
            ("F Major Barre", CreatePositions([(1, 1), (2, 1), (3, 3), (4, 3), (5, 2), (6, 1)]))
        ];

    private static ImmutableList<Position> CreatePositions(IEnumerable<(int str, int fret)> stringFretPairs)
    {
        var positions = new List<Position>();

        foreach (var (str, fret) in stringFretPairs)
        {
            var stringObj = Str.FromValue(str);
            var fretObj = Fret.FromValue(fret);
            var location = new PositionLocation(stringObj, fretObj);
            positions.Add(new Position.Played(location, MidiNote.FromValue(60)));
        }

        return [.. positions];
    }
}
