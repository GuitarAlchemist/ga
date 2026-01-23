namespace GaDataCLI.Commands;

using Spectre.Console;
using System.Diagnostics;

public class BenchmarkCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        AnsiConsole.MarkupLine("[bold cyan]Running Vector Search Benchmark...[/]");
        
        // Locate the Benchmark project path
        // Ascend from Tools/GaDataCLI/bin/Debug/net10.0 to root
        var currentDir = Directory.GetCurrentDirectory();
        
        // This is a naive search, assuming standard repo layout
        var projectPath = Path.Combine(currentDir, "..", "..", "..", "..", "Demos", "Performance", "VectorSearchBenchmark");
        projectPath = Path.GetFullPath(projectPath);

        if (!Directory.Exists(projectPath))
        {
             // Fallback search
             projectPath = Path.Combine(currentDir, "Demos", "Performance", "VectorSearchBenchmark");
             if (!Directory.Exists(projectPath))
             {
                 AnsiConsole.MarkupLine($"[red]Error: Could not locate VectorSearchBenchmark project at {projectPath}[/]");
                 return 1;
             }
        }

        AnsiConsole.MarkupLine($"[dim]Project path: {projectPath}[/]");

        // Pass through any additional arguments to the benchmark
        // Note: The benchmark project might not accept args yet, but good for future proofing
        var arguments = $"run --project \"{projectPath}\"";

        return await RunProcessAsync("dotnet", arguments);
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        
        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) Console.Error.WriteLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return process.ExitCode;
    }
}

public class TestCommand
{
    public static async Task<int> ExecuteAsync(string[] args)
    {
        AnsiConsole.MarkupLine("[bold cyan]Running Chatbot Integration Tests...[/]");

        // Locate the Test project path
        var currentDir = Directory.GetCurrentDirectory();
        var projectPath = Path.Combine(currentDir, "..", "..", "..", "..", "Tests", "Apps", "GuitarAlchemistChatbot.Tests");
        projectPath = Path.GetFullPath(projectPath);

        if (!Directory.Exists(projectPath))
        {       
             // Fallback search from repo root if running from there
             projectPath = Path.Combine(currentDir, "Tests", "Apps", "GuitarAlchemistChatbot.Tests");
             if (!Directory.Exists(projectPath))
             {
                 AnsiConsole.MarkupLine($"[red]Error: Could not locate GuitarAlchemistChatbot.Tests project at {projectPath}[/]");
                 return 1;
             }
        }

        AnsiConsole.MarkupLine($"[dim]Project path: {projectPath}[/]");

        var arguments = $"test \"{projectPath}\"";
        
        return await RunProcessAsync("dotnet", arguments);
    }

    private static async Task<int> RunProcessAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        
        process.OutputDataReceived += (sender, e) => 
        {
            if (e.Data != null) Console.WriteLine(e.Data);
        };
        process.ErrorDataReceived += (sender, e) => 
        {
            if (e.Data != null) Console.Error.WriteLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync();

        return process.ExitCode;
    }
}
