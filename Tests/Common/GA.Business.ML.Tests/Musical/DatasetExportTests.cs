namespace GA.Business.ML.Tests;

using System.Collections.Concurrent;
using System.Text.Json;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using GA.Business.ML.Rag.Models;
using GA.Domain.Core.Instruments.Fretboard.Voicings.Core;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Harmony;
using GA.Domain.Services.Chords;
using GA.Domain.Services.Fretboard.Voicings.Generation;
using NUnit.Framework;

[TestFixture]
public class DatasetExportTests
{
    private MusicalEmbeddingGenerator _generator;
    private Dictionary<string, List<ChordTemplate>> _templateLookup;

    [OneTimeSetUp]
    public void Setup()
    {
        // 1. Initialize Embedding Generator
        _generator = new MusicalEmbeddingGenerator(
            new IdentityVectorService(),
            new TheoryVectorService(),
            new MorphologyVectorService(),
            new ContextVectorService(),
            new SymbolicVectorService(),
            new ModalVectorService(),
            new PhaseSphereService()
        );

        // 2. Build Chord Template Lookup (Reverse Index)
        _templateLookup = [];
        
        // Get all base templates
        var templates = ChordTemplateFactory.GenerateAllPossibleChords().ToList();
        
        // For each template, generate for all 12 roots to cover all keys
        foreach (var template in templates)
        {
            for (int root = 0; root < 12; root++)
            {
                // Calculate shifted pitch classes for this root
                var shiftedPcs = template.PitchClassSet.Notes
                    .Select(pc => (pc.Value + root) % 12)
                    .OrderBy(x => x)
                    .ToArray();
                
                // Key format: "{0,4,7}"
                var key = "{" + string.Join(",", shiftedPcs) + "}";

                if (!_templateLookup.ContainsKey(key))
                {
                    _templateLookup[key] = [];
                }
                
                _templateLookup[key].Add(template); 
            }
        }
    }

    [Test]
    [Explicit("Long running dataset export - generates full fretboard analysis")]
    public async Task Export_All_Chord_Embeddings()
    {
        var fretboard = Fretboard.Default; // Standard tuning
        var exportItems = new ConcurrentBag<DatasetItem>();
        
        TestContext.WriteLine("Starting Voicing Generation...");
        
        // 1. Generate All Voicings (Physical Shapes)
        // Window size 4 covers most playable chords. Min notes 3 to filter trivial intervals.
        // Synchronous call to avoid async stream dependencies in test runner
        var voicings = VoicingGenerator.GenerateAllVoicings(fretboard, windowSize: 4, minPlayedNotes: 3);

        TestContext.WriteLine($"Generated {voicings.Count} physical voicings. Processing embeddings...");

        // 2. Process and Enrich in Parallel
        var processedCount = 0;
        
        // Process in parallel to speed up embedding generation
        await Parallel.ForEachAsync(voicings, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (voicing, _) =>
        {
            var notes = voicing.Notes;
            var pcsValues = notes.Select(n => n.Value % 12).Distinct().OrderBy(p => p).ToArray();
            var key = "{" + string.Join(",", pcsValues) + "}";

            // Check if this shape corresponds to a known chord
            if (_templateLookup.TryGetValue(key, out var templates))
            {
                foreach (var template in templates)
                {
                    // Deduce Root: 
                    // Assume first note in sorted PC matches first note in Template PC (relative 0).
                    // This is a heuristic but works for standard stacking.
                    var rootOffset = (pcsValues[0] - template.PitchClassSet.Notes.First().Value + 12) % 12;
                    var rootNote = PitchClass.FromValue(rootOffset).ToString();
                    var chordName = $"{rootNote} {template.Name}";

                    var doc = new ChordVoicingRagDocument
                    {
                        Id = Guid.NewGuid().ToString(),
                        SearchableText = chordName,
                        ChordName = chordName,
                        RootPitchClass = rootOffset,
                        MidiNotes = [.. notes.Select(n => n.Value)],
                        PitchClasses = pcsValues,
                        PitchClassSet = key,
                        IntervalClassVector = template.Formula.Intervals.ToString(), // Approximate
                        SemanticTags = [template.Quality.ToString(), template.Extension.ToString(), "generated-dataset"],
                        
                        // Physical props
                        MinFret = voicing.InferredMinFret(),
                        MaxFret = voicing.InferredMaxFret(),
                        HandStretch = voicing.InferredMaxFret() - voicing.InferredMinFret(),
                        BarreRequired = false, 
                        
                        // Required placeholders
                        PossibleKeys = [], 
                        YamlAnalysis = "{}",
                        Diagram = string.Join("-", voicing.Positions.Select(p => p.ToString())),
                        AnalysisEngine = "DatasetExport",
                        AnalysisVersion = "1.0",
                        Jobs = [],
                        TuningId = "Standard",
                        PitchClassSetId = key
                    };

                    // Generate Embedding (The core goal)
                    var embedding = await _generator.GenerateEmbeddingAsync(doc);
                    
                    exportItems.Add(new DatasetItem(chordName, key, embedding));
                }
            }
            
            var count = Interlocked.Increment(ref processedCount);
            if (count % 5000 == 0) TestContext.WriteLine($"Processed {count} voicings...");
        });
        
        // 3. Write to File
        var outputPath = Path.Combine(TestContext.CurrentContext.WorkDirectory, "optic_k_dataset.json");
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(exportItems.ToList(), options);
        await File.WriteAllTextAsync(outputPath, json);
        
        TestContext.WriteLine($"Export complete. {exportItems.Count} embeddings generated.");
        TestContext.WriteLine($"File saved to: {outputPath}");
    }
}

// Helper Extensions and Records
public static class VoicingHelpers 
{
    public static int InferredMinFret(this Voicing v) => 
        v.Positions.OfType<Position.Played>().Any() ? v.Positions.OfType<Position.Played>().Min(p => p.Location.Fret.Value) : 0;
        
    public static int InferredMaxFret(this Voicing v) => 
        v.Positions.OfType<Position.Played>().Any() ? v.Positions.OfType<Position.Played>().Max(p => p.Location.Fret.Value) : 0;
}

public record DatasetItem(string Name, string PitchClassSet, float[] Vector);
