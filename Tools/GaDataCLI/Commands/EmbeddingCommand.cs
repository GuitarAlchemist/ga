namespace GaDataCLI.Commands;

using Azure.AI.OpenAI;
using MongoDB.Bson;
using MongoDB.Driver;
using Spectre.Console;

public class EmbeddingCommand
{
    public static async Task<int> ExecuteAsync(
        string connectionString,
        string databaseName,
        string collectionName,
        string openAiApiKey,
        string embeddingModel = "text-embedding-3-small",
        int batchSize = 100,
        bool useOpenAi = true,
        string? baseUrl = null)
    {
        AnsiConsole.MarkupLine("[bold cyan]Guitar Alchemist - Vector Embedding Generator[/]");
        AnsiConsole.MarkupLine("[dim]Generating embeddings for chord database...[/]\n");

        var isLocal = !string.IsNullOrEmpty(baseUrl) && !baseUrl.Contains("api.openai.com");

        if (string.IsNullOrEmpty(openAiApiKey) && useOpenAi && !isLocal)
        {
            AnsiConsole.MarkupLine("[red]Error: OpenAI API key not provided![/]");
            AnsiConsole.MarkupLine("[yellow]If usage local LLM (e.g. Ollama), specify --base-url[/]");
            return 1;
        }

        // Use dummy key for local
        if (string.IsNullOrEmpty(openAiApiKey) && isLocal)
        {
            openAiApiKey = "dummy-key";
            AnsiConsole.MarkupLine("[dim]Using dummy API key for locally hosted model[/]");
        }

        // Connect to MongoDB
        var client = new MongoClient(connectionString);
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
            return 0;
        }

        // Ask for confirmation
        if (!AnsiConsole.Confirm($"Generate embeddings for {withoutEmbeddings:N0} chords?"))
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
            return 0;
        }

        AzureOpenAIClient? openAiClient = null;
        if (useOpenAi)
        {
            var endpoint = string.IsNullOrEmpty(baseUrl) ? "https://api.openai.com/v1" : baseUrl;
            openAiClient = new(new(endpoint), new System.ClientModel.ApiKeyCredential(openAiApiKey));
            AnsiConsole.MarkupLine($"[green]Using Model:[/] {embeddingModel}");
            AnsiConsole.MarkupLine($"[green]Endpoint:[/] {endpoint}\n");
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
                var task = ctx.AddTask("[green]Generating embeddings[/]", maxValue: (double)withoutEmbeddings);

                var filter = Builders<BsonDocument>.Filter.Not(
                    Builders<BsonDocument>.Filter.Exists("Embedding"));

                var cursor = await collection.Find(filter).ToCursorAsync();
                var batch = new List<BsonDocument>();
                var processed = 0;

                while (await cursor.MoveNextAsync())
                {
                    foreach (var doc in cursor.Current)
                    {
                        batch.Add(doc);

                        if (batch.Count >= batchSize)
                        {
                            await ProcessBatch(batch, collection, openAiClient, embeddingModel);
                            processed += batch.Count;
                            task.Increment(batch.Count);
                            batch.Clear();
                        }
                    }
                }

                // Process remaining
                if (batch.Count > 0)
                {
                    await ProcessBatch(batch, collection, openAiClient, embeddingModel);
                    processed += batch.Count;
                    task.Increment(batch.Count);
                }
            });

        AnsiConsole.MarkupLine("\n[green]✓ Embedding generation complete![/]");

        // Verify
        var finalWithEmbeddings = await collection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Exists("Embedding"));
        AnsiConsole.MarkupLine($"[green]Total chords with embeddings:[/] {finalWithEmbeddings:N0}");

        return 0;
    }

    private static async Task ProcessBatch(
        List<BsonDocument> batch,
        IMongoCollection<BsonDocument> collection,
        AzureOpenAIClient? openAiClient,
        string model)
    {
        try
        {
            if (openAiClient == null) return;

            // Prepare texts for embedding
            var texts = batch.Select(doc => CreateEmbeddingText(doc)).ToList();

            // Generate embeddings using the new Azure.AI.OpenAI Client logic
            // Assuming we use the EmbeddingClient subclass or similar pattern in 2.0+
            var embeddingClient = openAiClient.GetEmbeddingClient(model);

            // Batch generation
            var response = await embeddingClient.GenerateEmbeddingsAsync(texts);

            // Update documents
            var updates = new List<WriteModel<BsonDocument>>();

            for (var i = 0; i < batch.Count; i++)
            {
                var doc = batch[i];
                // Accessing the Embedding from the response object
                var embedding = response.Value[i].ToFloats(); // Open AI 2.0 SDK

                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                var update = Builders<BsonDocument>.Update
                    .Set("Embedding", new BsonArray(embedding.ToArray().Select(x => new BsonDouble((double)x))))
                    .Set("EmbeddingModel", model);

                updates.Add(new UpdateOneModel<BsonDocument>(filter, update));
            }

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

        if (!string.IsNullOrEmpty(name)) parts.Add($"Chord: {name}");
        if (!string.IsNullOrEmpty(quality)) parts.Add($"Quality: {quality}");
        if (!string.IsNullOrEmpty(extension)) parts.Add($"Extension: {extension}");
        if (!string.IsNullOrEmpty(stackingType)) parts.Add($"Stacking: {stackingType}");
        if (!string.IsNullOrEmpty(constructionType)) parts.Add($"Construction: {constructionType}");
        if (!string.IsNullOrEmpty(description)) parts.Add($"Description: {description}");

        return string.Join(". ", parts);
    }
}
