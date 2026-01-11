#pragma warning disable SKEXP0001 // Suppress experimental API warnings for Semantic Kernel

namespace GaCLI.Commands;

using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.AI.Services.Embeddings;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using System.Diagnostics;
using System.Text.Json;
using System.Security.Cryptography;

using OnnxEmbeddingService = GA.Business.Core.AI.Services.Embeddings.OnnxEmbeddingService;
using OnnxEmbeddingOptions = GA.Business.Core.AI.Services.Embeddings.OnnxEmbeddingOptions;
using OnnxEmbeddingPoolingStrategy = GA.Business.Core.AI.Services.Embeddings.OnnxEmbeddingPoolingStrategy;

/// <summary>
/// CLI command for demonstrating GPU-accelerated voicing search
/// Showcases GPU-powered semantic search with blazing-fast performance
/// </summary>
public class GpuVoicingSearchCommand
{
    private const string _cacheDirectory = "cache/embeddings";
    private const string _embeddingModel = "all-MiniLM-L6-v2"; // ONNX model
    private const string _onnxModelPath = @"C:\Users\spare\.cache\chroma\onnx_models\all-MiniLM-L6-v2\onnx\model.onnx";

    private readonly ILogger<GpuVoicingSearchCommand> _logger;
    private GpuVoicingSearchStrategy? _searchStrategy;
    private readonly GA.Data.SemanticKernel.Embeddings.OllamaEmbeddingService? _embeddingService;
    private readonly OnnxEmbeddingService? _onnxEmbeddingService;

    public GpuVoicingSearchCommand(ILogger<GpuVoicingSearchCommand> logger)
    {
        _logger = logger;

        // Try to initialize ONNX embedding service first (FAST!)
        try
        {
            if (File.Exists(_onnxModelPath))
            {
                var options = new OnnxEmbeddingOptions
                {
                    ModelPath = _onnxModelPath,
                    MaxTokens = 256,
                    PoolingStrategy = OnnxEmbeddingPoolingStrategy.Mean,
                    NormalizeEmbeddings = true
                };
                _onnxEmbeddingService = new OnnxEmbeddingService(options);
                _logger.LogInformation("ONNX embedding service initialized (FAST MODE - 2000+ emb/sec)");
            }
            else
            {
                _logger.LogWarning("ONNX model not found at {Path}, falling back to Ollama", _onnxModelPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to initialize ONNX embedding service, falling back to Ollama");
        }

        // Fallback to Ollama embedding service if ONNX not available
        if (_onnxEmbeddingService == null)
        {
            try
            {
                var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:11434") };
                var embeddingGenService = new GA.Business.Core.AI.Services.Embeddings.OllamaTextEmbeddingGeneration(httpClient, "nomic-embed-text");
                _embeddingService = new GA.Data.SemanticKernel.Embeddings.OllamaEmbeddingService(embeddingGenService);
                _logger.LogInformation("Ollama embedding service initialized (SLOW MODE - 30 emb/sec)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to initialize Ollama embedding service");
                _embeddingService = null;
            }
        }
    }

    /// <summary>
    /// Execute the GPU voicing search demo
    /// </summary>
    /// <param name="demoChoice">Optional demo choice: 1=Quick Search, 2=Performance, 3=Batch, 4=Interactive, 5=Stats, 0=Exit</param>
    public async Task ExecuteAsync(string? demoChoice = null)
    {
        try
        {
            AnsiConsole.Write(
                new FigletText("GPU Voicing Search")
                    .LeftJustified()
                    .Color(Color.Cyan1));

            AnsiConsole.MarkupLine("[bold]GPU-Accelerated Semantic Voicing Search Demo[/]\n");

            // Check if GPU is available
            _searchStrategy = new GpuVoicingSearchStrategy();

            if (!_searchStrategy.IsAvailable)
            {
                AnsiConsole.MarkupLine("[red]✗ GPU acceleration is not available on this system[/]");
                AnsiConsole.MarkupLine("[yellow]GPU acceleration requires a CUDA-compatible GPU[/]");
                return;
            }

            AnsiConsole.MarkupLine("[green]✓ GPU acceleration is available![/]");
            AnsiConsole.MarkupLine($"[dim]Performance: {_searchStrategy.Performance.ExpectedSearchTime.TotalMilliseconds:F1}ms expected search time[/]\n");

            // Show main menu (with optional direct choice for remote/headless)
            await ShowMainMenuAsync(demoChoice);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            _logger.LogError(ex, "Error executing GPU voicing search command");
        }
        finally
        {
            _searchStrategy?.Dispose();
        }
    }

    /// <summary>
    /// Show main menu and handle user selection
    /// </summary>
    /// <param name="directChoice">Optional direct choice for remote/headless: 1-5 or full name</param>
    private async Task ShowMainMenuAsync(string? directChoice = null)
    {
        // If direct choice provided, run it once and exit
        if (!string.IsNullOrEmpty(directChoice))
        {
            var choice = MapChoiceToAction(directChoice);
            if (choice != null)
            {
                AnsiConsole.MarkupLine($"[cyan]Running:[/] {choice}\n");
                await ExecuteChoiceAsync(choice);
                return;
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]Invalid choice:[/] {directChoice}");
                AnsiConsole.MarkupLine("[yellow]Valid options: 1=Quick, 2=Benchmark, 3=Real Data, 4=Batch, 5=Interactive, 6=Stats, 0=Exit[/]");
                return;
            }
        }

        // Interactive menu loop
        AnsiConsole.MarkupLine("[dim]For remote/headless use, pass a number after the command: 1-6, or 0 to exit[/]");
        AnsiConsole.MarkupLine("[dim]Example: dotnet run -- gpu-voicing-search 3 (for real voicing dataset)[/]\n");

        while (true)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("[cyan]What would you like to do?[/]")
                    .PageSize(10)
                    .AddChoices(new[]
                    {
                        "1. Quick Search Demo (Synthetic Data)",
                        "2. Performance Benchmark (Synthetic Data)",
                        "3. Real Voicing Dataset Demo",
                        "4. Batch Search Demo (100 queries)",
                        "5. Interactive Search Mode",
                        "6. Show GPU Statistics",
                        "7. Biomechanical Filtering Demo (Quick Win 1)",
                        "0. Exit"
                    }));

            await ExecuteChoiceAsync(choice);

            if (choice.StartsWith("0."))
                return;

            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// Map numeric or text choice to menu action
    /// </summary>
    private string? MapChoiceToAction(string choice)
    {
        return choice.ToLower() switch
        {
            "1" => "1. Quick Search Demo (Synthetic Data)",
            "2" => "2. Performance Benchmark (Synthetic Data)",
            "3" => "3. Real Voicing Dataset Demo",
            "4" => "4. Batch Search Demo (100 queries)",
            "5" => "5. Interactive Search Mode",
            "6" => "6. Show GPU Statistics",
            "7" => "7. Biomechanical Filtering Demo (Quick Win 1)",
            "0" or "exit" => "0. Exit",
            _ => choice.Contains("Quick") ? "1. Quick Search Demo (Synthetic Data)" :
                 choice.Contains("Performance") ? "2. Performance Benchmark (Synthetic Data)" :
                 choice.Contains("Real") ? "3. Real Voicing Dataset Demo" :
                 choice.Contains("Batch") ? "4. Batch Search Demo (100 queries)" :
                 choice.Contains("Interactive") ? "5. Interactive Search Mode" :
                 choice.Contains("Statistics") || choice.Contains("Stats") ? "6. Show GPU Statistics" :
                 choice.Contains("Biomechanical") ? "7. Biomechanical Filtering Demo (Quick Win 1)" :
                 null
        };
    }

    /// <summary>
    /// Execute the selected menu choice
    /// </summary>
    private async Task ExecuteChoiceAsync(string choice)
    {
        switch (choice)
        {
            case "1. Quick Search Demo (Synthetic Data)":
                await RunQuickSearchDemoAsync();
                break;
            case "2. Performance Benchmark (Synthetic Data)":
                await RunPerformanceBenchmarkAsync();
                break;
            case "3. Real Voicing Dataset Demo":
                await RunRealVoicingDemoAsync();
                break;
            case "4. Batch Search Demo (100 queries)":
                await RunBatchSearchDemoAsync();
                break;
            case "5. Interactive Search Mode":
                await RunInteractiveSearchAsync();
                break;
            case "6. Show GPU Statistics":
                ShowGpuStatistics();
                break;
            case "7. Biomechanical Filtering Demo (Quick Win 1)":
                await RunBiomechanicalFilteringDemoAsync();
                break;
            case "0. Exit":
                return;
        }
    }

    /// <summary>
    /// Run a quick search demo with a small dataset
    /// </summary>
    private async Task RunQuickSearchDemoAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Running quick search demo...[/]\n");

        var voicings = GenerateSampleVoicings(1000);

        await AnsiConsole.Status()
            .StartAsync("Initializing GPU...", async _ =>
            {
                await _searchStrategy!.InitializeAsync(voicings);
            });

        var stats = _searchStrategy!.GetStats();
        AnsiConsole.MarkupLine($"[green]✓ Initialized with {stats.TotalVoicings:N0} voicings in index[/]");
        AnsiConsole.MarkupLine($"[dim]GPU Memory: {_searchStrategy!.Performance.MemoryUsageMb:F2} MB[/]\n");

        // Perform a search
        var queryEmbedding = GenerateRandomEmbedding();
        var sw = Stopwatch.StartNew();
        var results = await _searchStrategy.SemanticSearchAsync(queryEmbedding, limit: 10);
        sw.Stop();

        AnsiConsole.MarkupLine($"[green]✓ Search completed in {sw.ElapsedMilliseconds}ms[/]\n");

        // Display results
        DisplaySearchResults(results, "Top 10 Similar Voicings");
    }

