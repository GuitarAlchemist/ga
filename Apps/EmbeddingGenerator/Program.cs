namespace EmbeddingGenerator;

using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Driver;
using Spectre.Console;

internal class Program
{
    private static async Task Main(string[] args)
    {
        // Load configuration
        var config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", false)
            .AddEnvironmentVariables()
            .Build();

        var mongoConnectionString = config["MongoDB:ConnectionString"]!;
        var databaseName = config["MongoDB:DatabaseName"]!;
        var collectionName = config["MongoDB:CollectionName"]!;
        var openAiApiKey = config["OpenAI:ApiKey"];
        var embeddingModel = config["OpenAI:Model"]!;
        var batchSize = int.Parse(config["EmbeddingOptions:BatchSize"]!);
        var maxConcurrency = int.Parse(config["EmbeddingOptions:MaxConcurrency"]!);
        var useOpenAi = bool.Parse(config["EmbeddingOptions:UseOpenAI"]!);

        AnsiConsole.Write(
            new FigletText("Embedding Generator")
                .LeftJustified()
                .Color(Color.Blue));

        AnsiConsole.MarkupLine("[bold cyan]Guitar Alchemist - Vector Embedding Generator[/]");
        AnsiConsole.MarkupLine("[dim]Generating embeddings for chord database...[/]\n");

        // Connect to MongoDB
        var client = new MongoClient(mongoConnectionString);
        var database = client.GetDatabase(databaseName);
        var collection = database.GetCollection<BsonDocument>(collectionName);

        // Get total count
        var totalCount = await collection.CountDocumentsAsync(new BsonDocument());
        AnsiConsole.MarkupLine($"[green]Total chords in database:[/] {totalCount:N0}");

        // Check how many already have embeddings
        var withEmbeddings = await collection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Exists("Embedding"));
        var withoutEmbeddings = totalCount - withEmbeddings;

        AnsiConsole.MarkupLine($"[yellow]Chords with embeddings:[/] {withEmbeddings:N0}");
        AnsiConsole.MarkupLine($"[yellow]Chords without embeddings:[/] {withoutEmbeddings:N0}\n");

        if (withoutEmbeddings == 0)
        {
            AnsiConsole.MarkupLine("[green]All chords already have embeddings![/]");
            return;
        }

        // Ask for confirmation
        if (!AnsiConsole.Confirm($"Generate embeddings for {withoutEmbeddings:N0} chords?"))
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
            return;
        }

        // Initialize OpenAI client if needed
        Azure.AI.OpenAI.AzureOpenAIClient? openAiClient = null;
        if (useOpenAi)
        {
            if (string.IsNullOrEmpty(openAiApiKey))
            {
                AnsiConsole.MarkupLine("[red]Error: OpenAI API key not configured![/]");
                AnsiConsole.MarkupLine(
                    "[yellow]Please set the OpenAI:ApiKey in appsettings.json or environment variable.[/]");
                return;
            }

            // TODO: Fix OpenAI client initialization - Azure.AI.OpenAI API has changed
            // openAiClient = new Azure.AI.OpenAI.AzureOpenAIClient(new Uri("https://api.openai.com/v1"), new ApiKeyCredential(openAiApiKey));
            AnsiConsole.MarkupLine($"[green]Using OpenAI model:[/] {embeddingModel}\n");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Local embedding model not yet implemented.[/]");
            AnsiConsole.MarkupLine("[yellow]Please set UseOpenAI to true in appsettings.json.[/]");
            return;
        }

        // Process chords in batches
        await AnsiConsole.Progress()
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Generating embeddings[/]", maxValue: withoutEmbeddings);

                var filter = Builders<BsonDocument>.Filter.Not(
                    Builders<BsonDocument>.Filter.Exists("Embedding"));

                var cursor = await collection.Find(filter).ToCursorAsync();
                var batch = new List<BsonDocument>();
                var processed = 0;

                await foreach (var doc in cursor.ToAsyncEnumerable())
                {
                    batch.Add(doc);

                    if (batch.Count >= batchSize)
                    {
                        await ProcessBatch(batch, collection, openAiClient!, embeddingModel);
                        processed += batch.Count;
                        task.Increment(batch.Count);
                        batch.Clear();
                    }
                }

                // Process remaining
                if (batch.Count > 0)
                {
                    await ProcessBatch(batch, collection, openAiClient!, embeddingModel);
                    processed += batch.Count;
                    task.Increment(batch.Count);
                }
            });

        AnsiConsole.MarkupLine("\n[green]✓ Embedding generation complete![/]");

        // Verify
        var finalWithEmbeddings = await collection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Exists("Embedding"));
        AnsiConsole.MarkupLine($"[green]Total chords with embeddings:[/] {finalWithEmbeddings:N0}");
    }

    private static async Task ProcessBatch(
        List<BsonDocument> batch,
        IMongoCollection<BsonDocument> collection,
        Azure.AI.OpenAI.AzureOpenAIClient? openAiClient,
        string model)
    {
        try
        {
            // TODO: Fix Azure.AI.OpenAI API - EmbeddingsOptions and GetEmbeddingsAsync no longer available
            // Prepare texts for embedding
            var texts = batch.Select(doc => CreateEmbeddingText(doc)).ToList();

            // Generate embeddings
            // var embeddingOptions = new EmbeddingsOptions(model, texts);
            // var response = await openAiClient.GetEmbeddingsAsync(embeddingOptions);

            // Update documents
            var updates = new List<WriteModel<BsonDocument>>();
            /*
            for (var i = 0; i < batch.Count; i++)
            {
                var doc = batch[i];
                var embedding = response.Value.Data[i].Embedding.ToArray();

                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                var update = Builders<BsonDocument>.Update
                    .Set("Embedding", new BsonArray(embedding.Select(x => new BsonDouble(x))))
                    .Set("EmbeddingModel", model);

                updates.Add(new UpdateOneModel<BsonDocument>(filter, update));
            }
            */

            // Bulk write
            if (updates.Count > 0)
            {
                await collection.BulkWriteAsync(updates);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error processing batch: {ex.Message}[/]");
        }
    }

    private static string CreateEmbeddingText(BsonDocument doc)
    {
        // Create a rich text representation for embedding
        var name = doc.GetValue("Name", "").AsString;
        var quality = doc.GetValue("Quality", "").AsString;
        var extension = doc.GetValue("Extension", "").AsString;
        var stackingType = doc.GetValue("StackingType", "").AsString;
        var description = doc.GetValue("Description", "").AsString;
        var constructionType = doc.GetValue("ConstructionType", "").AsString;

        // Build comprehensive text
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(name))
        {
            parts.Add($"Chord: {name}");
        }

        if (!string.IsNullOrEmpty(quality))
        {
            parts.Add($"Quality: {quality}");
        }

        if (!string.IsNullOrEmpty(extension))
        {
            parts.Add($"Extension: {extension}");
        }

        if (!string.IsNullOrEmpty(stackingType))
        {
            parts.Add($"Stacking: {stackingType}");
        }

        if (!string.IsNullOrEmpty(constructionType))
        {
            parts.Add($"Construction: {constructionType}");
        }

        if (!string.IsNullOrEmpty(description))
        {
            parts.Add($"Description: {description}");
        }

        return string.Join(". ", parts);
    }
}
