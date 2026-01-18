using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using GaChatbot.Abstractions;
using GaChatbot.Services;
using GaChatbot.Models;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Musical.Explanation;
using GA.Business.ML.Musical.Enrichment;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Embeddings;
using GA.Data.MongoDB.Services;
using GA.Data.MongoDB.Configuration;
using GA.Business.ML.Extensions;

// Parse command-line args
var mongoLimit = 0;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--mongo-limit" && i + 1 < args.Length)
    {
        int.TryParse(args[i + 1], out mongoLimit);
    }
}

var builder = Host.CreateApplicationBuilder(args);

// Register Services
// Register Services
// const string indexPath = "voicing_index.jsonl";
// builder.Services.AddSingleton(new FileBasedVectorIndex(indexPath)); 
// SPIKE: Use Qdrant
builder.Services.AddSingleton<IVectorIndex>(sp => new QdrantVectorIndex("localhost", 6334));

builder.Services.AddSingleton<PhaseSphereService>(); 
builder.Services.AddSingleton<AutoTaggingService>();

// MongoDB (for seeding)
builder.Services.Configure<MongoDbSettings>(options =>
{
    options.ConnectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") 
        ?? "mongodb://localhost:27017";
    options.DatabaseName = "guitaralchemist";
});
builder.Services.AddSingleton<MongoDbService>();
builder.Services.AddSingleton<GA.Business.Core.Tabs.IProgressionCorpusRepository, GA.Data.MongoDB.Services.MongoProgressionCorpusRepository>();

// Register all AI/ML services (OPTIC-K, Tabs, etc.)
builder.Services.AddGuitarAlchemistAI();

// Modal Flavor Service (Phase 13)
builder.Services.AddSingleton<ModalFlavorService>();

// Spectral Retrieval (Phase 15)
builder.Services.AddScoped<SpectralRetrievalService>();

// ---- Anti-Hallucination Guardrails (Phase 5.2.5 Spike) ----
builder.Services.AddSingleton<GroundedPromptBuilder>();
builder.Services.AddSingleton<ResponseValidator>();
builder.Services.AddSingleton<IGroundedNarrator, OllamaGroundedNarrator>();

// ---- Orchestration ----
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Tuning>(GA.Business.Core.Fretboard.Tuning.Default);
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Analysis.FretboardPositionMapper>();
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Analysis.IMlNaturalnessRanker, GA.Business.ML.Naturalness.MlNaturalnessRanker>();
builder.Services.AddSingleton<GA.Business.Core.Fretboard.Analysis.PhysicalCostService>();
builder.Services.AddSingleton<GA.Business.Core.Abstractions.IEmbeddingGenerator, GA.Business.ML.Embeddings.MusicalEmbeddingGenerator>();
// builder.Services.AddSingleton<IVectorIndex>(sp => sp.GetRequiredService<FileBasedVectorIndex>());
builder.Services.AddSingleton<GA.Business.ML.Retrieval.StyleProfileService>();
builder.Services.AddSingleton<GA.Business.ML.Retrieval.NextChordSuggestionService>();
builder.Services.AddSingleton<GA.Business.ML.Retrieval.ModulationAnalyzer>();
builder.Services.AddSingleton<GA.Business.ML.Tabs.AdvancedTabSolver>();
builder.Services.AddSingleton<GA.Business.ML.Tabs.AlternativeFingeringService>();
builder.Services.AddSingleton<GaChatbot.Services.SpectralRagOrchestrator>();
builder.Services.AddSingleton<GaChatbot.Services.TabPresentationService>();
builder.Services.AddSingleton<GaChatbot.Services.TabAwareOrchestrator>();
builder.Services.AddSingleton<GaChatbot.Services.ProductionOrchestrator>();

var app = builder.Build();

// Run the interactive loop
Console.WriteLine("=== Spectral RAG Chatbot (MVP Phase 15 - Qdrant Backend) ===");

// Get DI-resolved services
var index = app.Services.GetRequiredService<IVectorIndex>(); // Qdrant
var generator = app.Services.GetRequiredService<MusicalEmbeddingGenerator>();
var autoTagger = app.Services.GetRequiredService<AutoTaggingService>();
var mongoDb = app.Services.GetRequiredService<MongoDbService>();

// SKIP SEEDING - Assumes Qdrant (or Mongo) is already populated via GaCLI
Console.WriteLine($"Qdrant Index Loaded. {index.Documents.Count} documents in cache.");

