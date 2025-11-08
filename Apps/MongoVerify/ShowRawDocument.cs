namespace MongoVerify;

using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Spectre.Console;

internal class ShowRawDocument
{
    public static async Task Run()
    {
        var client = new MongoClient("mongodb://localhost:27017");
        var database = client.GetDatabase("guitar-alchemist");
        var collection = database.GetCollection<BsonDocument>("chords");

        var sample = await collection.Find(new BsonDocument()).Limit(1).FirstOrDefaultAsync();

        if (sample != null)
        {
            AnsiConsole.MarkupLine("[yellow]Raw document JSON:[/]");
            var json = sample.ToJson(new JsonWriterSettings { Indent = true });
            AnsiConsole.WriteLine(json);
        }
    }
}
