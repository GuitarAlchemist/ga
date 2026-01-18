namespace GaCLI.Commands;

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Embeddings;
using GA.Data.MongoDB.Services;
using Microsoft.SemanticKernel;
using MongoDB.Driver;
using MongoDB.Bson;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard.Voicings.Analysis;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.Core.Notes.Primitives;
using System.Collections.Immutable;
using Spectre.Console;
using static System.Console;

public class HybridBenchmarkCommand
{
    private readonly EnhancedVoicingSearchService _searchService;
    private readonly MongoDbService _mongoDbService;
    private readonly VoicingIndexingService _indexingService;
    private readonly OnnxEmbeddingGenerator _textEmbeddingGenerator;

    public HybridBenchmarkCommand(
        EnhancedVoicingSearchService searchService,
        MongoDbService mongoDbService,
        VoicingIndexingService indexingService,
        OnnxEmbeddingGenerator textEmbeddingGenerator)
    {
        _searchService = searchService;
        _mongoDbService = mongoDbService;
        _indexingService = indexingService;
        _textEmbeddingGenerator = textEmbeddingGenerator;
    }

    public async Task ExecuteAsync(bool verbose = false, string? key = null, int? maxFret = null, bool movable = false, int limit = 1000)
    {
        AnsiConsole.MarkupLine("[bold blue]Starting Hybrid Search Benchmark...[/]");

        // 1. Build Filter
        var builder = Builders<GA.Data.MongoDB.Models.VoicingEntity>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrEmpty(key))
        {
            // Simple check: ClosestKey starts with the letter (e.g. "E" matches "E Major", "E Minor")
            // Or exact match if user provides "E Major"
            filter &= builder.Regex(x => x.ClosestKey, new BsonRegularExpression($"^{key}", "i"));
            AnsiConsole.MarkupLine($"[yellow]Filter: Key starts with '{key}'[/]");
        }

        if (maxFret.HasValue)
        {
            filter &= builder.Lte(x => x.MaxFret, maxFret.Value);
            AnsiConsole.MarkupLine($"[yellow]Filter: Max Fret <= {maxFret.Value}[/]");
        }

        if (movable)
        {
            // Check for "movable" type OR tag
            var typeFilter = builder.Regex(x => x.VoicingType, new BsonRegularExpression("movable", "i"));
            var tagFilter = builder.AnyEq(x => x.SemanticTags, "movable");
            filter &= (typeFilter | tagFilter);
            AnsiConsole.MarkupLine($"[yellow]Filter: Movable only[/]");
        }

        // 2. Initialize with filtered data
        var mongoQuery = _mongoDbService.Voicings.Find(filter);
        
        // If limit is <= 0 or int.MaxValue, treat as "All" (no limit on query, but be careful with memory)
        if (limit > 0 && limit < int.MaxValue)
        {
            mongoQuery = mongoQuery.Limit(limit);
        }

        var entities = await mongoQuery.ToListAsync();

        var documents = entities.Select(e => new VoicingDocument
        {
            Id = e.Id,
            SearchableText = e.ChordName + " " + string.Join(" ", e.SemanticTags),
            Diagram = e.Diagram,
            Embedding = e.Embedding,
            TextEmbedding = HasValidEmbedding(e.TextEmbedding) ? e.TextEmbedding : null, // Use existing only if valid
            SemanticTags = [.. e.SemanticTags],
            ChordName = e.ChordName,
            MidiNotes = e.MidiNotes,
            PitchClasses = e.PitchClasses,
            PitchClassSet = "{" + string.Join(",", e.PitchClasses.OrderBy(p => p)) + "}",
            IntervalClassVector = e.IntervalClassVector,
            AnalysisEngine = "GaCLI",
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = e.PrimeFormId ?? "Unknown",
            PossibleKeys = [],
            YamlAnalysis = "",
            CagedShape = e.CagedShape // Map CAGED shape
        }).ToList();
        
        
        _indexingService.LoadDocuments(documents);
        AnsiConsole.MarkupLine($"[yellow]Loaded {documents.Count} documents into indexing service[/]");
        AnsiConsole.MarkupLine($"[gray]IndexingService Document Count: {_indexingService.DocumentCount}[/]");

        await _searchService.InitializeEmbeddingsAsync(
            text => _textEmbeddingGenerator.GenerateEmbeddingAsync(text),
            null);

        var stats = _searchService.GetStats();
        AnsiConsole.MarkupLine($"[yellow]Search Strategy Stats: {stats.TotalVoicings} documents indexed[/]");

        // 2. Run Test Queries
        var queries = new[]
        {
            "beginner chords",
            "soulful jazz voicings",
            "hendrix chord",
            "tense dominant shapes"
        };

        var table = new Table().Border(TableBorder.Rounded).Title("Hybrid Search Benchmark Results");
        table.AddColumn("Query");
        table.AddColumn("Top Result");
        table.AddColumn("Diagram");
        table.AddColumn("Score");
        table.AddColumn("Tags Matched");
        table.AddColumn("Time (ms)");

        var detailedResults = new System.Collections.Generic.List<(string Query, GA.Business.Core.Fretboard.Voicings.Search.VoicingSearchResult Result)>();

        foreach (var query in queries)
        {
            AnsiConsole.MarkupLine($"[grey]Running query: {query}[/]");
            var stopwatch = Stopwatch.StartNew();
            var results = await _searchService.SearchAsync(
                query, 
                text => _textEmbeddingGenerator.GenerateEmbeddingAsync(text), 
                topK: 5);
            stopwatch.Stop();

            if (results.Any())
            {
                var top = results.First();
                detailedResults.Add((query, top));
                var matchedTags = string.Join(", ", top.Document.SemanticTags.Intersect(query.Split(' '), StringComparer.OrdinalIgnoreCase));
                table.AddRow(
                    query, 
                    top.Document.ChordName ?? "Unknown", 
                    top.Document.Diagram ?? "",
                    top.Score.ToString("F2"),
                    matchedTags,
                    stopwatch.ElapsedMilliseconds.ToString());
            }
        }

        AnsiConsole.Write(table);

    if (verbose)
    {
        AnsiConsole.MarkupLine("\n[bold blue]Detailed Results Breakdown:[/]");
        foreach (var item in detailedResults)
        {
            var doc = item.Result.Document;
            var panel = new Panel(
                new Grid()
                    .AddColumn(new GridColumn().NoWrap())
                    .AddColumn(new GridColumn())
                    .AddRow("[bold]Query:[/]", item.Query)
                        .AddRow("[bold]Chord:[/]", doc.ChordName ?? "Unknown")
                        .AddRow("[bold]Diagram:[/]", doc.Diagram ?? "N/A")
                        .AddRow("[bold]CAGED:[/]", doc.CagedShape ?? "N/A")
                        .AddRow("[bold]Score:[/]", item.Result.Score.ToString("F4"))
                    .AddRow("[bold]All Tags:[/]", string.Join(", ", doc.SemanticTags))
                    .AddRow("[bold]Text:[/]", $"[grey]{Markup.Escape(doc.SearchableText)}[/]")
            );
            panel.Header = new PanelHeader(item.Query);
            panel.Border = BoxBorder.Rounded;
            AnsiConsole.Write(panel);
        }
    }
}

private static bool HasValidEmbedding(double[]? embedding)
{
    return embedding != null && embedding.Length > 0 && embedding.Any(x => Math.Abs(x) > double.Epsilon);
}

}

