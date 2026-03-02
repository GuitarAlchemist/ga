namespace GaDataCLI;

using System.Text.Json;
using System.Text.Json.Serialization;
using ChordTemplate = GA.Domain.Core.Theory.Harmony.ChordTemplate;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Services.Chords;
using GaDataCLI.Commands;
using Spectre.Console;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        // Parse command-line arguments
        var exportType = GetArgument(args, "--export", "-e");
        var outputPath = GetArgument(args, "--output", "-o") ?? "C:\\Users\\spare\\source\\repos\\ga\\Data\\Export";
        var quiet = HasFlag(args, "--quiet", "-q");

        if (!quiet)
        {
            AnsiConsole.Write(
                new FigletText("GA Data CLI")
                    .Centered()
                    .Color(Color.Blue));

            AnsiConsole.MarkupLine("[dim]Guitar Alchemist Data Export Tool[/]");
            AnsiConsole.WriteLine();
        }

        // Show help if requested
        if (HasFlag(args, "--help", "-h"))
        {
            ShowHelp();
            return 0;
        }

        if (HasFlag(args, "--benchmark"))
        {
            return await BenchmarkCommand.ExecuteAsync(args);
        }

        if (HasFlag(args, "--test"))
        {
            return await TestCommand.ExecuteAsync(args);
        }

        if (HasFlag(args, "--import"))
        {
            return await RunImport(args);
        }

        if (HasFlag(args, "--embed", "--embedding", "--embeddings"))
        {
            return await RunEmbeddings(args);
        }

        // Interactive mode if no export type specified
        string choice;
        if (string.IsNullOrEmpty(exportType))
        {
            choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[green]What data would you like to export?[/]")
                    .PageSize(10)
                    .AddChoices("All Chords", "Chord Templates", "Exit"));

            if (choice == "Exit")
            {
                AnsiConsole.MarkupLine("[yellow]Goodbye![/]");
                return 0;
            }

            outputPath = AnsiConsole.Ask<string>(
                "[green]Output directory path:[/]",
                outputPath);
        }
        else
        {
            // Map command-line argument to choice
            choice = exportType.ToLower() switch
            {
                "chords" or "all-chords" => "All Chords",
                "templates" or "chord-templates" => "Chord Templates",
                _ => throw new ArgumentException($"Invalid export type: {exportType}. Use 'chords' or 'templates'.")
            };

            if (!quiet)
            {
                AnsiConsole.MarkupLine($"[dim]Export type: {choice}[/]");
                AnsiConsole.MarkupLine($"[dim]Output path: {outputPath}[/]");
                AnsiConsole.WriteLine();
            }
        }

        // Create output directory if it doesn't exist
        Directory.CreateDirectory(outputPath);

        try
        {
            if (quiet)
            {
                // Non-interactive mode - no status display
                switch (choice)
                {
                    case "All Chords":
                        await ExportAllChordsQuiet(outputPath);
                        break;
                    case "Chord Templates":
                        await ExportChordTemplatesQuiet(outputPath);
                        break;
                }
            }
            else
            {
                // Interactive mode with status display
                await AnsiConsole.Status()
                    .StartAsync("[yellow]Generating data...[/]", async ctx =>
                    {
                        switch (choice)
                        {
                            case "All Chords":
                                await ExportAllChords(outputPath, ctx);
                                break;
                            case "Chord Templates":
                                await ExportChordTemplates(outputPath, ctx);
                                break;
                        }
                    });

                AnsiConsole.MarkupLine($"[green]✓[/] Data exported successfully to: [blue]{outputPath}[/]");
            }

            return 0;
        }
        catch (Exception ex)
        {
            if (quiet)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Error:[/] {ex.Message}");
                AnsiConsole.WriteException(ex);
            }

            return 1;
        }
    }

    private static void ShowHelp()
    {
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Usage:");
        AnsiConsole.WriteLine("  GaDataCLI [options]");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Options:");
        AnsiConsole.MarkupLine("  [blue]-e, --export <type>[/]     Export type: 'chords' or 'templates'");
        AnsiConsole.MarkupLine("  [blue]-o, --output <path>[/]     Output directory path");
        AnsiConsole.MarkupLine("  [blue]-q, --quiet[/]             Quiet mode (no interactive UI)");
        AnsiConsole.MarkupLine("  [blue]-h, --help[/]              Show this help message");
        AnsiConsole.MarkupLine("  [blue]--import[/]                Import exported chords into MongoDB");
        AnsiConsole.MarkupLine("  [blue]--data <file>[/]           Import file path (default: C:\\Temp\\GaExport\\all-chords.json)");
        AnsiConsole.MarkupLine("  [blue]--connection <conn>[/]     MongoDB connection string (default: mongodb://localhost:27017)");
        AnsiConsole.MarkupLine("  [blue]--database <name>[/]       MongoDB database name (default: guitaralchemist)");
        AnsiConsole.MarkupLine("  [blue]--collection <name>[/]     MongoDB collection name (default: chords)");
        AnsiConsole.MarkupLine("  [blue]-f, --force[/]             Drop collection before import");
        AnsiConsole.MarkupLine("  [blue]--embed, --embedding, --embeddings[/] Generate embeddings in MongoDB");
        AnsiConsole.MarkupLine("  [blue]--api-key <key>[/]         OpenAI API key (required unless using --base-url)");
        AnsiConsole.MarkupLine("  [blue]--model <name>[/]          Embedding model (default: text-embedding-3-small)");
        AnsiConsole.MarkupLine("  [blue]--batch-size <n>[/]        Embedding batch size (default: 100)");
        AnsiConsole.MarkupLine("  [blue]--base-url <url>[/]        OpenAI-compatible endpoint for local models");
        AnsiConsole.MarkupLine("  [blue]--benchmark[/]             Run vector search benchmark");
        AnsiConsole.MarkupLine("  [blue]--test[/]                  Run chatbot integration tests");
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine("Examples:");
        AnsiConsole.MarkupLine("  [dim]# Interactive mode[/]");
        AnsiConsole.WriteLine("  GaDataCLI");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Export all chords to default location[/]");
        AnsiConsole.WriteLine("  GaDataCLI --export chords");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Export chord templates to custom location[/]");
        AnsiConsole.WriteLine("  GaDataCLI -e templates -o C:\\Data\\Export");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Quiet mode for automation/scripting[/]");
        AnsiConsole.WriteLine("  GaDataCLI -e chords -o C:\\Data\\Export -q");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Import exported chords into MongoDB[/]");
        AnsiConsole.WriteLine("  GaDataCLI --import --data C:\\Temp\\GaExport\\all-chords.json");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Generate embeddings using OpenAI[/]");
        AnsiConsole.WriteLine("  GaDataCLI --embed --api-key <key> --collection chords");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Generate embeddings using a local endpoint[/]");
        AnsiConsole.WriteLine("  GaDataCLI --embed --base-url http://localhost:11434/v1 --model nomic-embed-text");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("  [dim]# Run benchmarks or tests[/]");
        AnsiConsole.WriteLine("  GaDataCLI --benchmark");
        AnsiConsole.WriteLine("  GaDataCLI --test");
        AnsiConsole.WriteLine();
    }

    private static string? GetArgument(string[] args, params string[] flags)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (flags.Contains(args[i], StringComparer.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static int GetIntArgument(string[] args, int defaultValue, params string[] flags)
    {
        var value = GetArgument(args, flags);
        return value != null && int.TryParse(value, out var parsed) ? parsed : defaultValue;
    }

    private static bool HasFlag(string[] args, params string[] flags) => args.Any(arg => flags.Contains(arg, StringComparer.OrdinalIgnoreCase));

    private static async Task<int> RunImport(string[] args)
    {
        var dataFile = GetArgument(args, "--data", "--data-file");
        var connectionString = GetArgument(args, "--connection", "--conn") ?? "mongodb://localhost:27017";
        var databaseName = GetArgument(args, "--database", "--db") ?? "guitaralchemist";
        var collectionName = GetArgument(args, "--collection", "--col") ?? "chords";
        var force = HasFlag(args, "--force", "-f");

        return await ImportCommand.ExecuteAsync(dataFile, connectionString, databaseName, collectionName, force);
    }

    private static async Task<int> RunEmbeddings(string[] args)
    {
        var connectionString = GetArgument(args, "--connection", "--conn") ?? "mongodb://localhost:27017";
        var databaseName = GetArgument(args, "--database", "--db") ?? "guitaralchemist";
        var collectionName = GetArgument(args, "--collection", "--col") ?? "chords";
        var apiKey = GetArgument(args, "--api-key", "--key") ?? string.Empty;
        var model = GetArgument(args, "--model") ?? "text-embedding-3-small";
        var batchSize = GetIntArgument(args, 100, "--batch-size");
        var baseUrl = GetArgument(args, "--base-url");

        return await EmbeddingCommand.ExecuteAsync(
            connectionString,
            databaseName,
            collectionName,
            apiKey,
            model,
            batchSize,
            useOpenAi: true,
            baseUrl: baseUrl);
    }

    private static async Task ExportAllChords(string outputPath, StatusContext ctx)
    {
        ctx.Status("[yellow]Generating all possible chords...[/]");

        var allChords = ChordTemplateFactory.GenerateAllPossibleChords().ToList();

        ctx.Status($"[yellow]Processing {allChords.Count} chords...[/]");

        var chordData = BuildChordData(allChords);

        ctx.Status("[yellow]Writing to JSON file...[/]");

        await WriteJsonFile(outputPath, "all-chords.json", chordData);

        AnsiConsole.MarkupLine($"[dim]  → Exported {chordData.Count} chords to all-chords.json[/]");
    }

    private static async Task ExportAllChordsQuiet(string outputPath)
    {
        var allChords = ChordTemplateFactory.GenerateAllPossibleChords().ToList();
        var chordData = BuildChordData(allChords);
        await WriteJsonFile(outputPath, "all-chords.json", chordData);
        Console.WriteLine($"Exported {chordData.Count} chords to {Path.Combine(outputPath, "all-chords.json")}");
    }

    private static List<object> BuildChordData(List<ChordTemplate> allChords) =>
        [.. allChords.Select((chord, index) => new
        {
            Id = index + 1,
            chord.Name,
            Quality = chord.Quality.ToString(),
            Extension = chord.Extension.ToString(),
            StackingType = chord.StackingType.ToString(),
            chord.NoteCount,
            Intervals = chord.Formula.Intervals.Select(i => new
            {
                Semitones = i.Interval.Semitones.Value,
                Function = i.Function.ToString(),
                i.IsEssential
            }).ToList(),
            PitchClassSet = chord.PitchClassSet.ToList().Select(pc => pc.Value).ToList(),
            ParentScale = chord.GetParentScale()?.Name,
            ScaleDegree = chord.GetScaleDegree(),
            Description = chord.GetDescription(),
            ConstructionType = chord.GetConstructionType()
        })];

    private static async Task ExportChordTemplates(string outputPath, StatusContext ctx)
    {
        ctx.Status("[yellow]Generating chord templates...[/]");

        var templates = ChordTemplateRegistry.GetAllTemplates().ToList();

        ctx.Status($"[yellow]Processing {templates.Count} templates...[/]");

        var templatesByPitchClass = BuildTemplateData(templates);

        ctx.Status("[yellow]Writing to JSON file...[/]");

        await WriteJsonFile(outputPath, "chord-templates.json", templatesByPitchClass);

        AnsiConsole.MarkupLine(
            $"[dim]  → Exported {templates.Count} templates ({templatesByPitchClass.Count} unique pitch class sets) to chord-templates.json[/]");
    }

    private static async Task ExportChordTemplatesQuiet(string outputPath)
    {
        var templates = ChordTemplateRegistry.GetAllTemplates().ToList();
        var templatesByPitchClass = BuildTemplateData(templates);
        await WriteJsonFile(outputPath, "chord-templates.json", templatesByPitchClass);
        Console.WriteLine(
            $"Exported {templates.Count} templates ({templatesByPitchClass.Count} unique pitch class sets) to {Path.Combine(outputPath, "chord-templates.json")}");
    }

    private static List<object> BuildTemplateData(List<ChordTemplate> templates) =>
        [.. templates
            .GroupBy(t => string.Join(",", t.PitchClassSet.ToList().Select(pc => pc.Value).OrderBy(v => v)))
            .Select(g => new
            {
                PitchClassSet = g.Key.Split(',').Select(int.Parse).ToList(),
                Templates = g.Select(t => new
                {
                    t.Name,
                    Quality = t.Quality.ToString(),
                    Extension = t.Extension.ToString(),
                    StackingType = t.StackingType.ToString(),
                    t.NoteCount,
                    Intervals = t.Formula.Intervals.Select(i => new
                    {
                        Semitones = i.Interval.Semitones.Value,
                        Function = i.Function.ToString()
                    }).ToList(),
                    ParentScale = t.GetParentScale()?.Name,
                    ScaleDegree = t.GetScaleDegree()
                }).ToList()
            })];

    private static async Task WriteJsonFile(string outputPath, string fileName, object data)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var outputFile = Path.Combine(outputPath, fileName);
        var json = JsonSerializer.Serialize(data, options);
        await File.WriteAllTextAsync(outputFile, json);
    }
}
