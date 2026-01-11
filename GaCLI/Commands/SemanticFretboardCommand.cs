namespace GaCLI.Commands;

using GA.Business.Intelligence.SemanticIndexing;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Notes;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.CommandLine;

/// <summary>
///     CLI command for testing semantic fretboard indexing and natural language querying
///     Provides an interactive interface to index voicings and ask questions
/// </summary>
public class SemanticFretboardCommand(SemanticFretboardService semanticService,
    ILogger<SemanticFretboardCommand> logger)
{
    /// <summary>
    ///     Create the CLI command definition
    /// </summary>
    public static Command CreateCommand()
    {
        // TODO: Implement System.CommandLine command when API is stable
        // For now, return a stub command
        var command = new Command("semantic-fretboard",
            "Test semantic fretboard indexing and natural language querying");

        return command;
    }

    /// <summary>
    ///     Execute the semantic fretboard command
    /// </summary>
    public async Task ExecuteAsync(SemanticFretboardOptions options)
    {
        try
        {
            AnsiConsole.Write(
                new FigletText("Semantic Fretboard")
                    .LeftJustified()
                    .Color(Color.Green));

            AnsiConsole.MarkupLine("[bold]Guitar Alchemist Semantic Fretboard Testing[/]\n");

            // Index voicings if requested
            if (options.ShouldIndex)
            {
                await IndexVoicingsAsync(options);
            }

            // Process query or enter interactive mode
            if (options.Interactive)
            {
                await RunInteractiveModeAsync();
            }
            else if (!string.IsNullOrEmpty(options.Query))
            {
                await ProcessSingleQueryAsync(options.Query);
            }
            else
            {
                AnsiConsole.MarkupLine("[yellow]No query specified. Use --query or --interactive[/]");
                await ShowIndexStatistics();
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            logger.LogError(ex, "Error executing semantic fretboard command");
        }
    }

    /// <summary>
    ///     Index fretboard voicings with progress display
    /// </summary>
    private async Task IndexVoicingsAsync(SemanticFretboardOptions options)
    {
        AnsiConsole.MarkupLine(
            $"[yellow]Indexing {options.Tuning} guitar voicings (max fret: {options.MaxFret})...[/]");

        var tuning = GetTuningByName(options.Tuning);
        var progress = new Progress<IndexingProgress>();

        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Indexing voicings[/]");
                task.MaxValue = 100;

                progress.ProgressChanged += (_, p) =>
                {
                    task.Value = p.PercentComplete;
                    task.Description = $"[green]Indexing voicings ({p.Indexed}/{p.Total}, {p.ErrorRate:P1} errors)[/]";
                };

                var result = await semanticService.IndexFretboardVoicingsAsync(
                    tuning,
                    instrumentName: $"{options.Tuning} Guitar",
                    maxFret: options.MaxFret,
                    includeBiomechanicalAnalysis: true,
                    progress: progress);

                task.Value = 100;
                task.Description = "[green]Indexing completed[/]";

                AnsiConsole.MarkupLine("\n[green]✓ Indexing completed![/]");
                AnsiConsole.MarkupLine(
                    $"[dim]Indexed {result.IndexedVoicings:N0} voicings in {result.ElapsedTime.TotalSeconds:F1}s[/]");
                AnsiConsole.MarkupLine(
                    $"[dim]Success rate: {result.SuccessRate:P1}, Speed: {result.IndexingRate:F0} voicings/sec[/]");
            });
    }

    /// <summary>
    ///     Run interactive mode for testing queries
    /// </summary>
    private async Task RunInteractiveModeAsync()
    {
        AnsiConsole.MarkupLine("[bold cyan]Interactive Semantic Fretboard Query Mode[/]");
        AnsiConsole.MarkupLine("[dim]Type 'exit' to quit, 'stats' to show index statistics[/]\n");

        while (true)
        {
            var query = AnsiConsole.Ask<string>("[green]Enter your guitar query:[/]");

            if (query.ToLower() == "exit")
            {
                break;
            }

            if (query.ToLower() == "stats")
            {
                await ShowIndexStatistics();
                continue;
            }

            try
            {
                await ProcessSingleQueryAsync(query);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            }

            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    ///     Process a single query and display results
    /// </summary>
    private async Task ProcessSingleQueryAsync(string query)
    {
        AnsiConsole.MarkupLine($"[yellow]Processing query: '{query}'[/]");

        await AnsiConsole.Status()
            .StartAsync("Searching and generating response...", async _ =>
            {
                var result = await semanticService.ProcessNaturalLanguageQueryAsync(query);

                AnsiConsole.MarkupLine($"\n[green]✓ Query completed in {result.ElapsedTime.TotalMilliseconds:F0}ms[/]");
                AnsiConsole.MarkupLine(
                    $"[dim]Model: {result.ModelUsed}, Results: {result.ResultCount}, Avg Relevance: {result.AverageRelevanceScore:F2}[/]\n");

                // Show LLM response
                var panel = new Panel(result.LlmInterpretation)
                    .Header("[bold blue]Guitar Expert Response[/]")
                    .Border(BoxBorder.Rounded)
                    .BorderColor(Color.Blue);
                AnsiConsole.Write(panel);

                // Show top search results
                if (result.SearchResults.Any())
                {
                    AnsiConsole.MarkupLine("\n[bold]Top Matching Voicings:[/]");

                    var table = new Table();
                    table.AddColumn("Rank");
                    table.AddColumn("Chord");
                    table.AddColumn("Score");
                    table.AddColumn("Details");
                    table.Border(TableBorder.Minimal);

                    for (var i = 0; i < Math.Min(5, result.SearchResults.Count); i++)
                    {
                        var searchResult = result.SearchResults[i];
                        var chordName = searchResult.Metadata.TryGetValue("chordName", out var name)
                            ? name.ToString()
                            : "Unknown";
                        var difficulty = searchResult.Metadata.TryGetValue("difficulty", out var diff)
                            ? diff.ToString()
                            : "Unknown";
                        var fretSpan = searchResult.Metadata.TryGetValue("fretSpan", out var span)
                            ? span.ToString()
                            : "?";

                        table.AddRow(
                            (i + 1).ToString(),
                            chordName ?? "Unknown",
                            $"{searchResult.Score:F2}",
                            $"{difficulty} (span: {fretSpan})"
                        );
                    }

                    AnsiConsole.Write(table);
                }
            });
    }

    /// <summary>
    ///     Show index statistics
    /// </summary>
    private async Task ShowIndexStatistics()
    {
        var stats = semanticService.GetIndexStatistics();

        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");
        table.Border(TableBorder.Rounded);
        table.Title("[bold]Index Statistics[/]");

        table.AddRow("Total Documents", $"{stats.TotalDocuments:N0}");
        table.AddRow("Embedding Dimension", stats.EmbeddingDimension.ToString());

        foreach (var (category, count) in stats.DocumentsByCategory)
        {
            table.AddRow($"  {category}", $"{count:N0}");
        }

        AnsiConsole.Write(table);
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Get tuning by name
    /// </summary>
    private static Tuning GetTuningByName(string tuningName)
    {
        return tuningName.ToLower() switch
        {
            "standard" => Tuning.Default,
            "dropd" => new Tuning(PitchCollection.Parse("D2 A2 D3 G3 B3 E4")),
            "dadgad" => new Tuning(PitchCollection.Parse("D2 A2 D3 G3 A3 D4")),
            _ => Tuning.Default
        };
    }
}

/// <summary>
///     Options for semantic fretboard command
/// </summary>
public class SemanticFretboardOptions
{
    public bool ShouldIndex { get; set; }
    public string Tuning { get; set; } = "standard";
    public int MaxFret { get; set; } = 12;
    public string? Query { get; set; }
    public bool Interactive { get; set; }
}
