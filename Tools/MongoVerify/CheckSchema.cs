namespace MongoVerify;

using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Spectre.Console;

internal class CheckSchema
{
    public static async Task Run()
    {
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("guitar-alchemist");
        var collection = database.GetCollection<BsonDocument>("chords");

        var sample = await collection.Find(new BsonDocument()).Limit(1).FirstOrDefaultAsync();

        if (sample != null)
        {
            AnsiConsole.MarkupLine("[yellow]Sample document fields:[/]");
            foreach (var element in sample.Elements)
            {
                AnsiConsole.MarkupLine($"  [green]{element.Name}[/]: {element.Value.BsonType}");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Full document:[/]");
            AnsiConsole.WriteLine(sample.ToJson(new JsonWriterSettings { Indent = true }));
        }
    }
}
