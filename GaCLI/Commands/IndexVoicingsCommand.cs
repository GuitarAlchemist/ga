namespace GaCLI.Commands;

using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Analysis;
using GA.Business.Core.Fretboard.Voicings.Generation;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Data.MongoDB.Models;
using GA.Data.MongoDB.Services;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Spectre.Console;
using System.Diagnostics;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Notes.Primitives;
using GA.Business.Core.Fretboard.Positions;
using GA.Business.ML.Embeddings;
using System.Threading.Channels;

public class IndexVoicingsCommand
{
    private readonly MusicalEmbeddingGenerator _embeddingGenerator;
    private readonly OnnxEmbeddingGenerator _textEmbeddingGenerator;
    private readonly MongoDbService _mongoDbService;
    private readonly IVectorIndex _vectorIndex;
    private readonly ILogger<IndexVoicingsCommand> _logger;

    public IndexVoicingsCommand(
        MongoDbService mongoDbService, 
        ILogger<IndexVoicingsCommand> logger,
        MusicalEmbeddingGenerator musicalGenerator,
        OnnxEmbeddingGenerator onnxGenerator,
        IVectorIndex vectorIndex)
    {
         _mongoDbService = mongoDbService;
         _logger = logger;
         _embeddingGenerator = musicalGenerator;
         _textEmbeddingGenerator = onnxGenerator;
         _vectorIndex = vectorIndex;
    }
    public async Task ExecuteAsync(bool indexAll = false, int limit = 1000, int windowSize = 4, int minPlayedNotes = 2, bool force = false, bool drop = false, bool seed = false)
    {
        AnsiConsole.Write(
            new FigletText("Voicing Indexer")
                .LeftJustified()
                .Color(Color.Green));

        if (seed)
        {
            await SeedOracleAsync(drop);
            return;
        }

        if (!force)
        {
            if (!indexAll && limit > 10000)
            {
                if (!AnsiConsole.Confirm($"You are about to index {limit} voicings. Continue?"))
                    return;
            }
            else if (indexAll)
            {
                if (!AnsiConsole.Confirm($"You are about to index ALL playable voicings with window={windowSize}, minNotes={minPlayedNotes}. Continue?"))
                    return;
            }
        }

        try
        {
            // 1. Generate Voicings
            var fretboard = Fretboard.Default;

            AnsiConsole.MarkupLine($"[yellow]Generating voicings (Span constraint: {windowSize} frets)...[/]");
            var stopwatch = Stopwatch.StartNew();

            // Use the generator
            // Note: For "ALL", we might want to iterate differently, but GenerateAllVoicings is efficient enough for now
            // as it uses parallel processing.
            // If indexAll is true, we ignore limit within the generator, but we might want to cap it for safety
            // or just let it run.

            // To prevent OOM on "ALL" (millions), we should probably stream or batch key generation.
            // But VoicingGenerator.GenerateAllVoicings returns a List.
            // Let's use the AsyncEnumerable version if available or just the List for now if memory holds.
            // The Gpu command loads 400k+ into RAM, so it should be fine for now.

            List<Voicing>? voicingsList = null;
            if (!indexAll)
            {
                // Just generate enough to satisfy limit (simple list approach)
                var all = VoicingGenerator.GenerateAllVoicings(fretboard, windowSize, minPlayedNotes);
                voicingsList = all
                    .DistinctBy(v => VoicingExtensions.GetPositionDiagram(v.Positions))
                    .Take(limit)
                    .ToList();
                AnsiConsole.MarkupLine($"[green]Generated {voicingsList.Count:N0} voicings in {stopwatch.Elapsed.TotalSeconds:F1}s[/]");
            }
            else
            {
                 AnsiConsole.MarkupLine($"[green]Starting stream generation for ALL voicings...[/]");
            }
            stopwatch.Stop();

            // 2. Prepare for Indexing
            AnsiConsole.MarkupLine("[yellow]Connecting to MongoDB...[/]");
            var collection = _mongoDbService.Voicings;
            
            // Drop collection if requested (for clean re-index with schema changes)
            if (drop)
            {
                AnsiConsole.MarkupLine("[red]Dropping existing voicings collection...[/]");
                await _mongoDbService.Database.DropCollectionAsync("voicings");
                AnsiConsole.MarkupLine("[green]Collection dropped.[/]");
            }
            
            // Create indexes if they don't exist
            AnsiConsole.MarkupLine("[yellow]Ensuring indexes...[/]");
            var indexKeys = Builders<VoicingEntity>.IndexKeys;
            await collection.Indexes.CreateManyAsync([
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.Diagram), new CreateIndexOptions { Unique = true }),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.ChordName)),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.PitchClasses)),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.SemanticTags)),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.ForteCode)),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.PrimeFormId)),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.Difficulty)),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.HandStretch)),
                new CreateIndexModel<VoicingEntity>(indexKeys.Ascending(v => v.MinFret)),
                // Compound index for common filter combos could be added here too
            ]);
            AnsiConsole.MarkupLine("[green]Indexes verified.[/]");

            var processedCount = 0;
            var batchSize = 1000;

            await AnsiConsole.Progress()
                .Columns(new ProgressColumn[] 
                {
                    new SpinnerColumn(),
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new ElapsedTimeColumn(),
                    new RemainingTimeColumn(),
                })
                .StartAsync(async ctx =>
                {
                    // Estimate total
                    var total = indexAll ? (windowSize == 6 ? 5000000 : 700000) : voicingsList!.Count;
                    
                    var task = ctx.AddTask("[green]Indexing[/]", maxValue: total);
                    var batchStopwatch = Stopwatch.StartNew();

                    // Create a channel for processed entities (DB Writer Buffer)
                    var channel = Channel.CreateBounded<VoicingEntity>(new BoundedChannelOptions(batchSize * 4) 
                    {
                        SingleWriter = false,
                        SingleReader = true,
                        FullMode = BoundedChannelFullMode.Wait
                    });

                    // CONSUMER: Batch Writer Task
                    var consumerTask = Task.Run(async () =>
                    {
                        var batch = new List<VoicingEntity>(batchSize);
                        
                        try
                        {
                            await foreach (var entity in channel.Reader.ReadAllAsync())
                            {
                                batch.Add(entity);
                                
                                if (batch.Count >= batchSize)
                                {
                                    await BulkUpsertAsync(collection, batch);
                                    
                                    // Also index into Vector Store (Qdrant)
                                    // Verify we have embeddings (we should)
                                    var vectorDocs = batch.Select(e => MapToDocument(e)).ToList();
                                    // IVectorIndex is read-only in the interface definition I saw earlier? 
                                    // Wait, I implemented IndexAsync in QdrantVectorIndex but did I add it to the Interface?
                                    // I need to check if IVectorIndex HAS IndexAsync. 
                                    // If not, I need to cast or update interface. 
                                    // Assuming I updated interface or will update it.
                                    if (_vectorIndex is QdrantVectorIndex qvi) 
                                    {
                                        await qvi.IndexAsync(vectorDocs);
                                    }
                                    // Ideally IVectorIndex should have IndexAsync.
                                    var count = batch.Count;
                                    batch.Clear();
                                    
                                    processedCount += count;
                                    task.Increment(count);
                                    
                                    var rate = processedCount / batchStopwatch.Elapsed.TotalSeconds;
                                    task.Description = $"[green]Indexing ({rate:N0} items/s)[/]";
                                }
                            }
                            
                            // Flush remaining
                            if (batch.Count > 0)
                            {
                                await BulkUpsertAsync(collection, batch);
                                
                                if (_vectorIndex is QdrantVectorIndex qvi) 
                                {
                                     await qvi.IndexAsync(batch.Select(e => MapToDocument(e)));
                                }

                                processedCount += batch.Count;
                                task.Increment(batch.Count);
                            }
                        }
                        catch (Exception ex)
                        {
                             _logger.LogError(ex, "Error in DB Writer Consumer");
                        }
                    });

                    // PRODUCER: Parallel Processing
                    var parallelOptions = new ParallelOptions 
                    { 
                        MaxDegreeOfParallelism = Environment.ProcessorCount
                    };

                    var source = indexAll 
                        ? VoicingGenerator.GenerateAllVoicingsAsync(fretboard, windowSize, minPlayedNotes)
                        : ConvertToListToAsyncEnumerable(voicingsList!);

                    await Parallel.ForEachAsync(source, parallelOptions, async (voicing, ct) => 
                    {
                        try
                        {
                            // This now includes Embedding Generation (Parallel CPU)
                            var entity = await ProcessVoicingEntityAsync(voicing);
                            await channel.Writer.WriteAsync(entity, ct);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process voicing");
                        }
                    });
                    
                    // Complete writer and wait for consumer
                    channel.Writer.Complete();
                    await consumerTask;
                });

            AnsiConsole.MarkupLine($"[green]Successfully indexed {processedCount:N0} voicings![/]");

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index voicings");
            AnsiConsole.WriteException(ex);
        }
    }

    private static async IAsyncEnumerable<Voicing> ConvertToListToAsyncEnumerable(List<Voicing> list)
    {
        foreach (var item in list)
        {
            yield return item;
            await Task.Yield();
        }
    }
    
    // Method renamed back to ProcessVoicingEntityAsync and includes embedding logic
    private async Task<VoicingEntity> ProcessVoicingEntityAsync(Voicing voicing)
    {
        // Analyze
        var decomposed = new DecomposedVoicing(voicing, null, null, null);
        var analysis = VoicingAnalyzer.AnalyzeEnhanced(decomposed);

        // Enrich with Semantic Tags (AI Layer)
        var semanticTags = GA.Business.AI.Interpretation.InterpretationService.GenerateSemanticTags(
            analysis.ChordId,
            analysis.VoicingCharacteristics,
            analysis.ModeInfo,
            analysis.PhysicalLayout,
            analysis.PlayabilityInfo,
            analysis.PerceptualQualities);
        
        // Map to Entity
        var entity = MapToEntity(voicing, analysis);
        entity.SemanticTags = semanticTags.ToArray();
        
        // Generate Embedding
        var doc = new VoicingDocument
        {
            Id = entity.Id,
            ChordName = entity.ChordName,
            VoicingType = entity.VoicingType,
            Diagram = entity.Diagram,
            MidiNotes = entity.MidiNotes,
            PitchClasses = entity.PitchClasses,
            IntervalClassVector = entity.IntervalClassVector ?? "",
            MinFret = entity.MinFret,
            MaxFret = entity.MaxFret,
            BarreRequired = entity.BarreRequired,
            HandStretch = entity.HandStretch,
            StackingType = entity.StackingType,
            RootPitchClass = entity.RootPitchClass,
            MidiBassNote = entity.MidiBassNote,
            HarmonicFunction = entity.HarmonicFunction,
            IsNaturallyOccurring = entity.IsNaturallyOccurring,
            Consonance = entity.ConsonanceScore,
            Brightness = entity.Brightness,
            IsRootless = entity.IsRootless,
            HasGuideTones = entity.HasGuideTones,
            Inversion = entity.Inversion,
            SearchableText = entity.SearchableText,
            PitchClassSet = string.Join(",", entity.PitchClasses),
            PossibleKeys = entity.PossibleKeys,
            SemanticTags = entity.SemanticTags,
            YamlAnalysis = "",
            AnalysisEngine = "VoicingIndexer", 
            AnalysisVersion = "1.0",
            Jobs = [],
            TuningId = "Standard",
            PitchClassSetId = entity.PrimeFormId ?? "",
            CagedShape = entity.CagedShape 
        };

        entity.Embedding = await _embeddingGenerator.GenerateEmbeddingAsync(doc);
        entity.EmbeddingModel = "MusicalFeature-v1";
        
        // Text embedding is optional - may not have ONNX model available
        try
        {
            entity.TextEmbedding = await _textEmbeddingGenerator.GenerateEmbeddingAsync(doc.SearchableText ?? doc.ChordName ?? "");
            if (entity.TextEmbedding != null && entity.TextEmbedding.Length > 0 && !entity.TextEmbedding.All(x => x == 0))
            {
                 entity.TextEmbeddingModel = "all-MiniLM-L6-v2";
            }
        }
        catch
        {
            // ONNX model not available, continue without text embedding
        }
        
        return entity;
    }

    private async Task SeedOracleAsync(bool drop)
    {
        AnsiConsole.MarkupLine("[yellow]Seeding Ground Truth Oracle voicings...[/]");
        
        var fretboard = Fretboard.Default;
        var diagrams = new[]
        {
            "x-3-2-0-1-0", // C
            "x-0-2-2-2-0", // A
            "x-0-2-2-1-0", // Am
            "0-2-2-1-0-0", // E
            "0-2-2-0-0-0", // Em
            "3-2-0-0-0-3", // G
            "x-x-0-2-3-2", // D
            "x-x-0-2-3-1", // Dm
            "1-3-3-2-1-1", // F
            "x-1-3-3-3-1", // Bb
            "3-2-0-0-0-1", // G7
            "x-0-2-0-2-0", // A7
            "0-2-0-1-0-0", // E7
            "0-7-6-7-8-0",  // E7#9
            // --- INVERSIONS ---
            "0-3-2-0-1-0", // C/E (1st inversion)
            "x-2-0-0-0-3", // G/B (1st inversion)
            "2-0-0-2-3-2", // D/F# (1st inversion)
            // --- SHELLS / JAZZ ---
            "3-x-3-4-x-x", // G7 shell (3rd/7th)
            "x-3-x-3-4-x", // C7 shell
            "5-x-5-5-x-x", // Am7 shell
            // --- HIGH REGISTER / UPPER STRUCTURES ---
            "x-x-12-12-12-12", // G6 (High register)
            "x-x-14-14-14-14", // A6 (High register)
            "x-x-15-16-15-15", // G7 high voicing
            "x-x-x-17-19-19",  // A high voicing
            "x-x-x-x-22-24"    // E-E high dyad
        };

        if (drop)
        {
            AnsiConsole.MarkupLine("[red]Dropping existing voicings collection...[/]");
            await _mongoDbService.Database.DropCollectionAsync("voicings");
        }

        var collection = _mongoDbService.Voicings;
        var batch = new List<VoicingEntity>();

        foreach (var d in diagrams)
        {
            var voicing = ParseDiagram(fretboard, d);
            var entity = await ProcessVoicingEntityAsync(voicing);
            batch.Add(entity);
        }

        await BulkUpsertAsync(collection, batch);

        if (_vectorIndex is QdrantVectorIndex qvi) 
        {
             await qvi.IndexAsync(batch.Select(e => MapToDocument(e)));
        }

        AnsiConsole.MarkupLine($"[green]Successfully seeded {batch.Count} Ground Truth voicings![/]");
    }

    private Voicing ParseDiagram(Fretboard fretboard, string diagram)
    {
        var partsLowToHigh = diagram.Split('-');
        var positions = new Position[partsLowToHigh.Length];
        var midiNotes = new List<MidiNote>();

        for (int i = 0; i < partsLowToHigh.Length; i++)
        {
            var part = partsLowToHigh[i];
            
            // i=0 is Low E string (Str 6)
            // i=5 is High E string (Str 1)
            int stringNumber = partsLowToHigh.Length - i; 

            if (part == "x")
            {
                positions[i] = new Position.Muted(new Str(stringNumber));
            }
            else
            {
                var fret = int.Parse(part);
                var location = new PositionLocation(new Str(stringNumber), new Fret(fret));
                
                var openStringPitch = fretboard.Tuning[new Str(stringNumber)];
                var midiNote = openStringPitch.MidiNote + fret;
                
                positions[i] = new Position.Played(location, midiNote);
                midiNotes.Add(midiNote);
            }
        }

        return new Voicing(positions, midiNotes.ToArray());
    }

    private static async Task BulkUpsertAsync(IMongoCollection<VoicingEntity> collection, List<VoicingEntity> entities)
    {
        var models = new List<WriteModel<VoicingEntity>>();
        foreach (var entity in entities)
        {
            var filter = Builders<VoicingEntity>.Filter.Eq(x => x.Diagram, entity.Diagram);
            models.Add(new ReplaceOneModel<VoicingEntity>(filter, entity) { IsUpsert = true });
        }

        if (models.Count > 0)
            await collection.BulkWriteAsync(models);
    }

    private static VoicingEntity MapToEntity(Voicing voicing, MusicalVoicingAnalysis analysis)
    {
        var diagram = VoicingExtensions.GetPositionDiagram(voicing.Positions);
        var id = $"voicing_{diagram.Replace("-", "_").Replace("x", "m")}";

        return new VoicingEntity
        {
            // === Core Identification ===
            Id = id,
            Diagram = diagram,
            SearchableText = $"{analysis.ChordId.ChordName} {string.Join(" ", analysis.SemanticTags)} {analysis.ModeInfo?.ModeName}",

            // === LAYER 1: IDENTITY ===
            ChordName = analysis.ChordId.ChordName,
            AlternateChordNames = analysis.AlternateChordNames?.ToArray(),
            RequiresContext = analysis.RequiresContext,
            MidiNotes = analysis.MidiNotes.Select(n => n.Value).ToArray(),
            PitchClasses = analysis.PitchClassSet.Select(p => p.Value).ToArray(),
            PrimeFormId = analysis.EquivalenceInfo?.PrimeFormId,
            ForteCode = analysis.EquivalenceInfo?.ForteCode,
            IntervalClassVector = analysis.IntervallicInfo.IntervalClassVector,
            ModeName = analysis.ModeInfo?.ModeName,
            ClosestKey = analysis.ChordId.ClosestKey?.ToString(),
            RomanNumeral = analysis.ChordId.RomanNumeral,
            HarmonicFunction = analysis.ChordId.HarmonicFunction,
            IsNaturallyOccurring = analysis.ChordId.IsNaturallyOccurring,

            // === LAYER 2: SOUND ===
            Tuning = "Standard", // TODO: Get from context when multi-tuning support is added
            CapoPosition = 0,
            VoicingType = analysis.VoicingCharacteristics.VoicingType,
            VoicingSpan = analysis.VoicingCharacteristics.Span,
            IsRootless = analysis.VoicingCharacteristics.IsRootless,
            TonesPresent = analysis.ToneInventory.TonesPresent.ToArray(),
            DoubledTones = analysis.ToneInventory.DoubledTones.Count > 0 ? analysis.ToneInventory.DoubledTones.ToArray() : null,
            OmittedTones = analysis.ToneInventory.OmittedTones.Count > 0 ? analysis.ToneInventory.OmittedTones.ToArray() : null,
            HasGuideTones = analysis.ToneInventory.HasGuideTones,
            Register = analysis.PerceptualQualities.Register,
            Brightness = analysis.PerceptualQualities.Brightness,
            ConsonanceScore = analysis.PerceptualQualities.ConsonanceScore,
            MayBeMuddy = analysis.PerceptualQualities.MayBeMuddy,
            Roughness = analysis.PerceptualQualities.Roughness,
            Spacing = analysis.PerceptualQualities.Spacing,
            StackingType = null, // analysis.VoicingCharacteristics.StackingType,
            Inversion = 0, // analysis.ChordId.Inversion,
            MidiBassNote = analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0].Value : 0,
            RootConfidence = analysis.ChordId.RootConfidence,
            RootPitchClass = analysis.ChordId.RootPitchClass?.Value,

            // === LAYER 3: HANDS ===
            HandPosition = analysis.PhysicalLayout.HandPosition,
            Difficulty = analysis.PlayabilityInfo.Difficulty,
            DifficultyScore = analysis.PlayabilityInfo.DifficultyScore,
            MinFret = analysis.PhysicalLayout.MinFret,
            MaxFret = analysis.PhysicalLayout.MaxFret,
            HandStretch = analysis.PlayabilityInfo.HandStretch,
            BarreRequired = analysis.PlayabilityInfo.BarreRequired,
            BarreInfo = analysis.PlayabilityInfo.BarreInfo,
            FingerAssignment = analysis.ErgonomicsInfo.FingerAssignment,
            StringSkips = analysis.ErgonomicsInfo.StringSkips,
            MinimumFingers = analysis.PlayabilityInfo.MinimumFingers,
            StringSet = analysis.PhysicalLayout.StringSet,
            CagedShape = analysis.PlayabilityInfo.CagedShape,
            ShellFamily = analysis.PlayabilityInfo.ShellFamily,

            // === CONTEXTUAL HOOKS ===
            CommonSubstitutions = analysis.ContextualHooks.CommonSubstitutions?.ToArray(),
            PlayStyles = analysis.ContextualHooks.PlayStyles?.ToArray(),
            GenreTags = analysis.ContextualHooks.GenreTags?.ToArray(),
            SongReferences = analysis.ContextualHooks.SongReferences?.ToArray(),

            // === METADATA ===
            PossibleKeys = analysis.PitchClassSet.GetCompatibleKeys().Select(k => k.ToString()).ToArray(),
            SemanticTags = analysis.SemanticTags.ToArray(),
            Embedding = null,
            EmbeddingModel = null,
            FullAnalysis = null,
            LastUpdated = DateTime.UtcNow,
            
            // === TRACEABILITY ===
            AnalysisEngine = VoicingAnalyzer.AnalysisEngineName,
            AnalysisVersion = VoicingAnalyzer.AnalysisVersionStamp,
            Jobs = []
        };
    }

    private static VoicingDocument MapToDocument(VoicingEntity e)
    {
        return new VoicingDocument
        {
            Id = e.Id,
            ChordName = e.ChordName,
            Diagram = e.Diagram,
            SearchableText = e.SearchableText ?? "",
            PossibleKeys = e.PossibleKeys ?? [],
            SemanticTags = e.SemanticTags ?? [],
            YamlAnalysis = e.FullAnalysis ?? "", 
            
            MidiNotes = e.MidiNotes ?? [],
            PitchClasses = e.PitchClasses ?? [],
            PitchClassSet = string.Join(",", e.PitchClasses ?? []), 
            IntervalClassVector = e.IntervalClassVector ?? "",
            
            AnalysisEngine = e.AnalysisEngine ?? "Unknown",
            AnalysisVersion = e.AnalysisVersion ?? "0.0",
            Jobs = e.Jobs ?? [],
            
            TuningId = e.Tuning ?? "Standard",
            PitchClassSetId = e.PrimeFormId ?? "",
             
            Embedding = e.Embedding ?? [],
            
            // Other optional props
            MinFret = e.MinFret,
            MaxFret = e.MaxFret,
            Difficulty = e.Difficulty,
            ForteCode = e.ForteCode,
            PrimeFormId = e.PrimeFormId,
            ModeName = e.ModeName,
            Position = e.HandPosition,
            HandStretch = e.HandStretch,
            BarreRequired = e.BarreRequired,
            StackingType = e.StackingType,
            RootPitchClass = e.RootPitchClass,
            MidiBassNote = e.MidiBassNote,
            HarmonicFunction = e.HarmonicFunction,
            IsNaturallyOccurring = e.IsNaturallyOccurring,
            Consonance = e.ConsonanceScore,
            Brightness = e.Brightness,
            IsRootless = e.IsRootless,
            HasGuideTones = e.HasGuideTones,
            Inversion = e.Inversion,
            
        };
    }
}