var orchestrator = app.Services.GetRequiredService<GaChatbot.Services.ProductionOrchestrator>();

while (true)
{
    Console.WriteLine("Type a query (or 'exit' to quit):");
    Console.Write("> ");
    var input = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(input)) continue;
    if (input.Equals("exit", StringComparison.OrdinalIgnoreCase)) break;

    var response = await orchestrator.AnswerAsync(new ChatRequest(input));
    
    Console.WriteLine("\n[Assistant]:");
    Console.WriteLine(response.NaturalLanguageAnswer);
    
    Console.WriteLine("\n[Candidates]:");
    foreach (var c in response.Candidates)
    {
        Console.WriteLine($"  - {c.DisplayName} ({c.Shape}) [Score: {c.Score:F2}]");
        Console.WriteLine($"    Why: {c.ExplanationText}");
    }
    Console.WriteLine();
}

static async Task SeedVoicingsAsync(FileBasedVectorIndex index, MusicalEmbeddingGenerator generator, GA.Business.ML.Musical.Enrichment.AutoTaggingService tagger) 
{
    Console.WriteLine("Seeding index...");
    var rawVoicings = new[] 
    {
        ("C Major Open", 0, "maj", new[] {0,4,7}, new[] {0, 4, 7, 0, 4}, "x-3-2-0-1-0"),
        ("C Major Shell", 0, "maj7", new[] {0,4,11}, new[] {0, 11, 4}, "x-3-2-4-x-x"),
        ("Dm7 Open", 2, "m7", new[] {2,5,9,0}, new[] {2, 9, 0, 5}, "x-x-0-2-1-1"),
        ("Dm7 Shell 5th", 2, "m7", new[] {2,5,0}, new[] {2, 0, 5}, "x-5-7-5-x-x"),
        ("Cmaj7#11", 0, "maj7#11", new[] {0, 2, 4, 6, 7, 11}, new[] {0, 2, 4, 6, 7, 11}, "x-3-2-0-0-2"), // C E B F# G -> Contains #4 (F#) for Lydian
        ("Dm6", 2, "m6", new[] {2, 5, 9, 11}, new[] {2, 5, 9, 11}, "x-x-0-2-0-1"), // D A B F -> D(Root) F(b3) A(5) B(6) -> Dorian Color
        ("G7 Open", 7, "7", new[] {7,11,2,5}, new[] {7, 11, 2, 5, 7, 7}, "3-2-0-0-0-1"),
        ("G13 Jazz", 7, "13", new[] {7,5,11,4}, new[] {7, 5, 11, 4}, "3-x-3-4-5-x")
    };

    foreach(var (name, rootPc, quality, pcs, notes, shape) in rawVoicings)
    {
       // Use Core Analyzer for robust identification (Root, Name, Function)
       var pcsList = pcs.Select(pc => GA.Business.Core.Atonal.PitchClass.FromValue(pc)).ToList();
       var pcsSet = new GA.Business.Core.Atonal.PitchClassSet(pcsList);
       var bassPc = GA.Business.Core.Atonal.PitchClass.FromValue(notes[0] % 12);
       var analysis = GA.Business.Core.Fretboard.Voicings.Analysis.VoicingHarmonicAnalyzer.IdentifyChord(pcsSet, pcsList, bassPc);

       // Calculate Forte Code
       string? forteCode = GA.Business.Core.Atonal.ForteCatalog.TryGetForteNumber(pcsSet.PrimeForm, out var forte) 
           ? forte.ToString() : null;

       var doc = new VoicingDocument 
       {
           Id = Guid.NewGuid().ToString(),
           ChordName = analysis.ChordName ?? name,
           RootPitchClass = analysis.RootPitchClass?.Value ?? rootPc,
           PitchClasses = pcs,
           MidiNotes = notes,
           Diagram = shape,
           
           // Richer Metadata from Analyzer
           HarmonicFunction = analysis.HarmonicFunction,
           IsNaturallyOccurring = analysis.IsNaturallyOccurring,
           HasGuideTones = true, // Simplified for seed data
           OmittedTones = [],

           // Defaults
           SearchableText = $"{name} {quality}",
           PossibleKeys = analysis.ClosestKey != null ? new[] { analysis.ClosestKey.ToString() } : Array.Empty<string>(),
           SemanticTags = Array.Empty<string>(),
           YamlAnalysis = "{}",
           PitchClassSet = string.Join(",", pcs),
           IntervalClassVector = "000000",
           AnalysisEngine = "VoicingHarmonicAnalyzer",
           AnalysisVersion = "1.1",
           Jobs = Array.Empty<string>(),
           TuningId = "Standard",
           PitchClassSetId = pcsSet.Id.ToString(),
           ForteCode = forteCode,
           Embedding = null 
       };

       // Phase 12: Auto-Enrich Tags
       var generatedTags = tagger.GenerateTags(doc);
       var enrichedDoc = doc with { SemanticTags = generatedTags };

       // Generate Embedding (creates new double[])
       var embedding = await generator.GenerateEmbeddingAsync(enrichedDoc);
       
       // Clone document with embedding set
       var finalDoc = enrichedDoc with { Embedding = embedding };

       index.Add(finalDoc);
    }
    Console.WriteLine($"Index seeded with {rawVoicings.Length} items.");
}

