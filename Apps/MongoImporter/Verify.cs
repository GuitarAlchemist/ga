namespace MongoImporter;

using MongoDB.Bson;
using MongoDB.Driver;
using Spectre.Console;

internal class Verify
{
    public static async Task Run(string connectionString = "mongodb://localhost:27017",
        string databaseName = "guitar-alchemist")
    {
        try
        {
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>("chords");

            // Count documents
            var count = await collection.CountDocumentsAsync(new BsonDocument());
            AnsiConsole.MarkupLine($"[green]✓ Total documents: {count:N0}[/]");

            // Get a sample chord
            var sample = await collection.Find(new BsonDocument()).Limit(1).FirstOrDefaultAsync();
            if (sample != null)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[yellow]Sample chord:[/]");
                AnsiConsole.WriteLine($"  Name: {sample["Name"]}");
                AnsiConsole.WriteLine($"  Quality: {sample["Quality"]}");
                AnsiConsole.WriteLine($"  Extension: {sample["Extension"]}");
                AnsiConsole.WriteLine($"  Stacking Type: {sample["StackingType"]}");
                AnsiConsole.WriteLine($"  Note Count: {sample["NoteCount"]}");
            }

            // List indexes
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Indexes:[/]");
            var indexes = await collection.Indexes.List().ToListAsync();
            foreach (var index in indexes)
            {
                AnsiConsole.WriteLine($"  - {index["name"]}");
            }

            // Test a query
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Testing query (Major 7th chords):[/]");
            var majorSevenths = await collection.Find(new BsonDocument
            {
                { "Quality", "Major" },
                { "Extension", "Seventh" }
            }).Limit(5).ToListAsync();

            foreach (var chord in majorSevenths)
            {
                AnsiConsole.WriteLine($"  - {chord["Name"]}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ MongoDB import verified successfully![/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗ Error: {ex.Message}[/]");
        }
    }
}
