namespace VoicingSearchDemo;

using GA.Business.Core.Fretboard;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings;
using Microsoft.Extensions.Logging;

/// <summary>
/// Demo application for voicing semantic search using RAG
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== Guitar Voicing Semantic Search Demo ===\n");

        // Setup logging
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        var indexingLogger = loggerFactory.CreateLogger<VoicingIndexingService>();
        var searchLogger = loggerFactory.CreateLogger<VoicingVectorSearchService>();

        // Create services
        var indexingService = new VoicingIndexingService(indexingLogger);
        var searchService = new VoicingVectorSearchService(searchLogger, indexingService);

        // Step 1: Generate and index voicings
        Console.WriteLine("Step 1: Generating and indexing voicings...");
        Console.WriteLine("This will generate a subset of voicings for demonstration purposes.\n");

        var fretboard = Fretboard.Default;
        var vectorCollection = new RelativeFretVectorCollection(strCount: 6, fretExtent: 5);

        // Generate a limited set of voicings for demo (open position triads)
        var criteria = new VoicingFilterCriteria
        {
            ChordType = ChordTypeFilter.Triads,
            FretRange = FretRangeFilter.OpenPosition,
            MaxResults = 100 // Limit for demo
        };

        var allVoicings = VoicingGenerator.GenerateAllVoicings(fretboard, windowSize: 4, minPlayedNotes: 3);

        var indexingResult = await indexingService.IndexFilteredVoicingsAsync(
            allVoicings,
            vectorCollection,
            criteria);

        Console.WriteLine($"✓ Indexed {indexingResult.DocumentCount} voicings in {indexingResult.Duration.TotalSeconds:F2}s\n");

        if (indexingResult.DocumentCount == 0)
        {
            Console.WriteLine("No voicings were indexed. Exiting.");
            return;
        }

        // Step 2: Initialize embeddings (mock implementation for demo)
        Console.WriteLine("Step 2: Initializing embeddings...");
        Console.WriteLine("Using mock embedding generator for demonstration.\n");

        await searchService.InitializeEmbeddingsAsync(MockEmbeddingGenerator);

        Console.WriteLine($"✓ Generated embeddings for {searchService.DocumentCount} documents\n");

        // Step 3: Interactive search demo
        Console.WriteLine("Step 3: Interactive Search Demo");
        Console.WriteLine("================================\n");

        await RunInteractiveDemo(searchService);

        Console.WriteLine("\n=== Demo Complete ===");
    }

    static async Task RunInteractiveDemo(VoicingVectorSearchService searchService)
    {
        var queries = new[]
        {
            "beginner friendly open position chords",
            "jazz voicings with rootless chords",
            "easy major chords for campfire songs",
            "drop-2 voicings in upper position",
            "dorian mode voicings"
        };

        Console.WriteLine("Example Queries:");
        for (int i = 0; i < queries.Length; i++)
        {
            Console.WriteLine($"  {i + 1}. {queries[i]}");
        }
        Console.WriteLine($"  {queries.Length + 1}. Custom query");
        Console.WriteLine("  0. Exit\n");

        while (true)
        {
            Console.Write("Select a query (0-{0}): ", queries.Length + 1);
            var input = Console.ReadLine();

            if (!int.TryParse(input, out var choice))
            {
                Console.WriteLine("Invalid input. Please enter a number.\n");
                continue;
            }

            if (choice == 0)
                break;

            string query;
            if (choice == queries.Length + 1)
            {
                Console.Write("Enter your query: ");
                query = Console.ReadLine() ?? "";
            }
            else if (choice > 0 && choice <= queries.Length)
            {
                query = queries[choice - 1];
            }
            else
            {
                Console.WriteLine("Invalid choice.\n");
                continue;
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("Query cannot be empty.\n");
                continue;
            }

            Console.WriteLine($"\nSearching for: \"{query}\"\n");

            try
            {
                var results = await searchService.SearchAsync(
                    query,
                    MockEmbeddingGenerator,
                    topK: 5);

                if (results.Count == 0)
                {
                    Console.WriteLine("No results found.\n");
                    continue;
                }

                Console.WriteLine($"Found {results.Count} results:\n");

                for (int i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    var doc = result.Document;

                    Console.WriteLine($"Result {i + 1} (Score: {result.Score:F4})");
                    Console.WriteLine($"  Chord: {doc.ChordName}");
                    Console.WriteLine($"  Position: {doc.Position}");
                    Console.WriteLine($"  Difficulty: {doc.Difficulty}");
                    Console.WriteLine($"  Voicing Type: {doc.VoicingType}");

                    if (!string.IsNullOrEmpty(doc.ModeName))
                        Console.WriteLine($"  Mode: {doc.ModeName} ({doc.ModalFamily})");

                    if (doc.SemanticTags.Length > 0)
                        Console.WriteLine($"  Tags: {string.Join(", ", doc.SemanticTags.Take(5))}");

                    Console.WriteLine($"  Fret Range: {doc.MinFret}-{doc.MaxFret}");
                    Console.WriteLine($"  Barre Required: {doc.BarreRequired}");

                    if (!string.IsNullOrEmpty(doc.Diagram))
                    {
                        Console.WriteLine("  Diagram:");
                        foreach (var line in doc.Diagram.Split('\n').Take(7))
                        {
                            Console.WriteLine($"    {line}");
                        }
                    }

                    Console.WriteLine();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}\n");
            }
        }
    }

    /// <summary>
    /// Mock embedding generator for demonstration
    /// In production, this would use a real embedding model (Ollama, OpenAI, etc.)
    /// </summary>
    static Task<double[]> MockEmbeddingGenerator(string text)
    {
        // Simple mock: create a deterministic embedding based on text hash
        // In production, use actual embedding models
        var hash = text.GetHashCode();
        var random = new Random(hash);

        var embedding = new double[384]; // Standard embedding dimension
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] = random.NextDouble() * 2 - 1; // Range: -1 to 1
        }

        // Normalize
        var magnitude = Math.Sqrt(embedding.Sum(x => x * x));
        for (var i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= magnitude;
        }

        return Task.FromResult(embedding);
    }
}

