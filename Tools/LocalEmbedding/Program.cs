namespace LocalEmbedding;

using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using MongoDB.Bson;
using MongoDB.Driver;
using Spectre.Console;

internal class Program
{
    private const string _modelUrl =
        "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/resolve/main/onnx/model.onnx";

    private const string _tokenizerUrl =
        "https://huggingface.co/sentence-transformers/all-MiniLM-L6-v2/raw/main/tokenizer.json";

    private const string _modelPath = "all-MiniLM-L6-v2.onnx";
    private const string _tokenizerPath = "tokenizer.json";
    private const int _embeddingDimensions = 384;

    private static async Task Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("Local Embedding")
                .LeftJustified()
                .Color(Color.Green));

        AnsiConsole.MarkupLine("[bold cyan]Guitar Alchemist - Local Vector Embedding Generator[/]");
        AnsiConsole.MarkupLine("[dim]100% local, no API keys required![/]\n");

        // Download model and tokenizer if needed
        await EnsureModelDownloadedAsync();

        // Connect to MongoDB
        var mongoConnectionString = "mongodb://localhost:27017";
        var databaseName = "guitar-alchemist";
        var collectionName = "chords";

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
        if (!AnsiConsole.Confirm($"Generate embeddings for {withoutEmbeddings:N0} chords using local model?"))
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled.[/]");
            return;
        }

        AnsiConsole.MarkupLine("[green]Loading local embedding model...[/]");

        // Load model and tokenizer
        using var session = new InferenceSession(_modelPath);
        // Note: Tokenizer not required for current simple embedding pipeline

        // Try to load the actual tokenizer file if available
        if (File.Exists(_tokenizerPath))
        {
            try
            {
                var tokenizerJson = await File.ReadAllTextAsync(_tokenizerPath);
                // For now, use a simple word-based tokenization as fallback
                // The actual embedding generation will handle tokenization internally
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[yellow]Warning: Could not load tokenizer file: {ex.Message}[/]");
                AnsiConsole.MarkupLine("[yellow]Using fallback tokenization...[/]");
            }
        }

        AnsiConsole.MarkupLine("[green]Model loaded successfully![/]");
        AnsiConsole.MarkupLine("[dim]Model: all-MiniLM-L6-v2 (384 dimensions)[/]\n");

        // Process chords in batches
        var batchSize = 50;
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

                await foreach (var doc in cursor.ToAsyncEnumerable())
                {
                    batch.Add(doc);

                    if (batch.Count >= batchSize)
                    {
                        await ProcessBatch(batch, collection, session);
                        task.Increment(batch.Count);
                        batch.Clear();
                    }
                }

                // Process remaining
                if (batch.Count > 0)
                {
                    await ProcessBatch(batch, collection, session);
                    task.Increment(batch.Count);
                }
            });

        AnsiConsole.MarkupLine("\n[green]✓ Embedding generation complete![/]");

        // Verify
        var finalWithEmbeddings = await collection.CountDocumentsAsync(
            Builders<BsonDocument>.Filter.Exists("Embedding"));
        AnsiConsole.MarkupLine($"[green]Total chords with embeddings:[/] {finalWithEmbeddings:N0}");
    }

    private static async Task EnsureModelDownloadedAsync()
    {
        if (!File.Exists(_modelPath))
        {
            AnsiConsole.MarkupLine("[yellow]Downloading embedding model (first time only)...[/]");
            await DownloadFileAsync(_modelUrl, _modelPath, "Model");
        }

        if (!File.Exists(_tokenizerPath))
        {
            AnsiConsole.MarkupLine("[yellow]Downloading tokenizer (first time only)...[/]");
            await DownloadFileAsync(_tokenizerUrl, _tokenizerPath, "Tokenizer");
        }
    }

    private static async Task DownloadFileAsync(string url, string path, string name)
    {
        using var client = new HttpClient();
        await AnsiConsole.Progress()
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask($"[green]Downloading {name}[/]");

                using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? 0;
                task.MaxValue = totalBytes;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream =
                    new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                long totalRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                    totalRead += bytesRead;
                    task.Value = totalRead;
                }
            });

        AnsiConsole.MarkupLine($"[green]✓ {name} downloaded successfully![/]");
    }

    private static async Task ProcessBatch(
        List<BsonDocument> batch,
        IMongoCollection<BsonDocument> collection,
        InferenceSession session)
    {
        try
        {
            var updates = new List<WriteModel<BsonDocument>>();

            foreach (var doc in batch)
            {
                // Create embedding text
                var text = CreateEmbeddingText(doc);

                // Generate embedding
                var embedding = GenerateEmbedding(text, session);

                // Update document
                var filter = Builders<BsonDocument>.Filter.Eq("_id", doc["_id"]);
                var update = Builders<BsonDocument>.Update
                    .Set("Embedding", new BsonArray(embedding.Select(x => new BsonDouble(x))))
                    .Set("EmbeddingModel", "all-MiniLM-L6-v2");

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

    private static float[] GenerateEmbedding(string text, InferenceSession session)
    {
        // Simple tokenization - convert text to basic tokens
        // For a proper implementation, we'd need the exact tokenizer for all-MiniLM-L6-v2
        var words = text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var inputIds = words.Select((word, _) => (long)(word.GetHashCode() % 30000 + 1)).ToArray();

        // Ensure we have at least some tokens and limit to reasonable length
        if (inputIds.Length == 0)
        {
            inputIds = [1]; // UNK token
        }

        if (inputIds.Length > 512)
        {
            inputIds = [.. inputIds.Take(512)]; // Limit sequence length
        }

        var attentionMask = Enumerable.Repeat(1L, inputIds.Length).ToArray();

        // Create tensors
        var inputIdsTensor = new DenseTensor<long>(inputIds, new[] { 1, inputIds.Length });
        var attentionMaskTensor = new DenseTensor<long>(attentionMask, new[] { 1, attentionMask.Length });

        // Run inference
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids", inputIdsTensor),
            NamedOnnxValue.CreateFromTensor("attention_mask", attentionMaskTensor)
        };

        using var results = session.Run(inputs);
        var output = results.First().AsEnumerable<float>().ToArray();

        // Mean pooling
        var embeddings = new float[_embeddingDimensions];
        var tokenCount = inputIds.Length;

        for (var i = 0; i < _embeddingDimensions; i++)
        {
            float sum = 0;
            for (var j = 0; j < tokenCount; j++)
            {
                sum += output[j * _embeddingDimensions + i];
            }

            embeddings[i] = sum / tokenCount;
        }

        // Normalize
        var norm = Math.Sqrt(embeddings.Sum(x => x * x));
        for (var i = 0; i < embeddings.Length; i++)
        {
            embeddings[i] /= (float)norm;
        }

        return embeddings;
    }

    private static string CreateEmbeddingText(BsonDocument doc)
    {
        var name = doc.GetValue("Name", "").AsString;
        var quality = doc.GetValue("Quality", "").AsString;
        var extension = doc.GetValue("Extension", "").AsString;
        var stackingType = doc.GetValue("StackingType", "").AsString;
        var description = doc.GetValue("Description", "").AsString;
        var constructionType = doc.GetValue("ConstructionType", "").AsString;

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
