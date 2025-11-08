namespace PsychoacousticVoicingDemo;

using System.Collections.Immutable;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings;
using GA.Business.Core.Notes.Primitives;
using Spectre.Console;

internal class Program
{
    private static void Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Psychoacoustic Voicing Analysis")
                .LeftJustified()
                .Color(Color.Purple));

        AnsiConsole.MarkupLine("[bold]Guitar Chord Voicing Psychoacoustic Analysis & Semantic Search[/]\n");

        // Run all demonstrations
        // TODO: Fix DemonstrateBasicPsychoacousticAnalysis - Type mismatch in AnalyzeVoicing
        // DemonstrateBasicPsychoacousticAnalysis();
        // TODO: Fix DemonstrateSemanticTagging - VoicingSemanticSearchService not found
        // DemonstrateSemanticTagging();
        // TODO: Fix DemonstrateNaturalLanguageSearch - VoicingSemanticSearchService not found
        // DemonstrateNaturalLanguageSearch();
        // TODO: Fix DemonstrateSimilaritySearch - VoicingSemanticSearchService not found
        // DemonstrateSimilaritySearch();
        // TODO: Fix DemonstrateVectorEmbeddings - VoicingSemanticSearchService not found
        // DemonstrateVectorEmbeddings();

        AnsiConsole.MarkupLine("\n[green]Demo completed! Press any key to exit...[/]");
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

    // TODO: Fix DemonstrateBasicPsychoacousticAnalysis - Type mismatch in AnalyzeVoicing
    /*
    private static void DemonstrateBasicPsychoacousticAnalysis()
    {
        AnsiConsole.MarkupLine("[bold yellow]1. Basic Psychoacoustic Analysis[/]");

        var fretboard = Fretboard.Default;
        var testVoicings = GetTestVoicings();

        var table = new Table();
        table.AddColumn("Voicing");
        table.AddColumn("Playability");
        table.AddColumn("Consonance");
        table.AddColumn("Brightness");
        table.AddColumn("Density");
        table.AddColumn("Color");
        table.AddColumn("Overall Quality");
        table.Border(TableBorder.Rounded);

        foreach (var (name, positions) in testVoicings)
        {
            var analysis = PsychoacousticVoicingAnalyzer.AnalyzeVoicing(positions, fretboard);

            table.AddRow(
                name,
                // TODO: Fix GetPlayabilityColor - PlayabilityLevel type not found
                "[gray]N/A[/]", // GetPlayabilityColor(analysis.Physical.Playability),
                GetScoreColor(analysis.Perceptual.ConsonanceScore),
                GetScoreColor(analysis.Perceptual.BrightnessIndex),
                analysis.Textural.Density.ToString(),
                analysis.Textural.Color.ToString(),
                GetScoreColor(analysis.Quality.OverallQuality));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }
    */

    // TODO: Fix DemonstrateSemanticTagging - VoicingSemanticSearchService not found
    /*
    private static void DemonstrateSemanticTagging()
    {
        AnsiConsole.MarkupLine("[bold blue]2. Semantic Tagging System[/]");

        var fretboard = Fretboard.Default;
        var testVoicings = GetTestVoicings();

        foreach (var (name, positions) in testVoicings.Take(3))
        {
            var analysis = PsychoacousticVoicingAnalyzer.AnalyzeVoicing(positions, fretboard);

            AnsiConsole.MarkupLine($"[bold cyan]{name}[/]");
            AnsiConsole.MarkupLine(
                $"  Physical: Fret span {analysis.Physical.FretSpan}, {analysis.Physical.Playability} difficulty");
            AnsiConsole.MarkupLine(
                $"  Perceptual: {analysis.Perceptual.ConsonanceScore:F2} consonance, {analysis.Perceptual.Weight} weight");
            AnsiConsole.MarkupLine($"  Textural: {analysis.Textural.Spacing} spacing, {analysis.Textural.Color} color");
            AnsiConsole.MarkupLine($"  Tags: [green]{string.Join(", ", analysis.SemanticTags)}[/]");
            AnsiConsole.WriteLine();
        }
    }
    */

    // TODO: Fix DemonstrateNaturalLanguageSearch - VoicingSemanticSearchService not found
    /*
    private static void DemonstrateNaturalLanguageSearch()
    {
        AnsiConsole.MarkupLine("[bold green]3. Natural Language Search[/]");

        var fretboard = Fretboard.Default;
        var allVoicings = GetTestVoicings().Select(v => v.positions).ToList();

        var searchQueries = new[]
        {
            "bright and open",
            "warm and mellow",
            "easy to play",
            "jazz voicing",
            "dark and moody"
        };

        foreach (var query in searchQueries)
        {
            AnsiConsole.MarkupLine($"[bold]Search: \"{query}\"[/]");

            var results = VoicingSemanticSearchService.SearchByDescription(query, allVoicings, fretboard, 3);

            var searchTable = new Table();
            searchTable.AddColumn("Voicing");
            searchTable.AddColumn("Similarity");
            searchTable.AddColumn("Description");
            searchTable.AddColumn("Matching Criteria");
            searchTable.Border(TableBorder.Minimal);

            foreach (var result in results)
            {
                var voicingName = GetVoicingName(result.Positions);
                searchTable.AddRow(
                    voicingName,
                    $"{result.SemanticSimilarity:P0}",
                    result.Description.EscapeMarkup(),
                    string.Join(", ", result.MatchingCriteria).EscapeMarkup());
            }

            AnsiConsole.Write(searchTable);
            AnsiConsole.WriteLine();
        }
    }
    */

    // TODO: Fix DemonstrateSimilaritySearch - VoicingSemanticSearchService not found
    /*
    private static void DemonstrateSimilaritySearch()
    {
        AnsiConsole.MarkupLine("[bold magenta]4. Similarity-Based Search[/]");

        var fretboard = Fretboard.Default;
        var testVoicings = GetTestVoicings();
        var referenceVoicing = testVoicings.First().positions; // Use first voicing as reference
        var candidateVoicings = testVoicings.Skip(1).Select(v => v.positions).ToList();

        AnsiConsole.MarkupLine($"[bold]Reference: {testVoicings.First().name}[/]");

        var similarVoicings = VoicingSemanticSearchService.FindSimilarVoicings(
            referenceVoicing, candidateVoicings, fretboard, 5);

        var similarityTable = new Table();
        similarityTable.AddColumn("Similar Voicing");
        similarityTable.AddColumn("Similarity");
        similarityTable.AddColumn("Description");
        similarityTable.AddColumn("Common Characteristics");
        similarityTable.Border(TableBorder.Rounded);

        foreach (var result in similarVoicings)
        {
            var voicingName = GetVoicingName(result.Positions);
            similarityTable.AddRow(
                voicingName,
                $"{result.SemanticSimilarity:P0}",
                result.Description.EscapeMarkup(),
                string.Join(", ", result.MatchingCriteria).EscapeMarkup());
        }

        AnsiConsole.Write(similarityTable);
        AnsiConsole.WriteLine();
    }
    */

    // TODO: Fix DemonstrateVectorEmbeddings - VoicingSemanticSearchService not found
    /*
    private static void DemonstrateVectorEmbeddings()
    {
        AnsiConsole.MarkupLine("[bold red]5. Vector Embeddings for ML Integration[/]");

        var fretboard = Fretboard.Default;
        var testVoicing = GetTestVoicings().First().positions;
        var analysis = PsychoacousticVoicingAnalyzer.AnalyzeVoicing(testVoicing, fretboard);
        var embedding = VoicingSemanticSearchService.GenerateVoicingEmbedding(analysis);

        AnsiConsole.MarkupLine($"[bold]Voicing: {GetVoicingName(testVoicing)}[/]");
        AnsiConsole.MarkupLine($"Vector Dimensions: {embedding.Length}");
        AnsiConsole.MarkupLine("Sample embedding values:");

        var embeddingTable = new Table();
        embeddingTable.AddColumn("Feature Category");
        embeddingTable.AddColumn("Values");
        embeddingTable.Border(TableBorder.Minimal);

        embeddingTable.AddRow("Physical (5 dims)", FormatEmbeddingSegment(embedding, 0, 5));
        embeddingTable.AddRow("Perceptual (8 dims)", FormatEmbeddingSegment(embedding, 5, 8));
        embeddingTable.AddRow("Harmonic (8 dims)", FormatEmbeddingSegment(embedding, 13, 8));
        embeddingTable.AddRow("Textural (4 dims)", FormatEmbeddingSegment(embedding, 21, 4));
        embeddingTable.AddRow("Register (4 dims)", FormatEmbeddingSegment(embedding, 25, 4));
        embeddingTable.AddRow("Quality (5 dims)", FormatEmbeddingSegment(embedding, 29, 5));

        AnsiConsole.Write(embeddingTable);

        AnsiConsole.MarkupLine("\n[dim]These vectors can be used for:");
        AnsiConsole.MarkupLine("• Machine learning model training");
        AnsiConsole.MarkupLine("• Similarity clustering");
        AnsiConsole.MarkupLine("• Recommendation systems");
        AnsiConsole.MarkupLine("• Automated voicing generation[/]");
        AnsiConsole.WriteLine();
    }
    */

    private static List<(string name, ImmutableList<Position> positions)> GetTestVoicings()
    {
        return
        [
            ("C Major Open", CreatePositions([(1, 0), (2, 1), (3, 0), (4, 2), (5, 3)])),
            ("G Major Open", CreatePositions([(1, 3), (2, 2), (3, 0), (4, 0), (5, 0), (6, 3)])),
            ("F Major Barre", CreatePositions([(1, 1), (2, 1), (3, 3), (4, 3), (5, 2), (6, 1)])),
            ("Am7 Jazz", CreatePositions([(1, 5), (2, 5), (3, 5), (4, 5)])),
            ("Cmaj7#11", CreatePositions([(1, 0), (2, 0), (3, 4), (4, 5)])),
            ("E Minor Open", CreatePositions([(1, 0), (2, 0), (3, 0), (4, 2), (5, 2), (6, 0)])),
            ("D7 Cowboy", CreatePositions([(1, 2), (2, 1), (3, 2), (4, 0)])),
            ("Bb Major Barre", CreatePositions([(1, 1), (2, 3), (3, 3), (4, 3), (5, 1), (6, 1)]))
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

        return positions.ToImmutableList();
    }

    // TODO: Fix GetPlayabilityColor - PlayabilityLevel type not found
    /*
    private static string GetPlayabilityColor(PsychoacousticVoicingAnalyzer.PlayabilityLevel playability)
    {
        return playability switch
        {
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Beginner => "[green]Beginner[/]",
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Intermediate => "[yellow]Intermediate[/]",
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Advanced => "[orange1]Advanced[/]",
            PsychoacousticVoicingAnalyzer.PlayabilityLevel.Expert => "[red]Expert[/]",
            _ => "[gray]Unknown[/]"
        };
    }
    */

    private static string GetScoreColor(double score)
    {
        return score switch
        {
            >= 0.8 => $"[green]{score:F2}[/]",
            >= 0.6 => $"[yellow]{score:F2}[/]",
            >= 0.4 => $"[orange1]{score:F2}[/]",
            _ => $"[red]{score:F2}[/]"
        };
    }

    private static string GetVoicingName(ImmutableList<Position> positions)
    {
        // Simple heuristic to identify voicing from our test set
        var playedPositions = positions.OfType<Position.Played>().ToList();
        var fretPattern = string.Join("-", playedPositions.Select(p => p.Location.Fret.Value));

        return fretPattern switch
        {
            "0-1-0-2-3" => "C Major Open",
            "3-2-0-0-0-3" => "G Major Open",
            "1-1-3-3-2-1" => "F Major Barre",
            "5-5-5-5" => "Am7 Jazz",
            "0-0-4-5" => "Cmaj7#11",
            "0-0-0-2-2-0" => "E Minor Open",
            "2-1-2-0" => "D7 Cowboy",
            "1-3-3-3-1-1" => "Bb Major Barre",
            _ => "Unknown Voicing"
        };
    }

    private static string FormatEmbeddingSegment(double[] embedding, int start, int length)
    {
        var segment = embedding.Skip(start).Take(length).Select(v => v.ToString("F2"));
        return $"[{string.Join(", ", segment)}]";
    }
}