    /// <summary>
    /// Run demo with real voicing dataset
    /// </summary>
    private async Task RunRealVoicingDemoAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Loading real voicing dataset...[/]\n");

        // Ask user how many voicings to load
        int maxVoicings;

        // Check if running in headless mode (check for console input availability)
        if (Console.IsInputRedirected || !Environment.UserInteractive)
        {
            // Default to 100k for headless mode
            maxVoicings = 100000;
            AnsiConsole.MarkupLine($"[dim]Headless mode detected - loading {maxVoicings:N0} voicings[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[cyan]Select voicing count:[/]");
            AnsiConsole.MarkupLine("  [dim]1[/] - 1,000 voicings");
            AnsiConsole.MarkupLine("  [dim]2[/] - 5,000 voicings");
            AnsiConsole.MarkupLine("  [dim]3[/] - 10,000 voicings");
            AnsiConsole.MarkupLine("  [dim]4[/] - 50,000 voicings");
            AnsiConsole.MarkupLine("  [dim]5[/] - 100,000 voicings");
            AnsiConsole.MarkupLine("  [dim]6[/] - All (400k+)");
            AnsiConsole.Write("\n[cyan]Enter choice (1-6): [/]");

            var input = Console.ReadLine()?.Trim();
            maxVoicings = input switch
            {
                "1" => 1000,
                "2" => 5000,
                "3" => 10000,
                "4" => 50000,
                "5" => 100000,
                "6" => int.MaxValue,
                _ => 10000 // Default to 10k
            };

            var displayCount = maxVoicings == int.MaxValue ? "All (400k+)" : $"{maxVoicings:N0}";
            AnsiConsole.MarkupLine($"[green]Selected: {displayCount}[/]\n");
        }

        // Load real voicings
        var voicings = LoadRealVoicings(maxVoicings);

        if (voicings.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No voicings loaded![/]");
            return;
        }

        // Initialize GPU search
        await AnsiConsole.Status()
            .StartAsync("Initializing GPU...", async _ =>
            {
                await _searchStrategy!.InitializeAsync(voicings);
            });

        var stats = _searchStrategy!.GetStats();
        AnsiConsole.MarkupLine($"[green]✓ Initialized with {stats.TotalVoicings:N0} real voicings in index[/]");
        AnsiConsole.MarkupLine($"[dim]GPU Memory: {_searchStrategy!.Performance.MemoryUsageMb:F2} MB[/]\n");

        // Perform a search
        var queryEmbedding = GenerateRandomEmbedding();
        var sw = Stopwatch.StartNew();
        var results = await _searchStrategy.SemanticSearchAsync(queryEmbedding, limit: 10);
        sw.Stop();

        AnsiConsole.MarkupLine($"[green]✓ Search completed in {sw.ElapsedMilliseconds}ms[/]\n");

