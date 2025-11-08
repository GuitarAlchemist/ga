namespace MongoVerify;

using MongoDB.Bson;
using MongoDB.Driver;
using Spectre.Console;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : "mongodb://localhost:27017";
        var databaseName = args.Length > 1 ? args[1] : "guitar-alchemist";

        AnsiConsole.Write(
            new FigletText("MongoDB Verify")
                .Centered()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[dim]Guitar Alchemist Data Verification[/]");
        AnsiConsole.WriteLine();

        try
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>("chords");

            // Count documents
            AnsiConsole.MarkupLine("[yellow]Counting documents...[/]");
            var count = await collection.CountDocumentsAsync(new BsonDocument());
            AnsiConsole.MarkupLine($"[green]✓ Total documents: {count:N0}[/]");

            // Get a sample chord
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Sample chord:[/]");
            var sample = await collection.Find(new BsonDocument()).Limit(1).FirstOrDefaultAsync();
            if (sample != null)
            {
                var table = new Table();
                table.Border(TableBorder.Rounded);
                table.AddColumn("[yellow]Field[/]");
                table.AddColumn("[green]Value[/]");
                table.AddRow("Name", sample["Name"].ToString());
                table.AddRow("Quality", sample["Quality"].ToString());
                table.AddRow("Extension", sample["Extension"].ToString());
                table.AddRow("Stacking Type", sample["StackingType"].ToString());
                table.AddRow("Note Count", sample["NoteCount"].ToString());
                AnsiConsole.Write(table);
            }

            // List indexes
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Indexes:[/]");
            var indexes = await collection.Indexes.List().ToListAsync();
            foreach (var index in indexes)
            {
                AnsiConsole.MarkupLine($"  [green]✓[/] {index["name"]}");
            }

            // Test queries
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Testing queries...[/]");

            // Query 1: Major 7th chords
            var majorSevenths = await collection.Find(new BsonDocument
            {
                { "Quality", "Major" },
                { "Extension", "Seventh" }
            }).Limit(5).ToListAsync();

            AnsiConsole.MarkupLine($"[green]✓[/] Found {majorSevenths.Count} Major 7th chords (showing 5):");
            foreach (var chord in majorSevenths)
            {
                AnsiConsole.MarkupLine($"    - {chord["Name"]}");
            }

            // Query 2: Tertian chords
            var tertianCount = await collection.CountDocumentsAsync(new BsonDocument
            {
                { "StackingType", "Tertian" }
            });
            AnsiConsole.MarkupLine($"[green]✓[/] Tertian chords: {tertianCount:N0}");

            // Query 3: Quartal chords
            var quartalCount = await collection.CountDocumentsAsync(new BsonDocument
            {
                { "StackingType", "Quartal" }
            });
            AnsiConsole.MarkupLine($"[green]✓[/] Quartal chords: {quartalCount:N0}");

            // Query 4: Quintal chords
            var quintalCount = await collection.CountDocumentsAsync(new BsonDocument
            {
                { "StackingType", "Quintal" }
            });
            AnsiConsole.MarkupLine($"[green]✓[/] Quintal chords: {quintalCount:N0}");

            // Summary
            AnsiConsole.WriteLine();
            var summaryTable = new Table();
            summaryTable.Border(TableBorder.Rounded);
            summaryTable.AddColumn("[yellow]Metric[/]");
            summaryTable.AddColumn("[green]Value[/]");
            summaryTable.AddRow("Database", databaseName);
            summaryTable.AddRow("Collection", "chords");
            summaryTable.AddRow("Total Documents", $"{count:N0}");
            summaryTable.AddRow("Indexes", indexes.Count.ToString());
            summaryTable.AddRow("Tertian Chords", $"{tertianCount:N0}");
            summaryTable.AddRow("Quartal Chords", $"{quartalCount:N0}");
            summaryTable.AddRow("Quintal Chords", $"{quintalCount:N0}");
            AnsiConsole.Write(summaryTable);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ MongoDB import verified successfully![/]");
            AnsiConsole.WriteLine();

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message}[/]");
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}
