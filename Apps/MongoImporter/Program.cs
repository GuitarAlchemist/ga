namespace MongoImporter;

using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using Spectre.Console;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        AnsiConsole.Write(
            new FigletText("MongoDB Importer")
                .Centered()
                .Color(Color.Green));

        AnsiConsole.MarkupLine("[dim]Guitar Alchemist Chord Data Import Tool[/]");
        AnsiConsole.WriteLine();

        // Configuration
        var dataFile = args.Length > 0 ? args[0] : @"C:\Temp\GaExport\all-chords.json";
        var connectionString = args.Length > 1 ? args[1] : "mongodb://localhost:27017";
        var databaseName = args.Length > 2 ? args[2] : "guitar-alchemist";
        var collectionName = args.Length > 3 ? args[3] : "chords";

        AnsiConsole.MarkupLine("[yellow]Configuration:[/]");
        AnsiConsole.MarkupLine($"  Data file: [blue]{dataFile}[/]");
        AnsiConsole.MarkupLine($"  Connection: [blue]{connectionString}[/]");
        AnsiConsole.MarkupLine($"  Database: [blue]{databaseName}[/]");
        AnsiConsole.MarkupLine($"  Collection: [blue]{collectionName}[/]");
        AnsiConsole.WriteLine();

        try
        {
            // Check if file exists
            if (!File.Exists(dataFile))
            {
                AnsiConsole.MarkupLine($"[red]✗ Error: Data file not found: {dataFile}[/]");
                AnsiConsole.MarkupLine("[yellow]Please export chord data first:[/]");
                AnsiConsole.MarkupLine(
                    "  dotnet run --project Apps/GaDataCLI/GaDataCLI.csproj -- -e chords -o C:\\Temp\\GaExport -q");
                return 1;
            }

            var fileInfo = new FileInfo(dataFile);
            AnsiConsole.MarkupLine($"[green]✓ Data file found ({fileInfo.Length / 1024 / 1024:F2} MB)[/]");

            // Connect to MongoDB
            AnsiConsole.MarkupLine("[yellow]Connecting to MongoDB...[/]");
            var client = new MongoClient(connectionString);
            var database = client.GetDatabase(databaseName);
            var collection = database.GetCollection<BsonDocument>(collectionName);

            // Test connection
            await database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
            AnsiConsole.MarkupLine("[green]✓ Connected to MongoDB[/]");

            // Check if collection already exists and has data
            var existingCount = await collection.CountDocumentsAsync(new BsonDocument());
            if (existingCount > 0)
            {
                AnsiConsole.MarkupLine($"[yellow]⚠ Collection already contains {existingCount} documents[/]");
                if (!AnsiConsole.Confirm("Do you want to drop the collection and reimport?"))
                {
                    AnsiConsole.MarkupLine("[yellow]Import cancelled[/]");
                    return 0;
                }

                await database.DropCollectionAsync(collectionName);
                AnsiConsole.MarkupLine("[green]✓ Collection dropped[/]");
            }

            // Read and parse JSON file
            AnsiConsole.MarkupLine("[yellow]Reading JSON file...[/]");
            string jsonContent;
            using (var reader = new StreamReader(dataFile))
            {
                jsonContent = await reader.ReadToEndAsync();
            }

            AnsiConsole.MarkupLine("[green]✓ JSON file loaded[/]");

            // Parse JSON array
            AnsiConsole.MarkupLine("[yellow]Parsing JSON data...[/]");
            var jsonArray = JsonDocument.Parse(jsonContent).RootElement;

            if (jsonArray.ValueKind != JsonValueKind.Array)
            {
                AnsiConsole.MarkupLine("[red]✗ Error: JSON file must contain an array[/]");
                return 1;
            }

            var totalDocuments = jsonArray.GetArrayLength();
            AnsiConsole.MarkupLine($"[green]✓ Found {totalDocuments:N0} documents[/]");

            // Import documents in batches
            const int batchSize = 1000;
            var imported = 0;

            await AnsiConsole.Progress()
                .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(),
                    new RemainingTimeColumn(), new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Importing documents[/]", maxValue: totalDocuments);

                    var batch = new List<BsonDocument>();

                    foreach (var element in jsonArray.EnumerateArray())
                    {
                        var bsonDoc = BsonDocument.Parse(element.GetRawText());
                        batch.Add(bsonDoc);

                        if (batch.Count >= batchSize)
                        {
                            await collection.InsertManyAsync(batch);
                            imported += batch.Count;
                            task.Increment(batch.Count);
                            batch.Clear();
                        }
                    }

                    // Insert remaining documents
                    if (batch.Count > 0)
                    {
                        await collection.InsertManyAsync(batch);
                        imported += batch.Count;
                        task.Increment(batch.Count);
                    }
                });

            AnsiConsole.MarkupLine($"[green]✓ Imported {imported:N0} documents[/]");

            // Create indexes
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Creating indexes...[/]");

            await AnsiConsole.Status()
                .StartAsync("[yellow]Creating indexes...[/]", async ctx =>
                {
                    // Compound index on Quality, Extension, StackingType
                    ctx.Status("[yellow]Creating compound index on Quality, Extension, StackingType...[/]");
                    await collection.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(
                            Builders<BsonDocument>.IndexKeys
                                .Ascending("Quality")
                                .Ascending("Extension")
                                .Ascending("StackingType")
                        )
                    );

                    // Index on PitchClassSet
                    ctx.Status("[yellow]Creating index on PitchClassSet...[/]");
                    await collection.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(
                            Builders<BsonDocument>.IndexKeys.Ascending("PitchClassSet")
                        )
                    );

                    // Index on ParentScale and ScaleDegree
                    ctx.Status("[yellow]Creating index on ParentScale and ScaleDegree...[/]");
                    await collection.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(
                            Builders<BsonDocument>.IndexKeys
                                .Ascending("ParentScale")
                                .Ascending("ScaleDegree")
                        )
                    );

                    // Text index on Name and Description
                    ctx.Status("[yellow]Creating text index on Name and Description...[/]");
                    await collection.Indexes.CreateOneAsync(
                        new CreateIndexModel<BsonDocument>(
                            Builders<BsonDocument>.IndexKeys
                                .Text("Name")
                                .Text("Description")
                        )
                    );
                });

            AnsiConsole.MarkupLine("[green]✓ Indexes created[/]");

            // Verify import
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Verifying import...[/]");
            var finalCount = await collection.CountDocumentsAsync(new BsonDocument());
            AnsiConsole.MarkupLine($"[green]✓ Final document count: {finalCount:N0}[/]");

            // Show sample document
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Sample document:[/]");
            var sample = await collection.Find(new BsonDocument()).FirstOrDefaultAsync();
            if (sample != null)
            {
                var panel = new Panel(sample.ToJson(new JsonWriterSettings { Indent = true }))
                {
                    Header = new PanelHeader("Sample Chord"),
                    Border = BoxBorder.Rounded
                };
                AnsiConsole.Write(panel);
            }

            // Summary
            AnsiConsole.WriteLine();
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[yellow]Metric[/]");
            table.AddColumn("[green]Value[/]");
            table.AddRow("Database", databaseName);
            table.AddRow("Collection", collectionName);
            table.AddRow("Documents Imported", $"{imported:N0}");
            table.AddRow("Final Count", $"{finalCount:N0}");
            table.AddRow("Indexes Created", "4");
            AnsiConsole.Write(table);

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[green]✓ Import completed successfully![/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Next steps:[/]");
            AnsiConsole.MarkupLine("  1. Test queries:");
            AnsiConsole.MarkupLine("     [dim]mongosh[/]");
            AnsiConsole.MarkupLine($"     [dim]use {databaseName}[/]");
            AnsiConsole.MarkupLine($"     [dim]db.{collectionName}.find({{ Quality: 'Major' }}).limit(5)[/]");
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