/// <summary>
/// Seeds the index from MongoDB voicings collection.
/// </summary>
static async Task SeedFromMongoDbAsync(
    FileBasedVectorIndex index, 
    MusicalEmbeddingGenerator generator, 
    AutoTaggingService tagger,
    MongoDbService mongoDb,
    int limit)
{
    Console.WriteLine($"Querying MongoDB for up to {limit} voicings...");
    
    var filter = MongoDB.Driver.Builders<GA.Data.MongoDB.Models.VoicingEntity>.Filter.Empty;
    var entities = await mongoDb.Voicings.Find(filter).Limit(limit).ToListAsync();
    
    Console.WriteLine($"Found {entities.Count} voicings in MongoDB.");
    
    int count = 0;
    foreach (var entity in entities)
    {
        // Convert VoicingEntity to VoicingDocument
        var doc = new VoicingDocument
        {
            Id = entity.Id ?? Guid.NewGuid().ToString(),
            ChordName = entity.ChordName ?? "Unknown",
            RootPitchClass = entity.RootPitchClass ?? 0,
            PitchClasses = entity.PitchClasses ?? [],
            MidiNotes = entity.MidiNotes ?? [],
            Diagram = entity.Diagram,
            SearchableText = entity.SearchableText ?? entity.ChordName ?? "",
            PossibleKeys = entity.PossibleKeys ?? [],
            SemanticTags = entity.SemanticTags ?? [],
            YamlAnalysis = entity.FullAnalysis ?? "{}",
            PitchClassSet = entity.PitchClasses != null ? string.Join(",", entity.PitchClasses) : "",
            IntervalClassVector = entity.IntervalClassVector ?? "000000",
            AnalysisEngine = entity.AnalysisEngine ?? "MongoDB",
            AnalysisVersion = entity.AnalysisVersion ?? "1.0",
            Jobs = entity.Jobs ?? [],
            TuningId = entity.Tuning ?? "Standard",
            PitchClassSetId = entity.PrimeFormId ?? "0",
            Embedding = entity.Embedding
        };
        
        // Auto-Enrich Tags if not already present
        if (doc.SemanticTags.Length == 0)
        {
            var generatedTags = tagger.GenerateTags(doc);
            doc = doc with { SemanticTags = generatedTags };
        }

        // Ensure Forte Code is populated (Atonal Strategy)
        if (string.IsNullOrEmpty(doc.ForteCode) && doc.PitchClasses.Length > 0)
        {
            var pcsList = doc.PitchClasses.Select(pc => GA.Business.Core.Atonal.PitchClass.FromValue(pc));
            var pcs = new GA.Business.Core.Atonal.PitchClassSet(pcsList);
            
            if (GA.Business.Core.Atonal.ForteCatalog.TryGetForteNumber(pcs.PrimeForm, out var forte))
            {
                doc = doc with { ForteCode = forte.ToString() };
            }
        }
        
        // Generate Embedding if not already present
        if (doc.Embedding == null || doc.Embedding.Length == 0)
        {
            var embedding = await generator.GenerateEmbeddingAsync(doc);
            doc = doc with { Embedding = embedding };
        }
        
        index.Add(doc);
        count++;
        
        if (count % 100 == 0)
        {
            Console.WriteLine($"  Processed {count} voicings...");
        }
    }
    
    Console.WriteLine($"Imported {count} voicings from MongoDB.");
}
