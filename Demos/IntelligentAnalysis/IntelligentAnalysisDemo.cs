using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using GA.Business.ML.Tabs;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Retrieval;
using GA.Business.ML.Wavelets;
using GA.Business.ML.Extensions;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Notes;
using GA.Business.Core.Atonal;

public static class IntelligentAnalysisDemo
{
    public static async Task Main()
    {
        Console.WriteLine("=== Guitar Alchemist: Intelligent Analysis Demo ===");
        
        // 1. Setup DI and Services
        var services = new ServiceCollection();
        services.AddGuitarAlchemistAI();
        services.AddSingleton(new FileBasedVectorIndex("voicing_index.jsonl"));
        services.AddSingleton<GA.Business.ML.Musical.Enrichment.AutoTaggingService>();
        services.AddSingleton<GA.Business.ML.Musical.Enrichment.ModalFlavorService>();
        services.AddSingleton<SpectralRetrievalService>(); 
        services.AddSingleton<GA.Business.Core.Fretboard.Analysis.PhysicalCostService>();
        services.AddSingleton<PhaseSphereService>(); // Ensure Phase Sphere is registered
        var provider = services.BuildServiceProvider();

        var tabAnalyzer = provider.GetRequiredService<TabAnalysisService>();
        var styleClassifier = provider.GetRequiredService<StyleClassifierService>();
        var suggestionService = provider.GetRequiredService<NextChordSuggestionService>();
        var modulationAnalyzer = provider.GetRequiredService<ModulationAnalyzer>();
        var index = provider.GetRequiredService<FileBasedVectorIndex>();

        // 2. Define a "Real" Input: Jazz ii-V-I in C Major
        // Perfectly aligned columns for vertical parsing
        var asciiTab = @"
e|--5---3---3---3--|
B|--6---3---5---5--|
G|--5---4---4---4--|
D|--7---3---5---5--|
A|--5---5---3---3--|
E|------3----------|
";
        Console.WriteLine("\n[Input Tab]:");
        Console.WriteLine(asciiTab);

        // 3. Run Analysis
        Console.WriteLine("\n[1. Processing Pipeline...]");
        var result = await tabAnalyzer.AnalyzeAsync(asciiTab);
        var generator = provider.GetRequiredService<MusicalEmbeddingGenerator>();

        // We must ensure the documents have embeddings for similarity to work
        var events = result.Events.ToList();
        for (int i = 0; i < events.Count; i++)
        {
            var doc = events[i].Document;
            events[i] = events[i] with { Document = doc with { Embedding = await generator.GenerateEmbeddingAsync(doc) } };
        }

        var progression = events.Select(e => e.Document).ToList();

        Console.WriteLine($"Found {progression.Count} harmonic events.");
        foreach (var e in result.Events)
        {
            Console.WriteLine($"  - Event {e.TimestampIndex}: {e.Document.ChordName} (Fret {e.Document.MinFret})");
        }

        // 4. Style Classification
        Console.WriteLine("\n[2. Style Intelligence]:");
        var style = styleClassifier.PredictStyle(progression);
        Console.WriteLine($"Predicted Style: {style.PredictedStyle} ({style.Confidence * 100:F0}% confidence)");

        // 5. Key Drift / Modulation
        Console.WriteLine("\n[3. Harmonic Gravity]:");
        var targets = modulationAnalyzer.IdentifyTargets(progression);
        foreach (var t in targets.Take(2))
        {
            Console.WriteLine($"Potential Key Center: {t.Key.ToSharpNote()} ({t.Confidence * 100:F0}% confidence)");
        }

        // 6. Generative Suggestions
        Console.WriteLine("\n[4. Where to go next?]:");
        // Seed index with several targets
        var targetChords = new[] {
            ("Fmaj7", new[] { 53, 57, 60, 64 }, new[] { 5, 9, 0, 4 }, "x-x-3-2-1-0"),
            ("A Minor", new[] { 45, 48, 52 }, new[] { 9, 0, 4 }, "x-0-2-2-1-0"),
            ("G Major", new[] { 43, 47, 50 }, new[] { 7, 11, 2 }, "3-2-0-0-0-3")
        };

        foreach (var (name, midi, pcs, diagram) in targetChords)
        {
            var doc = new VoicingDocument {
                Id = Guid.NewGuid().ToString(), ChordName = name, MidiNotes = midi, PitchClasses = pcs,
                SemanticTags = ["jazz"], PossibleKeys = [], YamlAnalysis = "{}", PitchClassSet = string.Join(",", pcs), IntervalClassVector = "000000", AnalysisEngine = "Demo", AnalysisVersion = "1.0", Jobs = [], TuningId = "Standard", PitchClassSetId = "0", Diagram = diagram, SearchableText = ""
            };
            doc = doc with { Embedding = await provider.GetRequiredService<MusicalEmbeddingGenerator>().GenerateEmbeddingAsync(doc) };
            index.Add(doc);
        }

        var lastChord = progression.Last();
        var suggestions = await suggestionService.SuggestNextAsync(lastChord, topK: 3);
        foreach (var s in suggestions)
        {
            Console.WriteLine($"  -> Try **{s.Doc.ChordName}** (Harmonic Similarity: {s.HarmonicScore:F2}, Physical Transition Cost: {s.PhysicalCost:F2})");
        }

        Console.WriteLine("\n=== Demo Complete ===");
    }
}
