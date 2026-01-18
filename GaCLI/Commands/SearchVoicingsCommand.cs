namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;
using System.Text.RegularExpressions;

using GA.Business.ML.Embeddings;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Core;

public class SearchVoicingsCommand(
    MongoDbService mongoDbService, 
    IVectorIndex vectorIndex, 
    MusicalEmbeddingGenerator embeddingGenerator)
{
    public class ValidatedOptions
    {
        public string? ChordName { get; set; }
        public string? Difficulty { get; set; }
        public string? Tag { get; set; }
        public int? MinFret { get; set; }
        public int? MaxFret { get; set; }
        public int? MinStretch { get; set; }
        public int? MaxStretch { get; set; }
        public bool? NoBarre { get; set; }
        public string? ForteCode { get; set; }
        public string? HarmonicFunction { get; set; }
        public bool? HasGuideTones { get; set; }
        public bool? IsRootless { get; set; }
        public string? Register { get; set; }
        public string? StringSet { get; set; }
        public bool Detailed { get; set; } = false;
        public int Limit { get; set; } = 20;
        public string? SimilarTo { get; set; } 
    }

    public async Task ExecuteAsync(ValidatedOptions options)
    {
        var rule = new Rule($"[blue]Voicing Search[/]");
        rule.Justification = Justify.Left;
        AnsiConsole.Write(rule);
        
        try
        {
            var collection = mongoDbService.Voicings;

            // VECTOR SEARCH PATH
            if (!string.IsNullOrWhiteSpace(options.SimilarTo))
            {
                await ExecuteVectorSearchAsync(options, collection);
                return;
            }

            var builder = Builders<VoicingEntity>.Filter;
            var filter = builder.Empty;

            // Build filters
            if (!string.IsNullOrWhiteSpace(options.ChordName))
            {
                filter &= builder.Regex(v => v.ChordName, new MongoDB.Bson.BsonRegularExpression($"^{Regex.Escape(options.ChordName)}$", "i"));
            }

            if (!string.IsNullOrWhiteSpace(options.ForteCode))
            {
                filter &= builder.Eq(v => v.ForteCode, options.ForteCode);
            }

            if (!string.IsNullOrWhiteSpace(options.Difficulty))
            {
                filter &= builder.Eq(v => v.Difficulty, options.Difficulty);
            }

            if (!string.IsNullOrWhiteSpace(options.Tag))
            {
                filter &= builder.AnyEq(v => v.SemanticTags, options.Tag);
            }

            if (!string.IsNullOrWhiteSpace(options.HarmonicFunction))
            {
                filter &= builder.Eq(v => v.HarmonicFunction, options.HarmonicFunction);
            }

            if (!string.IsNullOrWhiteSpace(options.Register))
            {
                filter &= builder.Eq(v => v.Register, options.Register);
            }

            if (!string.IsNullOrWhiteSpace(options.StringSet))
            {
                filter &= builder.Eq(v => v.StringSet, options.StringSet);
            }

            if (options.HasGuideTones.HasValue)
            {
                filter &= builder.Eq(v => v.HasGuideTones, options.HasGuideTones.Value);
            }

            if (options.IsRootless.HasValue)
            {
                filter &= builder.Eq(v => v.IsRootless, options.IsRootless.Value);
            }

            if (options.MinFret.HasValue)
            {
                filter &= builder.Gte(v => v.MinFret, options.MinFret.Value);
            }
            
            if (options.MaxFret.HasValue)
            {
                filter &= builder.Lte(v => v.MaxFret, options.MaxFret.Value);
            }

            if (options.MinStretch.HasValue)
            {
                filter &= builder.Gte(v => v.HandStretch, options.MinStretch.Value);
            }

            if (options.MaxStretch.HasValue)
            {
                filter &= builder.Lte(v => v.HandStretch, options.MaxStretch.Value);
            }

            if (options.NoBarre.HasValue && options.NoBarre.Value)
            {
                filter &= builder.Eq(v => v.BarreRequired, false);
            }

            // Execute Query
            var count = await collection.CountDocumentsAsync(filter);
            
            if (count == 0)
            {
                AnsiConsole.MarkupLine("[red]No voicings matched your criteria.[/]");
                return;
            }

            // Execute Query
            var sort = Builders<VoicingEntity>.Sort.Ascending(v => v.MinFret).Ascending(v => v.Difficulty);
            
            var results = await collection.Find(filter)
                .Sort(sort)
                .Limit(options.Limit)
                .ToListAsync();

            AnsiConsole.MarkupLine($"Found [green]{count:N0}[/]. Showing top {results.Count}:");

            if (options.Detailed)
            {
                // Detailed view - show all properties
                foreach (var v in results)
                {
                    DisplayDetailedVoicing(v);
                }
            }
            else
            {
                // Simplified Table view
                var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Chord")
                    .AddColumn("Diagram")
                    .AddColumn("Difficulty")
                    .AddColumn("Function");

                foreach (var v in results)
                {
                    table.AddRow(
                        $"[bold]{Markup.Escape(v.ChordName ?? "?")}[/]",
                        $"[cyan]{v.Diagram}[/]",
                        ColorDifficulty(v.Difficulty),
                        ColorFunction(v.HarmonicFunction)
                    );
                }

                AnsiConsole.Write(table);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/]");
            AnsiConsole.WriteException(ex);
        }
    }

    private void DisplayDetailedVoicing(VoicingEntity v)
    {
        var panel = new Panel(new Rows(
            new Markup($"[bold cyan]Diagram:[/] {v.Diagram}"),
            new Markup($"[bold]Chord:[/] {Markup.Escape(v.ChordName ?? "?")}"),
            v.AlternateChordNames?.Length > 0 
                ? new Markup($"[dim]Also known as:[/] {string.Join(", ", v.AlternateChordNames)}") 
                : new Markup(""),
            new Rule("[yellow]Identity[/]").LeftJustified(),
            new Markup($"  Key: {v.ClosestKey ?? "?"} | Roman: {v.RomanNumeral ?? "?"} | Function: {ColorFunction(v.HarmonicFunction)}"),
            new Markup($"  Forte: {v.ForteCode ?? "?"} | Prime Form: {v.PrimeFormId ?? "?"} | Mode: {v.ModeName ?? "-"}"),
            new Markup($"  {(v.IsNaturallyOccurring ? "[green]Diatonic[/]" : "[yellow]Borrowed/Chromatic[/]")} | Context Required: {(v.RequiresContext ? "[yellow]Yes[/]" : "No")}"),
            new Rule("[yellow]Sound[/]").LeftJustified(),
            new Markup($"  Register: {v.Register ?? "?"} | Brightness: {v.Brightness:P0} | Consonance: {v.ConsonanceScore:P0}"),
            new Markup($"  Tones: {string.Join(", ", v.TonesPresent ?? [])} | Guide Tones: {(v.HasGuideTones ? "[green]Yes[/]" : "[dim]No[/]")}"),
            v.DoubledTones?.Length > 0 ? new Markup($"  Doubled: {string.Join(", ", v.DoubledTones)}") : new Markup(""),
            v.OmittedTones?.Length > 0 ? new Markup($"  Omitted: [dim]{string.Join(", ", v.OmittedTones)}[/]") : new Markup(""),
            v.MayBeMuddy ? new Markup("  [red]⚠ May sound muddy (close intervals in bass)[/]") : new Markup(""),
            new Rule("[yellow]Hands[/]").LeftJustified(),
            new Markup($"  Position: {v.HandPosition} | Difficulty: {ColorDifficulty(v.Difficulty)} ({v.DifficultyScore:F1}) | Stretch: {v.HandStretch} frets"),
            new Markup($"  Barre: {(v.BarreRequired ? $"[yellow]Required[/] ({v.BarreInfo})" : "[green]No[/]")} | Fingers: {v.MinimumFingers}"),
            new Markup($"  String Set: {v.StringSet ?? "?"} | String Skips: {v.StringSkips}"),
            v.CagedShape != null ? new Markup($"  CAGED: {v.CagedShape}") : new Markup(""),
            new Rule("[yellow]Context[/]").LeftJustified(),
            v.SemanticTags?.Length > 0 ? new Markup($"  Tags: [cyan]{string.Join(", ", v.SemanticTags)}[/]") : new Markup(""),
            v.GenreTags?.Length > 0 ? new Markup($"  Genres: {string.Join(", ", v.GenreTags)}") : new Markup(""),
            v.PlayStyles?.Length > 0 ? new Markup($"  Play Styles: {string.Join(", ", v.PlayStyles)}") : new Markup(""),
            v.CommonSubstitutions?.Length > 0 ? new Markup($"  Substitutions: {string.Join(", ", v.CommonSubstitutions)}") : new Markup(""),
            v.SongReferences?.Length > 0 ? new Markup($"  [dim italic]Songs: {string.Join(", ", v.SongReferences)}[/]") : new Markup("")
        ))
        {
            Header = new PanelHeader($"[bold green]{Markup.Escape(v.ChordName ?? v.Diagram)}[/]"),
            Border = BoxBorder.Rounded,
            Padding = new Padding(1, 0)
        };
        
        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static string ColorDifficulty(string? diff)
    {
        if (string.IsNullOrEmpty(diff)) return "-";
        return diff.ToLower() switch
        {
            "beginner" => $"[green]{diff}[/]",
            "intermediate" => $"[yellow]{diff}[/]",
            "advanced" => $"[red]{diff}[/]",
            _ => diff
        };
    }

    private static string ColorFunction(string? func)
    {
        if (string.IsNullOrEmpty(func)) return "-";
        return func.ToLower() switch
        {
            "tonic" => $"[green]{func}[/]",
            "predominant" => $"[cyan]{func}[/]",
            "dominant" => $"[yellow]{func}[/]",
            "ambiguous" => $"[dim]{func}[/]",
            _ => func
        };
    }

    private async Task ExecuteVectorSearchAsync(ValidatedOptions options, IMongoCollection<VoicingEntity> collection)
    {
        AnsiConsole.MarkupLine($"[yellow]Searching for voicings similar to '{options.SimilarTo}'...[/]");
        
        var filter = Builders<VoicingEntity>.Filter.Eq(v => v.ChordName, options.SimilarTo) | 
                     Builders<VoicingEntity>.Filter.Eq(v => v.Diagram, options.SimilarTo);
        var seedEntity = await collection.Find(filter).FirstOrDefaultAsync();
        
        double[]? queryVector = null;

        if (seedEntity != null)
        {
             if (seedEntity.Embedding != null && seedEntity.Embedding.Length > 0)
             {
                 queryVector = seedEntity.Embedding;
                 AnsiConsole.MarkupLine($"[green]Found seed voicing: {seedEntity.ChordName} ({seedEntity.Diagram})[/]");
             }
             else
             {
                  AnsiConsole.MarkupLine($"[red]Seed has no embedding. Index it first.[/]");
                  return;
             }
        }
        else
        {
             AnsiConsole.MarkupLine($"[red]Could not find voicing '{options.SimilarTo}'[/]");
             return;
        }

        if (queryVector == null) return;

        var results = vectorIndex.Search(queryVector, options.Limit);
        
        var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Score")
                    .AddColumn("Chord")
                    .AddColumn("Diagram")
                    .AddColumn("Tags");

        foreach (var (doc, score) in results)
        {
             var scoreFmt = score > 0.99 ? $"[green]{score:F4}[/]" : $"{score:F4}";
             table.AddRow(
                 scoreFmt,
                 $"[bold]{Markup.Escape(doc.ChordName ?? "?")}[/]",
                 $"[cyan]{doc.Diagram}[/]",
                 string.Join(", ", doc.SemanticTags.Take(3))
             );
        }
        
        AnsiConsole.Write(table);
    }
}

