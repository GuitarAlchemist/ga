using BenchmarkDotNet.Running;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Attributes;
using Spectre.Console;
using System;
using System.IO;
using System.Linq;
using System.Reflection;

public class Program
{
    public static int Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help"))
        {
            AnsiConsole.MarkupLine("[bold]GA.Domain.Core Benchmarks[/]");
            AnsiConsole.MarkupLine("Usage:");
            AnsiConsole.MarkupLine("  --run-all          Run all benchmarks");
            AnsiConsole.MarkupLine("  --help             Show this help");
            AnsiConsole.MarkupLine("");
            AnsiConsole.MarkupLine("Available benchmarks:");
            
            var benchmarkType = typeof(DomainCoreBenchmarks);
            var methods = benchmarkType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                .Where(m => m.GetCustomAttributes(typeof(BenchmarkAttribute), false).Length > 0)
                .ToList();

            foreach (var method in methods)
            {
                var name = method.Name;
                AnsiConsole.MarkupLine($"  - [green]{name}[/]");
            }
            return 0;
        }

        if (args.Contains("--run-all"))
        {
            AnsiConsole.MarkupLine("[yellow]Running all benchmarks...[/]");
            var config = new BenchmarkDotNet.Configs.ManualConfig()
                .WithArtifactsPath(Path.Combine(Directory.GetCurrentDirectory(), "BenchmarkResults"))
                .WithSummaryStyle(SummaryStyle.Default);

            var summary = BenchmarkRunner.Run<DomainCoreBenchmarks>(config);
            AnsiConsole.MarkupLine($"[green]Benchmarks completed! Results saved to {config.ArtifactsPath}[/]");
            return 0;
        }

        AnsiConsole.MarkupLine("[red]Invalid arguments. Use --help for usage.[/]");
        return 1;
    }
}