namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;

public class DemoVoicingCapabilitiesCommand(MongoDbService mongoDbService)
{
    public async Task ExecuteAsync()
    {
        AnsiConsole.Write(
            new FigletText("Voicing Use Cases")
                .LeftJustified()
                .Color(Color.Cyan1));

        var collection = mongoDbService.Voicings;

        // 1. The "Impossible" Chord
        await AnalyzeWidestStretch(collection);

        // 2. Jazz Harmony
        await AnalyzeJazzHarmony(collection);

        // 3. Beginner Friendly
        await AnalyzeBeginnerVoicings(collection);

        // 4. Positional Logic
        await AnalyzePositionLogic(collection);
    }

    private async Task AnalyzeWidestStretch(IMongoCollection<VoicingEntity> collection)
    {
        AnsiConsole.MarkupLine("\n[bold yellow]Use Case 1: Extreme Biomechanics (Widest Stretch)[/]");
        AnsiConsole.MarkupLine("[dim]Querying for max HandStretch > 5...[/]");

        var filter = Builders<VoicingEntity>.Filter.Gt(v => v.HandStretch, 5);
        var sort = Builders<VoicingEntity>.Sort.Descending(v => v.HandStretch);
        var result = await collection.Find(filter).Sort(sort).Limit(5).ToListAsync();

        if (result.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]No extreme stretches found (Check index depth)[/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Chord");
        table.AddColumn("Stretch");
        table.AddColumn("Diagram");
        table.AddColumn("Difficulty");

        foreach (var v in result)
        {
            table.AddRow(
                v.ChordName ?? "Unknown", 
                $"{v.HandStretch} frets", 
                v.Diagram, 
                v.Difficulty ?? "?"
            );
        }
        AnsiConsole.Write(table);
    }

    private async Task AnalyzeJazzHarmony(IMongoCollection<VoicingEntity> collection)
    {
        AnsiConsole.MarkupLine("\n[bold yellow]Use Case 2: Jazz Standards (Rootless & Drop Voicings)[/]");
        
        // Find Rootless Dominants
        var filter = Builders<VoicingEntity>.Filter.AnyEq(v => v.SemanticTags, "rootless") & 
                     Builders<VoicingEntity>.Filter.AnyEq(v => v.SemanticTags, "dominant");
        
        var count = await collection.CountDocumentsAsync(filter);
        AnsiConsole.MarkupLine($"Found [cyan]{count:N0}[/]."); // Removed text that might cause markup confusion

        // Sample G7 rootless
        AnsiConsole.MarkupLine("[dim]Sample: Rootless G7 voicings[/]");
        var g7Filter = filter & Builders<VoicingEntity>.Filter.Eq(v => v.ChordName, "G7");
        var g7Results = await collection.Find(g7Filter).Limit(3).ToListAsync();

        if (g7Results.Count > 0)
        {
            foreach (var v in g7Results)
            {
                AnsiConsole.MarkupLine($" - {v.Diagram} ({v.HandPosition})");
            }
        }
        else
        {
             AnsiConsole.MarkupLine("[dim]No rootless G7 found explicitly tagged 'G7' (might be under specific extensions)[/]");
        }
    }

    private async Task AnalyzeBeginnerVoicings(IMongoCollection<VoicingEntity> collection)
    {
        AnsiConsole.MarkupLine("\n[bold yellow]Use Case 3: Learner Pathways (Beginner Friendly)[/]");
        
        var filter = Builders<VoicingEntity>.Filter.Eq(v => v.Difficulty, "Beginner") &
                     Builders<VoicingEntity>.Filter.Eq(v => v.BarreRequired, false);

        var count = await collection.CountDocumentsAsync(filter);
        AnsiConsole.MarkupLine($"Found [cyan]{count:N0}[/cyan] beginner-friendly non-barre voicings.");

        // Show distribution of chords
        AnsiConsole.MarkupLine("[dim]Random easy chords to try:[/]");
        var pipeline = collection.Aggregate()
            .Match(filter)
            .Sample(5);
        
        var samples = await pipeline.ToListAsync();
        foreach (var v in samples)
        {
             // Use Markup.Escape to prevent chord names with brackets from breaking the parser
             var safeName = Markup.Escape(v.ChordName ?? "Unknown");
             var safeDiagram = Markup.Escape(v.Diagram);
             AnsiConsole.MarkupLine($" - [green]{safeName}[/]: {safeDiagram}");
        }
    }

    private async Task AnalyzePositionLogic(IMongoCollection<VoicingEntity> collection)
    {
        AnsiConsole.MarkupLine("\n[bold yellow]Use Case 4: Positional Mastery (Same chord, different places)[/]");
        
        var chordName = "C Major";
        var distinctPositions = await collection.Distinct(v => v.HandPosition, Builders<VoicingEntity>.Filter.Eq(v => v.ChordName, chordName)).ToListAsync();

        AnsiConsole.MarkupLine($"Positions found for [cyan]{chordName}[/]:");
        foreach(var pos in distinctPositions)
        {
            if (pos == null) continue;
            var posFilter = Builders<VoicingEntity>.Filter.And(
                Builders<VoicingEntity>.Filter.Eq(v => v.ChordName, chordName),
                Builders<VoicingEntity>.Filter.Eq(v => v.HandPosition, pos)
            );
            var example = await collection.Find(posFilter).FirstOrDefaultAsync();
            AnsiConsole.MarkupLine($" - {pos}: {example?.Diagram ?? "N/A"}");
        }
    }
}