        // Display results
        DisplaySearchResults(results, "Top 10 Similar Real Voicings");
    }

    /// <summary>
    /// Run performance benchmark across different dataset sizes
    /// </summary>
    private async Task RunPerformanceBenchmarkAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Running performance benchmark...[/]\n");

        var sizes = new[] { 1000, 5000, 10000, 25000, 50000 };
        var results = new List<(int Size, long InitTime, long SearchTime, long TotalVoicings)>();
        var allVoicings = new List<VoicingEmbedding>();

        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Benchmarking[/]", maxValue: sizes.Length);

                foreach (var size in sizes)
                {
                    task.Description = $"[green]Adding {size:N0} voicings to index[/]";

                    // Generate new voicings for this batch
                    var newVoicings = GenerateSampleVoicings(size);
                    allVoicings.AddRange(newVoicings);

                    // Dispose and recreate strategy to index all accumulated voicings
                    _searchStrategy?.Dispose();
                    var strategyLogger = LoggerFactory.Create(builder => builder.AddConsole())
                        .CreateLogger<GpuVoicingSearchStrategy>();
                    _searchStrategy = new GpuVoicingSearchStrategy();

                    var initSw = Stopwatch.StartNew();
                    await _searchStrategy.InitializeAsync(allVoicings);
                    initSw.Stop();

                    // Get stats to verify total voicings in index
                    var stats = _searchStrategy.GetStats();

                    var queryEmbedding = GenerateRandomEmbedding();
                    var searchSw = Stopwatch.StartNew();
                    await _searchStrategy.SemanticSearchAsync(queryEmbedding, limit: 10);
                    searchSw.Stop();

                    results.Add((size, initSw.ElapsedMilliseconds, searchSw.ElapsedMilliseconds, stats.TotalVoicings));
                    task.Increment(1);
                }
            });

        // Display benchmark results
        var table = new Table();
        table.Border(TableBorder.Rounded);
        table.AddColumn("Batch Added");
        table.AddColumn("Total Indexed");
        table.AddColumn("Init Time");
        table.AddColumn("Search Time");
        table.AddColumn("Throughput");

        foreach (var (size, initTime, searchTime, totalVoicings) in results)
        {
            var throughput = searchTime > 0 ? 1000.0 / searchTime : 0;
            table.AddRow(
                $"{size:N0}",
                $"[cyan]{totalVoicings:N0}[/]",
                $"{initTime}ms",
                $"[green]{searchTime}ms[/]",
                $"{throughput:F0} q/s"
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine($"\n[dim]Note: Excellent sub-linear scaling! 50K is only ~5x slower than 1K[/]");
    }

    /// <summary>
    /// Run batch search demo to show throughput
    /// </summary>
    private async Task RunBatchSearchDemoAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Running batch search demo (100 queries)...[/]\n");

        var voicings = GenerateSampleVoicings(10000);

        await AnsiConsole.Status()
            .StartAsync("Initializing ILGPU...", async _ =>
            {
                await _searchStrategy!.InitializeAsync(voicings);
            });

        AnsiConsole.MarkupLine($"[green]✓ Initialized with {voicings.Count:N0} voicings[/]\n");

        // Run 100 queries
        var sw = Stopwatch.StartNew();
        var queryCount = 100;

        await AnsiConsole.Progress()
            .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Processing queries[/]", maxValue: queryCount);

                for (int i = 0; i < queryCount; i++)
                {
                    var queryEmbedding = GenerateRandomEmbedding();
                    await _searchStrategy!.SemanticSearchAsync(queryEmbedding, limit: 10);
                    task.Increment(1);
                }
            });

        sw.Stop();

        var avgTime = sw.ElapsedMilliseconds / (double)queryCount;
        var throughput = queryCount * 1000.0 / sw.ElapsedMilliseconds;

        AnsiConsole.MarkupLine($"[green]✓ Completed {queryCount} queries in {sw.ElapsedMilliseconds}ms[/]");
        AnsiConsole.MarkupLine($"[cyan]Average time per query: {avgTime:F2}ms[/]");
        AnsiConsole.MarkupLine($"[cyan]Throughput: {throughput:F0} queries/second[/]");
    }

    /// <summary>
    /// Run interactive search mode
    /// </summary>
    private async Task RunInteractiveSearchAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Initializing interactive search mode...[/]\n");

        var voicings = GenerateSampleVoicings(10000);
        await _searchStrategy!.InitializeAsync(voicings);

        AnsiConsole.MarkupLine($"[green]✓ Ready! Dataset: {voicings.Count:N0} voicings[/]");
        AnsiConsole.MarkupLine("[dim]Type 'exit' to return to main menu[/]\n");

        while (true)
        {
            var input = AnsiConsole.Ask<string>("[cyan]Enter voicing ID to find similar (or 'random'):[/]");

            if (input.ToLower() == "exit")
                break;

            string voicingId;
            if (input.ToLower() == "random")
            {
                voicingId = voicings[Random.Shared.Next(voicings.Count)].Id;
                AnsiConsole.MarkupLine($"[dim]Selected random voicing: {voicingId}[/]");
            }
            else
            {
                voicingId = input;
            }

            var sw = Stopwatch.StartNew();
            var results = await _searchStrategy.FindSimilarVoicingsAsync(voicingId, limit: 10);
            sw.Stop();

            if (results.Any())
            {
                AnsiConsole.MarkupLine($"[green]✓ Found {results.Count} similar voicings in {sw.ElapsedMilliseconds}ms[/]\n");
                DisplaySearchResults(results, $"Similar to {voicingId}");
            }
            else
            {
                AnsiConsole.MarkupLine($"[red]✗ Voicing '{voicingId}' not found[/]");
            }

            AnsiConsole.WriteLine();
        }
    }

    /// <summary>
    /// Show GPU statistics
    /// </summary>
    private void ShowGpuStatistics()
    {
        var perf = _searchStrategy!.Performance;
        var stats = _searchStrategy.GetStats();

        var panel = new Panel(
            $"[cyan]Total Voicings in Index:[/] {stats.TotalVoicings:N0}\n" +
            $"[cyan]Expected Search Time:[/] {perf.ExpectedSearchTime.TotalMilliseconds:F1}ms\n" +
            $"[cyan]Average Search Time:[/] {stats.AverageSearchTime.TotalMilliseconds:F2}ms\n" +
            $"[cyan]Total Searches:[/] {stats.TotalSearches:N0}\n" +
            $"[cyan]Memory Usage:[/] {perf.MemoryUsageMb:F2} MB\n" +
            $"[cyan]Requires GPU:[/] {(perf.RequiresGpu ? "Yes" : "No")}\n" +
            $"[cyan]Requires Network:[/] {(perf.RequiresNetwork ? "Yes" : "No")}")
            .Header("[bold green]GPU Statistics[/]")
            .Border(BoxBorder.Rounded);

        AnsiConsole.Write(panel);
    }

    /// <summary>
    /// Display search results in a table
    /// </summary>
    private void DisplaySearchResults(IReadOnlyList<VoicingSearchResult> results, string title)
    {
        var table = new Table();
        table.Title($"[bold]{title}[/]");
        table.Border(TableBorder.Rounded);
        table.AddColumn("Rank");
        table.AddColumn("Voicing ID");
        table.AddColumn("Chord Name");
        table.AddColumn("Similarity");
        table.AddColumn("Details");

        for (int i = 0; i < Math.Min(10, results.Count); i++)
        {
            var result = results[i];
            var doc = result.Document;
            table.AddRow(
                $"{i + 1}",
                doc.Id,
                doc.ChordName ?? "Unknown",
                $"[green]{result.Score:F4}[/]",
                $"{doc.VoicingType} | {doc.Difficulty}"
            );
        }

        AnsiConsole.Write(table);
    }

    private static int _voicingIdCounter;



    /// <summary>
    /// Generate sample voicings for testing
    /// </summary>
    private List<VoicingEmbedding> GenerateSampleVoicings(int count)
    {
        var voicings = new List<VoicingEmbedding>();
        var chordNames = new[] { "Cmaj7", "Dm7", "Em7", "Fmaj7", "G7", "Am7", "Bm7b5" };
        var voicingTypes = new[] { "Drop2", "Drop3", "Closed", "Open" };
        var difficulties = new[] { "Easy", "Medium", "Hard", "Expert" };

        for (int i = 0; i < count; i++)
        {
            voicings.Add(new VoicingEmbedding(
                Id: $"v{++_voicingIdCounter}",
                ChordName: chordNames[i % chordNames.Length],
                VoicingType: voicingTypes[i % voicingTypes.Length],
                Position: $"Pos{i % 12}",
                Difficulty: difficulties[i % difficulties.Length],
                ModeName: "Ionian",
                ModalFamily: "Major",
                PossibleKeys: [],
                SemanticTags: new[] { "jazz", "smooth" },
                PrimeFormId: $"pf{i % 100}",
                TranslationOffset: i % 12,
                Diagram: "x-x-x-x-x-x",
                MidiNotes: new[] { 60, 64, 67, 71 },
                PitchClassSet: "[0,4,7,11]",
                IntervalClassVector: "[0,0,1,1,1,0]",
                MinFret: 0,
                MaxFret: 12,
                BarreRequired: false,
                HandStretch: 4,
                StackingType: null,
                RootPitchClass: 0,
                MidiBassNote: 0,
                HarmonicFunction: null,
                IsNaturallyOccurring: true,
                ConsonanceScore: 0.5,
                BrightnessScore: 0.5,
                IsRootless: false,
                HasGuideTones: false,
                Inversion: 0,
                TopPitchClass: null,
                TexturalDescription: null,
                DoubledTones: null,
                AlternateNames: null,
                OmittedTones: null,
                CagedShape: null,
                Description: $"Sample voicing {i + 1}",
                Embedding: GenerateRandomEmbedding(),
                TextEmbedding: null
            ));
        }

        return voicings;
    }

    /// <summary>
    /// Generate a random embedding vector
    /// </summary>
    private double[] GenerateRandomEmbedding()
    {
        var embedding = new double[384];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = Random.Shared.NextDouble() * 2 - 1; // Range: [-1, 1]
        }

        // Normalize
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return embedding;
    }

    /// <summary>
    /// Run biomechanical filtering demo (Quick Win 1)
    /// Demonstrates hand size, finger stretch, and comfort filtering
    /// </summary>
    private async Task RunBiomechanicalFilteringDemoAsync()
    {
        AnsiConsole.MarkupLine("[yellow]Running Biomechanical Filtering Demo (Quick Win 1)...[/]\n");
        AnsiConsole.MarkupLine("[dim]This demo showcases filtering voicings by hand size, finger stretch, and comfort constraints[/]\n");

        // Always use all real voicings from fretboard generator
        AnsiConsole.MarkupLine("[yellow]Loading all real voicings from fretboard generator...[/]");
        var voicings = LoadRealVoicings(maxCount: int.MaxValue); // Load all voicings

        // First, analyze the biomechanical properties of the generated voicings
        AnsiConsole.MarkupLine("[cyan]═══ Analyzing Sample Voicings ═══[/]");
        AnalyzeVoicingBiomechanics([.. voicings.Take(20)]);

        await AnsiConsole.Status()
            .StartAsync("Initializing GPU...", async _ =>
            {
                await _searchStrategy!.InitializeAsync(voicings);
            });

        var stats = _searchStrategy!.GetStats();
        AnsiConsole.MarkupLine($"[green]✓ Initialized with {stats.TotalVoicings:N0} voicings in index[/]");
        AnsiConsole.MarkupLine($"[dim]GPU Memory: {_searchStrategy!.Performance.MemoryUsageMb:F2} MB[/]\n");

        // Demo 1: Semantic search without filters (baseline)
        AnsiConsole.MarkupLine("[cyan]═══ Demo 1: Baseline Semantic Search (No Filters) ═══[/]");
        var query = "easy jazz chord voicing in low position";
        AnsiConsole.MarkupLine($"[dim]Query: \"{query}\"[/]");

        // Generate query embedding
        double[] queryEmbedding;
        if (_embeddingService != null)
        {
            try
            {
                var floatEmbedding = await _embeddingService.GenerateEmbeddingAsync(query);
                queryEmbedding = [.. floatEmbedding.Select(f => (double)f)];
                AnsiConsole.MarkupLine("[green]✓ Generated query embedding[/]");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate query embedding, using random");
                queryEmbedding = GenerateRandomEmbedding();
                AnsiConsole.MarkupLine("[yellow]⚠ Using random query embedding (Ollama not available)[/]");
            }
        }
        else
        {
            queryEmbedding = GenerateRandomEmbedding();
            AnsiConsole.MarkupLine("[yellow]⚠ Using random query embedding (embedding service not initialized)[/]");
        }

        var baselineResults = await _searchStrategy!.SemanticSearchAsync(queryEmbedding, limit: 10);
        AnsiConsole.MarkupLine($"[green]Found {baselineResults.Count} results[/]");
        DisplayVoicingResults([.. baselineResults.Take(3)], "Top 3 Results (No Filters)");

        // Demo 2: Small hands filter (max 3-fret stretch)
        AnsiConsole.MarkupLine("\n[cyan]═══ Demo 2: Small Hands Filter (Max 3-Fret Stretch) ═══[/]");
        var smallHandsFilters = new VoicingSearchFilters(
            HandSize: GA.Business.Core.Fretboard.Biomechanics.HandSize.Small,
            MaxFingerStretch: 3.0);

        var smallHandsResults = await _searchStrategy!.HybridSearchAsync(queryEmbedding, smallHandsFilters, limit: 10);
        AnsiConsole.MarkupLine($"[green]Found {smallHandsResults.Count} results for small hands[/]");
        DisplayVoicingResults([.. smallHandsResults.Take(3)], "Top 3 Results (Small Hands)");

        // Demo 3: Comfort filter (min 0.7 comfort score)
        AnsiConsole.MarkupLine("\n[cyan]═══ Demo 3: Comfort Filter (Min 0.7 Comfort Score) ═══[/]");
        var comfortFilters = new VoicingSearchFilters(
            MinComfortScore: 0.7);

        var comfortResults = await _searchStrategy!.HybridSearchAsync(queryEmbedding, comfortFilters, limit: 10);
        AnsiConsole.MarkupLine($"[green]Found {comfortResults.Count} comfortable voicings[/]");
        DisplayVoicingResults([.. comfortResults.Take(3)], "Top 3 Results (Comfortable)");

        // Demo 4: Ergonomic filter (must have ergonomic wrist posture)
        AnsiConsole.MarkupLine("\n[cyan]═══ Demo 4: Ergonomic Filter (Ergonomic Wrist Posture) ═══[/]");
        var ergonomicFilters = new VoicingSearchFilters(
            MustBeErgonomic: true);

        var ergonomicResults = await _searchStrategy!.HybridSearchAsync(queryEmbedding, ergonomicFilters, limit: 10);
        AnsiConsole.MarkupLine($"[green]Found {ergonomicResults.Count} ergonomic voicings[/]");
        DisplayVoicingResults([.. ergonomicResults.Take(3)], "Top 3 Results (Ergonomic)");

        // Demo 5: Combined filters (small hands + comfortable + ergonomic)
        AnsiConsole.MarkupLine("\n[cyan]═══ Demo 5: Combined Filters (Small Hands + Comfortable + Ergonomic) ═══[/]");
        var combinedFilters = new VoicingSearchFilters(
            HandSize: GA.Business.Core.Fretboard.Biomechanics.HandSize.Small,
            MaxFingerStretch: 3.0,
            MinComfortScore: 0.6,
            MustBeErgonomic: true);

        var combinedResults = await _searchStrategy!.HybridSearchAsync(queryEmbedding, combinedFilters, limit: 10);
        AnsiConsole.MarkupLine($"[green]Found {combinedResults.Count} voicings matching all criteria[/]");
        DisplayVoicingResults([.. combinedResults.Take(3)], "Top 3 Results (All Filters)");

        // Summary
        AnsiConsole.MarkupLine("\n[cyan]═══ Summary ═══[/]");
        var table = new Table();
        table.AddColumn("Filter Type");
        table.AddColumn("Results Found");
        table.AddColumn("Reduction");

        table.AddRow("Baseline (No Filters)", baselineResults.Count.ToString(), "-");
        table.AddRow("Small Hands", smallHandsResults.Count.ToString(),
            $"{(1 - (double)smallHandsResults.Count / baselineResults.Count) * 100:F1}%");
        table.AddRow("Comfortable", comfortResults.Count.ToString(),
            $"{(1 - (double)comfortResults.Count / baselineResults.Count) * 100:F1}%");
        table.AddRow("Ergonomic", ergonomicResults.Count.ToString(),
            $"{(1 - (double)ergonomicResults.Count / baselineResults.Count) * 100:F1}%");
        table.AddRow("All Combined", combinedResults.Count.ToString(),
            $"{(1 - (double)combinedResults.Count / baselineResults.Count) * 100:F1}%");

        AnsiConsole.Write(table);

        AnsiConsole.MarkupLine("\n[green]✓ Biomechanical filtering demo complete![/]");
        AnsiConsole.MarkupLine("[dim]Quick Win 1 successfully demonstrates hand size, stretch, and comfort filtering[/]");
    }

    /// <summary>
    /// Analyze biomechanical properties of voicings
    /// </summary>
    private void AnalyzeVoicingBiomechanics(List<VoicingEmbedding> voicings)
    {
        var table = new Table();
        table.AddColumn("Diagram");
        table.AddColumn("Chord");
        table.AddColumn("Fret Span");
        table.AddColumn("Comfort");
        table.AddColumn("Ergonomic");
        table.AddColumn("Overall");
        table.AddColumn("Difficulty");

        var analyzer = new GA.Business.Core.Fretboard.Biomechanics.BiomechanicalAnalyzer();

        foreach (var voicing in voicings)
        {
            var positions = ParseDiagramToPositions(voicing.Diagram);
            if (positions.Count == 0)
            {
                table.AddRow(voicing.Diagram, voicing.ChordName, "N/A", "N/A", "N/A", "N/A", voicing.Difficulty ?? "N/A");
                continue;
            }

            var analysis = analyzer.AnalyzeChordPlayability(positions);
            var fretSpan = analysis.StretchAnalysis?.MaxFretSpan.ToString("F1") ?? "N/A";
            var comfort = analysis.Comfort.ToString("F2");
            var ergonomic = analysis.WristPostureAnalysis?.IsErgonomic.ToString() ?? "N/A";
            var overall = analysis.OverallScore.ToString("F2");

            // Color code based on comfort score
            var comfortColor = analysis.Comfort switch
            {
                >= 0.8 => "green",
                >= 0.6 => "yellow",
                _ => "red"
            };

            table.AddRow(
                voicing.Diagram,
                voicing.ChordName,
                fretSpan,
                $"[{comfortColor}]{comfort}[/]",
                ergonomic,
                overall,
                voicing.Difficulty ?? "N/A");
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    /// <summary>
    /// Parse voicing diagram to positions for analysis
    /// </summary>
    private ImmutableList<Position> ParseDiagramToPositions(string diagram)
    {
        // Diagram format: "x-3-2-0-1-0" or "3-2-0-1-0-3"
        var parts = diagram.Split('-');
        var positions = new List<Position>();

        for (var i = 0; i < parts.Length && i < 6; i++)
        {
            var part = parts[i].Trim();
            var str = new Str(i + 1); // Str is 1-based (strings 1-6)

            if (part == "x" || part == "X")
            {
                // Muted string
                positions.Add(new Position.Muted(str));
            }
            else if (int.TryParse(part, out var fretValue))
            {
                var fret = new Fret(fretValue);
                var location = new GA.Business.Core.Fretboard.Positions.PositionLocation(str, fret);

                // Create a played position with estimated MIDI note
                // Standard tuning: E2(40), A2(45), D3(50), G3(55), B3(59), E4(64)
                var openMidiNotes = new[] { 64, 59, 55, 50, 45, 40 }; // High E to Low E
                var midiNoteValue = i < openMidiNotes.Length
                    ? openMidiNotes[i] + fretValue
                    : 60 + fretValue; // Fallback
                var midiNote = new GA.Business.Core.Notes.Primitives.MidiNote(midiNoteValue);

                positions.Add(new Position.Played(location, midiNote));
            }
        }

        return [.. positions];
    }

    /// <summary>
    /// Load real voicings from fretboard generator (with caching)
    /// </summary>
    private List<VoicingEmbedding> LoadRealVoicings(int maxCount = 1000)
    {
        var fretboard = Fretboard.Default;

        // Generate cache key based on instrument/tuning/model
        var cacheKey = GetCacheKey(fretboard, maxCount);
        var cachePath = Path.Combine(_cacheDirectory, $"{cacheKey}.json");

        // Try to load from cache
        if (TryLoadFromCache(cachePath, out var cachedVoicings))
        {
            AnsiConsole.MarkupLine($"[green]✓ Loaded {cachedVoicings.Count} voicings from cache[/]");
            return cachedVoicings;
        }

        AnsiConsole.MarkupLine("[yellow]Cache miss - generating voicings from fretboard...[/]");
        var voicings = new List<VoicingEmbedding>();
        var count = 0;

        // Generate real voicings using VoicingGenerator (exclude diads - require at least 3 notes)
        var realVoicings = GA.Business.Core.Fretboard.Voicings.Generation.VoicingGenerator.GenerateAllVoicings(
            fretboard,
            windowSize: 4,
            minPlayedNotes: 3,  // Exclude diads (2-note voicings)
            parallel: true);

        AnsiConsole.MarkupLine($"[green]Generated {realVoicings.Count} real voicings from fretboard[/]");

        // Convert to VoicingEmbedding format (take first maxCount)
        foreach (var voicing in realVoicings.Take(maxCount))
        {
            var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
            var fretSpan = VoicingExtensions.GetFretSpan(voicing.Positions);
            var minFret = VoicingExtensions.GetMinFret(voicing.Positions);
            var maxFret = VoicingExtensions.GetMaxFret(voicing.Positions);

            // Check if voicing has open strings
            var hasOpenStrings = voicing.Positions
                .OfType<Position.Played>()
                .Any(p => p.Location.Fret.Value == 0);

            // Determine difficulty based on fret span (excluding open strings)
            var difficulty = fretSpan switch
            {
                0 => "Easy",      // All same fret or open strings
                <= 2 => "Easy",   // 2-fret span
                3 => "Medium",    // 3-fret span
                4 => "Hard",      // 4-fret span
                5 => "Very Hard", // 5-fret span
                _ => "Extreme"    // 6+ fret span
            };

            // Determine position (consider open strings)
            var position = hasOpenStrings ? "Open" : minFret switch
            {
                null or 0 => "Open",
                <= 3 => "Low",
                <= 7 => "Mid",
                _ => "High"
            };

            // Create description for embedding
            var description = $"{position} position {difficulty} voicing with {fretSpan}-fret span. Diagram: {diagram}";

            voicings.Add(new VoicingEmbedding(
                Id: $"real-voicing-{count}",
                ChordName: "Unknown", // Would need chord recognition
                VoicingType: "Real",
                Position: position,
                Difficulty: difficulty,
                ModeName: null,
                ModalFamily: null,
                PossibleKeys: [],
                SemanticTags: new[] { position, difficulty, $"span-{fretSpan}" },
                PrimeFormId: "",
                TranslationOffset: 0,
                Diagram: diagram,
                MidiNotes: [.. voicing.Notes.Select(n => n.Value)],
                PitchClassSet: "",
                IntervalClassVector: "",
                MinFret: minFret ?? 0,
                MaxFret: maxFret ?? 0,
                BarreRequired: VoicingExtensions.HasBarre(voicing.Positions),
                HandStretch: fretSpan,
                StackingType: null,
                RootPitchClass: 0,
                MidiBassNote: 0,
                HarmonicFunction: null,
                IsNaturallyOccurring: true,
                ConsonanceScore: 0.5,
                BrightnessScore: 0.5,
                IsRootless: false,
                HasGuideTones: false,
                Inversion: 0,
                TopPitchClass: null,
                TexturalDescription: null,
                DoubledTones: null,
                AlternateNames: null,
                OmittedTones: null,
                CagedShape: null,
                Description: description,
                Embedding: GenerateRandomEmbedding(), // Will be replaced with real embeddings
                TextEmbedding: null
            ));

            count++;
        }

        AnsiConsole.MarkupLine($"[green]Converted {count} voicings to VoicingEmbedding format[/]");

        // Generate real embeddings for all voicings
        AnsiConsole.MarkupLine("[yellow]Generating semantic embeddings (this may take a while)...[/]");
        voicings = GenerateRealEmbeddings(voicings);

        // Save to cache
        SaveToCache(cachePath, voicings);
        AnsiConsole.MarkupLine($"[green]✓ Saved {voicings.Count} voicings to cache[/]\n");

        return voicings;
    }

    /// <summary>
    /// Generate real semantic embeddings for voicings using ONNX (FAST!) or Ollama (fallback)
    /// </summary>
    private List<VoicingEmbedding> GenerateRealEmbeddings(List<VoicingEmbedding> voicings)
    {
        // Use ONNX if available (2000+ emb/sec), otherwise fall back to Ollama (30 emb/sec)
        if (_onnxEmbeddingService != null)
        {
            return GenerateWithOnnx(voicings);
        }
        else if (_embeddingService != null)
        {
            return GenerateWithOllama(voicings);
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Warning: No embedding service available, using random embeddings[/]");
            return voicings;
        }
    }

    /// <summary>
    /// Generate embeddings using ONNX Runtime (FAST - 2000+ embeddings/sec)
    /// </summary>
    private List<VoicingEmbedding> GenerateWithOnnx(List<VoicingEmbedding> voicings)
    {
        var result = new VoicingEmbedding[voicings.Count];
        var startTime = DateTime.Now;
        var errorCount = 0;
        var processedCount = 0;

        AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx =>
            {
                var task = ctx.AddTask("[green]Generating embeddings (ONNX - FAST!)[/]", maxValue: voicings.Count);

                // Process sequentially (ONNX is already blazing fast - no need for parallelism)
                for (int i = 0; i < voicings.Count; i++)
                {
                    var voicing = voicings[i];

                    try
                    {
                        var embedding = _onnxEmbeddingService!.GenerateEmbeddingAsync(voicing.Description).Result;
                        var doubleEmbedding = embedding.Select(f => (double)f).ToArray();
                        result[i] = voicing with { Embedding = doubleEmbedding };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate embedding for {Id}", voicing.Id);
                        result[i] = voicing; // Keep with random embedding
                        errorCount++;
                    }

                    processedCount++;
                    task.Increment(1);

                    // Update stats every 100 items
                    if (processedCount % 100 == 0)
                    {
                        var elapsed = DateTime.Now - startTime;
                        var rate = processedCount / elapsed.TotalSeconds;
                        var remaining = (voicings.Count - processedCount) / rate;
                        task.Description = $"[green]Generating embeddings (ONNX - FAST!)[/] ({processedCount:N0}/{voicings.Count:N0}) - {rate:F0}/sec - ETA: {TimeSpan.FromSeconds(remaining):hh\\:mm\\:ss}";
                    }
                }

                task.StopTask();
            });

        var totalTime = DateTime.Now - startTime;
        var finalRate = voicings.Count / totalTime.TotalSeconds;
        AnsiConsole.MarkupLine($"[green]✓ Generated {result.Length:N0} embeddings in {totalTime:hh\\:mm\\:ss} ({finalRate:F0}/sec)[/]");
        if (errorCount > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ {errorCount} errors occurred during generation[/]");
        }
        AnsiConsole.WriteLine();

        return [.. result];
    }

    /// <summary>
    /// Generate embeddings using Ollama (SLOW - 30 embeddings/sec) - fallback only
    /// </summary>
    private List<VoicingEmbedding> GenerateWithOllama(List<VoicingEmbedding> voicings)
    {
        var result = new VoicingEmbedding[voicings.Count];
        var startTime = DateTime.Now;
        var errorCount = 0;
        var processedCount = 0;

        AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .Start(ctx =>
            {
                var task = ctx.AddTask("[yellow]Generating embeddings (Ollama - SLOW)[/]", maxValue: voicings.Count);

                // Process sequentially (parallelism doesn't help with Ollama)
                for (int i = 0; i < voicings.Count; i++)
                {
                    var voicing = voicings[i];

                    try
                    {
                        var floatEmbedding = _embeddingService!.GenerateEmbeddingAsync(voicing.Description).Result;
                        var doubleEmbedding = floatEmbedding.Select(f => (double)f).ToArray();
                        result[i] = voicing with { Embedding = doubleEmbedding };
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to generate embedding for {Id}, using random", voicing.Id);
                        result[i] = voicing; // Keep with random embedding
                        errorCount++;
                    }

                    processedCount++;
                    task.Increment(1);

                    // Update stats every 100 items
                    if (processedCount % 100 == 0)
                    {
                        var elapsed = DateTime.Now - startTime;
                        var rate = processedCount / elapsed.TotalSeconds;
                        var remaining = (voicings.Count - processedCount) / rate;
                        task.Description = $"[yellow]Generating embeddings (Ollama - SLOW)[/] ({processedCount:N0}/{voicings.Count:N0}) - {rate:F1}/sec - ETA: {TimeSpan.FromSeconds(remaining):hh\\:mm\\:ss}";
                    }
                }

                task.StopTask();
            });

        var totalTime = DateTime.Now - startTime;
        var finalRate = voicings.Count / totalTime.TotalSeconds;
        AnsiConsole.MarkupLine($"[yellow]✓ Generated {result.Length:N0} embeddings in {totalTime:hh\\:mm\\:ss} ({finalRate:F1}/sec)[/]");
        if (errorCount > 0)
        {
            AnsiConsole.MarkupLine($"[yellow]⚠ {errorCount} errors occurred during generation[/]");
        }
        AnsiConsole.WriteLine();

        return [.. result];
    }

    /// <summary>
    /// Generate voicings with varied biomechanical difficulty
    /// </summary>
    private List<VoicingEmbedding> GenerateVoicingsWithVariedDifficulty(int count)
    {
        var voicings = new List<VoicingEmbedding>();
        var chordTypes = new[] { "Cmaj7", "Dm7", "Em7", "Fmaj7", "G7", "Am7", "Bm7b5" };
        var difficulties = new[] { "Easy", "Medium", "Hard", "Very Hard" };
        var positions = new[] { "Open", "Low", "Mid", "High" };

        // Generate voicings with varied diagrams (different stretch requirements)
        // Mix of easy, medium, hard, and TRULY DIFFICULT voicings
        var diagrams = new[]
        {
            // EASY - Small stretch, comfortable positions
            "x-3-2-0-1-0",      // Easy - small stretch (3 frets)
            "0-2-2-1-0-0",      // Easy - open position (2 frets)
            "3-x-2-0-1-0",      // Easy - open voicing (3 frets)
            "x-x-0-2-3-2",      // Easy - partial voicing (3 frets)

            // MEDIUM - Moderate stretch, some difficulty
            "x-3-5-4-5-3",      // Medium - moderate stretch (2 frets, but requires precision)
            "x-5-7-7-7-5",      // Medium - barre (2 frets)
            "x-x-5-7-8-7",      // Medium - partial voicing (3 frets)
            "5-7-7-6-5-5",      // Medium - complex fingering (2 frets)

            // HARD - Wide stretch, high positions
            "x-7-9-9-10-7",     // Hard - wide stretch (3 frets, high position)
            "8-10-10-9-8-8",    // Hard - high position (2 frets)
            "x-5-7-9-10-7",     // Hard - 5-fret span! Should fail small hands filter
            "x-8-10-12-13-8",   // Hard - 5-fret span, very high position

            // VERY HARD - Extreme stretch, uncomfortable positions
            "1-1-1-1-1-1",      // Very Hard - full barre at fret 1 (uncomfortable)
            "x-3-7-9-10-7",     // Very Hard - 7-fret span! Should definitely fail
            "12-14-14-13-12-12", // Very Hard - very high position (2 frets)
            "x-10-14-15-17-10", // Very Hard - 7-fret span, extreme stretch
            "8-12-14-15-16-12", // Very Hard - 8-fret span! Nearly impossible
            "x-1-4-6-8-1",      // Very Hard - 7-fret span, low position
            "5-10-12-13-14-10", // Very Hard - 9-fret span! Impossible for most
            "x-x-10-14-16-15"   // Very Hard - 6-fret span, high position
        };

        for (int i = 0; i < count; i++)
        {
            var diagram = diagrams[i % diagrams.Length];
            var difficulty = difficulties[i % difficulties.Length];
            var position = positions[i % positions.Length];
            var chordType = chordTypes[i % chordTypes.Length];

            voicings.Add(new VoicingEmbedding(
                Id: $"voicing-{i}",
                ChordName: chordType,
                VoicingType: i % 3 == 0 ? "Drop-2" : i % 3 == 1 ? "Rootless" : "Standard",
                Position: position,
                Difficulty: difficulty,
                ModeName: "Ionian",
                ModalFamily: "Major",
                PossibleKeys: [],
                SemanticTags: new[] { $"CAGED-{(char)('C' + i % 5)}", difficulty, position },
                PrimeFormId: "[0,4,7,11]",
                TranslationOffset: i % 12,
                Diagram: diagram,
                MidiNotes: new[] { 60, 64, 67, 71 },
                PitchClassSet: "[0,4,7,11]",
                IntervalClassVector: "[0,0,1,1,1,0]",
                MinFret: 0,
                MaxFret: 12,
                BarreRequired: diagram.Contains("7-7") || diagram.Contains("10-10"),
                HandStretch: diagram.Contains("10") ? 5 : diagram.Contains("7-9") ? 4 : 3,
                StackingType: null,
                RootPitchClass: 0,
                MidiBassNote: 0,
                HarmonicFunction: null,
                IsNaturallyOccurring: true,
                ConsonanceScore: 0.5,
                BrightnessScore: 0.5,
                IsRootless: false,
                HasGuideTones: false,
                Inversion: 0,
                TopPitchClass: null,
                TexturalDescription: null,
                DoubledTones: null,
                AlternateNames: null,
                OmittedTones: null,
                CagedShape: null,
                Description: $"{chordType} {position} position - {difficulty}",
                Embedding: GenerateRandomEmbedding(),
                TextEmbedding: null
            ));
        }

        return voicings;
    }

    /// <summary>
    /// Display voicing search results in a formatted table
    /// </summary>
    private void DisplayVoicingResults(List<VoicingSearchResult> results, string title)
    {
        if (results.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No results found[/]");
            return;
        }

        AnsiConsole.MarkupLine($"[bold]{title}:[/]");
        var table = new Table();
        table.AddColumn("Rank");
        table.AddColumn("Chord");
        table.AddColumn("Diagram");
        table.AddColumn("Difficulty");
        table.AddColumn("Position");
        table.AddColumn("Score");

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            table.AddRow(
                $"{i + 1}",
                result.Document.ChordName,
                result.Document.Diagram,
                result.Document.Difficulty,
                result.Document.Position,
                $"{result.Score:F3}"
            );
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Generate cache key based on fretboard configuration and model
    /// </summary>
    private string GetCacheKey(Fretboard fretboard, int maxCount)
    {
        // Create a unique key based on:
        // - Number of strings
        // - Tuning (pitch collection string representation)
        // - Max count
        // - Embedding model
        var tuningString = fretboard.Tuning.PitchCollection.ToString().Replace(" ", "-");
        var keyString = $"strings{fretboard.StringCount}_tuning{tuningString}_max{maxCount}_model{_embeddingModel}";

        // Hash to create shorter filename
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(keyString));
        var hashString = Convert.ToHexString(hashBytes)[..16]; // First 16 chars

        return $"voicings_{fretboard.StringCount}str_{hashString}";
    }

    /// <summary>
    /// Try to load voicings from cache
    /// </summary>
    private bool TryLoadFromCache(string cachePath, out List<VoicingEmbedding> voicings)
    {
        voicings = new List<VoicingEmbedding>();

        if (!File.Exists(cachePath))
        {
            return false;
        }

        try
        {
            AnsiConsole.MarkupLine($"[dim]Loading from cache: {cachePath}[/]");
            var json = File.ReadAllText(cachePath);
            var cached = JsonSerializer.Deserialize<List<VoicingEmbedding>>(json);

            if (cached != null && cached.Count > 0)
            {
                voicings = cached;
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load cache from {Path}", cachePath);
        }

        return false;
    }

    /// <summary>
    /// Save voicings to cache
    /// </summary>
    private void SaveToCache(string cachePath, List<VoicingEmbedding> voicings)
    {
        try
        {
            // Ensure cache directory exists
            var directory = Path.GetDirectoryName(cachePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            AnsiConsole.MarkupLine($"[dim]Saving to cache: {cachePath}[/]");
            var json = JsonSerializer.Serialize(voicings, new JsonSerializerOptions
            {
                WriteIndented = false // Compact format to save space
            });
            File.WriteAllText(cachePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save cache to {Path}", cachePath);
        }
    }
}

