namespace GaCLI.Commands;

using GA.Business.ML.AI.Benchmarks;
using Spectre.Console;
using System.Threading.Tasks;
using System.Linq;

public class RunBenchmarksCommand(BenchmarkRunner runner)
{
    public async Task ExecuteAsync(string? benchmarkName = null)
    {
        AnsiConsole.Write(new FigletText("GA Benchmarks").Color(Color.Purple));

        if (benchmarkName != null)
        {
            var result = await runner.RunByNameAsync(benchmarkName);
            if (result == null)
            {
                AnsiConsole.MarkupLine($"[red]Benchmark '{benchmarkName}' not found.[/]");
                return;
            }
            DisplayResult(result);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Running all registered benchmarks...[/]");
            var results = await runner.RunAllAsync();
            foreach (var result in results)
            {
                DisplayResult(result);
            }
        }
    }

    private void DisplayResult(GA.Business.Core.AI.Benchmarks.BenchmarkResult result)
    {
        var color = result.Passed ? "green" : "red";
        AnsiConsole.MarkupLine($"\n[bold]{result.Name}[/] - Score: [{color}]{result.Score:P0}[/] ({result.Duration.TotalMilliseconds:F0}ms)");

        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Step");
        table.AddColumn("Expected");
        table.AddColumn("Actual");
        table.AddColumn("Score");
        table.AddColumn("Notes");

        foreach (var step in result.Steps)
        {
            var stepColor = step.Passed ? "green" : "red";
            table.AddRow(
                step.Name,
                step.Expected,
                step.Actual,
                $"[{stepColor}]{step.Score:P0}[/]",
                step.Notes ?? ""
            );
        }

        AnsiConsole.Write(table);
    }
}
