namespace GaCLI.Commands;
using System;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Musical.Explanation;
using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;

/// <summary>
/// Explains the musical characteristics of a voicing using its vector embedding.
/// </summary>
public class ExplainVoicingCommand(
    MongoDbService mongoDbService,
    VoicingExplanationService explanationService)
{
    public async Task ExecuteAsync(string diagram, bool verbose = false)
    {
        AnsiConsole.Write(
            new Rule("[bold cyan]AI Voicing Explainer[/]")
                .RuleStyle("cyan"));

        var normalizedDiagram = NormalizeDiagram(diagram);
        if (string.IsNullOrEmpty(normalizedDiagram))
        {
            AnsiConsole.MarkupLine("[red]Invalid diagram format.[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[dim]Analyzing:[/] [bold]{normalizedDiagram}[/]");

        // 1. Try to find in DB (to get existing embedding context)
        var filter = Builders<VoicingEntity>.Filter.Eq(v => v.Diagram, normalizedDiagram);
        var voicing = await mongoDbService.Voicings.Find(filter).FirstOrDefaultAsync();

        double[] embedding;

        if (voicing?.Embedding != null)
        {
             AnsiConsole.MarkupLine($"[dim]Using cached embedding from database (Length: {voicing.Embedding.Length}).[/]");
             embedding = voicing.Embedding;
        }
        else
        {
            AnsiConsole.MarkupLine("[dim]Generating on-the-fly embedding...[/]");
            if (voicing == null)
            {
                AnsiConsole.MarkupLine("[yellow]Voicing not found in database. Cannot explain without analysis data.[/]");
                return;
            }
            AnsiConsole.MarkupLine("[red]Embedding missing for this voicing.[/]");
            return;
        }

        // 2. Explain the embedding
        var explanation = explanationService.Explain(embedding);

        // 3. Display Results
        AnsiConsole.WriteLine();
        var panel = new Panel(new Markup($"[italic blue]{explanation.Summary}[/]"))
        {
            Header = new PanelHeader("[bold]Theory Breakdown[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(2, 1)
        };
        AnsiConsole.Write(panel);

        var table = new Table().Border(TableBorder.Rounded).Title("[bold]Symbolic Tags[/]");
        table.AddColumn("Category");
        table.AddColumn("Tags");

        table.AddRow("[green]Techniques[/]", string.Join(", ", explanation.Techniques.Select(t => $"[dim]{t}[/]")));
        table.AddRow("[blue]Styles/Lineage[/]", string.Join(", ", explanation.Styles.Select(t => $"[dim]{t}[/]")));

        AnsiConsole.Write(table);

        if (verbose)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[dim]Full Symbolic Vector (Active Bits):[/]");
            var symbolic = embedding.Skip(EmbeddingSchema.SymbolicOffset).Take(EmbeddingSchema.SymbolicDim).ToArray();
            for (int i = 0; i < symbolic.Length; i++)
            {
                if (symbolic[i] > 0) AnsiConsole.MarkupLine($"  [dim]Bit {i}:[/] [bold]1.0[/]");
            }
        }
    }

    private static string? NormalizeDiagram(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var cleaned = input.Trim().ToLower().Replace(" ", "-");
        if (cleaned.Contains('-')) return cleaned;
        if (cleaned.Length == 6 && cleaned.All(c => c == 'x' || char.IsDigit(c)))
            return string.Join("-", cleaned.Select(c => c.ToString()));
        return null;
    }
}
