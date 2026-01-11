namespace GaCLI.Commands;

using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using MongoDB.Driver;
using Spectre.Console;

public class QueryVoicingsCommand(MongoDbService mongoDbService)
{
    public async Task ExecuteAsync(string? chordName = null)
    {
        AnsiConsole.Write(
            new FigletText("Voicing Query")
                .LeftJustified()
                .Color(Color.Blue));

        try
        {
            var collection = mongoDbService.Voicings;
            
            // 1. Get Total Count
            var count = await collection.CountDocumentsAsync(FilterDefinition<VoicingEntity>.Empty);
            AnsiConsole.MarkupLine($"[green]Total Voicings Indexed: {count:N0}[/]");

            if (count == 0)
            {
                AnsiConsole.MarkupLine("[red]No voicings found in the index.[/]");
                return;
            }

            // 2. Query Samples
            if (string.IsNullOrEmpty(chordName))
            {
                chordName = "C Major"; // Default query
            }

            AnsiConsole.MarkupLine($"\n[yellow]Querying for '{chordName}'...[/]");
            
            var filter = Builders<VoicingEntity>.Filter.Eq(v => v.ChordName, chordName);
            var results = await collection.Find(filter).Limit(10).ToListAsync();

            if (results.Count == 0)
            {
                AnsiConsole.MarkupLine($"[red]No voicings found for '{chordName}'[/]");
            }
            else
            {
                var table = new Table();
                table.AddColumn("Diagram");
                table.AddColumn("Positions");
                table.AddColumn("Difficulty");
                table.AddColumn("Tags");
                table.AddColumn("Type");

                foreach (var v in results)
                {
                    table.AddRow(
                        v.Diagram,
                        v.HandPosition ?? "?",
                        v.Difficulty ?? "?",
                        string.Join(", ", v.SemanticTags.Take(3)),
                        v.VoicingType ?? "-"
                    );
                }

                AnsiConsole.Write(table);
                
                // Show statistics for this chord
                var chordCount = await collection.CountDocumentsAsync(filter);
                AnsiConsole.MarkupLine($"[dim]Total variations for {chordName}: {chordCount:N0}[/]");
            }
            
            // 3. Query "Easy" voicings for this chord
            var easyFilter = Builders<VoicingEntity>.Filter.And(
                Builders<VoicingEntity>.Filter.Eq(v => v.ChordName, chordName),
                Builders<VoicingEntity>.Filter.Eq(v => v.Difficulty, "Easy")
            );
            
            var easyCount = await collection.CountDocumentsAsync(easyFilter);
            AnsiConsole.MarkupLine($"\n[green]Easy variations for {chordName}: {easyCount:N0}[/]");

        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }
}
