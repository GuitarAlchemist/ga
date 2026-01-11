namespace GaCLI.Commands;

using GA.Business.Core.AI;
using GA.Business.Core.Fretboard.Primitives;
using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.Core.Fretboard.Voicings.Analysis;
using GA.Business.Core.Fretboard.Voicings.Generation; // For DecomposedVoicing
using GA.Business.Core.Fretboard.Positions; // For PositionLocation
using GA.Business.Core.Notes;
using GA.Business.Core.Intervals;
using Spectre.Console;
using System.Text.RegularExpressions;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Notes.Primitives;

using GA.Business.Core.AI.Embeddings;

public class BenchmarkSimilarityCommand
{
    private readonly IEmbeddingGenerator _generator;
    private readonly Fretboard _fretboard = Fretboard.Default;

    public BenchmarkSimilarityCommand(MusicalEmbeddingGenerator generator)
    {
        _generator = generator;
    }

    public async Task ExecuteAsync()
    {
        AnsiConsole.Write(new FigletText("Benchmark Similarity").Color(Color.Cyan1));

        var diagramA = AnsiConsole.Ask<string>("Enter first voicing ([green]x-3-2-0-1-0[/]):");
        var diagramB = AnsiConsole.Ask<string>("Enter second voicing ([green]x-3-2-0-1-0[/]):");

        try
        {
            var docA = await GenerateDocumentAsync(diagramA);
            var docB = await GenerateDocumentAsync(diagramB);

            // Generate Embeddings
            var vectorA = await _generator.GenerateEmbeddingAsync(docA);
            var vectorB = await _generator.GenerateEmbeddingAsync(docB);

            // Compute Similarity
            var similarity = CosineSimilarity(vectorA, vectorB);

            // Display Results
            AnsiConsole.WriteLine();
            var table = new Table();
            table.AddColumn("Metric");
            table.AddColumn("Value");
            table.AddRow("Diagram A", diagramA);
            table.AddRow("Chord A", docA.ChordName ?? "Unknown");
            table.AddRow("Diagram B", diagramB);
            table.AddRow("Chord B", docB.ChordName ?? "Unknown");
            table.AddRow("Cosine Similarity", $"[bold yellow]{similarity:P2}[/]");
            
            AnsiConsole.Write(table);

            // Detailed Feature Comparison
            DisplayFeatureBreakdown(vectorA, vectorB);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
        }
    }

    private void DisplayFeatureBreakdown(double[] vA, double[] vB)
    {
        AnsiConsole.MarkupLine("\n[bold]v1.1 Subspace Similarity Breakdown:[/]");
        var table = new Table().Border(TableBorder.Rounded);
        table.AddColumn("Subspace");
        table.AddColumn("Range");
        table.AddColumn("Similarity");
        table.AddColumn("Visual Profile (A vs B)");

        // 1. Identity (0-5)
        AddSubspaceRow(table, "Identity (O)", EmbeddingSchema.IdentityOffset, EmbeddingSchema.IdentityDim, vA, vB);

        // 2. Structure (T) - OPTIC/K
        AddSubspaceRow(table, "Structure (T)", EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureDim, vA, vB);

        // 3. Morphology (P) - Physical
        AddSubspaceRow(table, "Morphology (P)", EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyDim, vA, vB);

        // 4. Context (C) - Progression
        AddSubspaceRow(table, "Context (C)", EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextDim, vA, vB);

        // 5. Symbolic (K) - Knowledge
        AddSubspaceRow(table, "Symbolic (K)", EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim, vA, vB);
        
        AnsiConsole.Write(table);
    }

    private void AddSubspaceRow(Table table, string name, int start, int count, double[] vA, double[] vB)
    {
        var subVA = vA.Skip(start).Take(count).ToArray();
        var subVB = vB.Skip(start).Take(count).ToArray();
        var sim = CosineSimilarity(subVA, subVB);

        // Visual sparkline-style profile
        var profile = "";
        for (int i = 0; i < count; i++)
        {
            var charA = subVA[i] > 0.1 ? "█" : "░";
            var charB = subVB[i] > 0.1 ? "█" : "░";
            
            if (charA == "█" && charB == "█") profile += "[green]█[/]";
            else if (charA == "█") profile += "[red]◀[/]";
            else if (charB == "█") profile += "[blue]▶[/]";
            else profile += "░";
        }

        table.AddRow(
            name, 
            $"{start}-{start + count - 1}", 
            $"[bold]{sim:P1}[/]",
            profile);
    }


    private async Task<VoicingDocument> GenerateDocumentAsync(string diagram)
    {
        var voicing = ParseDiagram(_fretboard, diagram);
        
        // Lightweight analysis setup
        var decomposed = new DecomposedVoicing(voicing, null, null, null);
        var analysis = VoicingAnalyzer.AnalyzeEnhanced(decomposed);
        
        return new VoicingDocument
        {
            Id = "temp",
            Diagram = diagram,
            ChordName = analysis.ChordId.ChordName,
            MidiNotes = analysis.MidiNotes.Select(n => n.Value).ToArray(),
            PitchClasses = analysis.PitchClassSet.Select(p => p.Value).ToArray(),
            IntervalClassVector = analysis.IntervallicInfo.IntervalClassVector ?? "000000",
            RootPitchClass = analysis.ChordId.RootPitchClass?.Value,
            MidiBassNote = analysis.MidiNotes.Length > 0 ? analysis.MidiNotes[0].Value : 0,
            Consonance = analysis.PerceptualQualities.ConsonanceScore,
            Brightness = analysis.PerceptualQualities.Brightness,
            HandStretch = analysis.PlayabilityInfo.HandStretch,
            StackingType = null, // analysis.VoicingCharacteristics.StackingType,
            Inversion = 0, // analysis.ChordId.Inversion,
            
            // Required placeholders
            VoicingType = "Normal",
            MinFret = 0, MaxFret = 12, BarreRequired = false, 
            SemanticTags = [], PossibleKeys = [], Jobs = [], AnalysisEngine = "Bench", AnalysisVersion = "1.0",
            SearchableText = "", YamlAnalysis = "", TuningId = "Standard", PitchClassSetId = "", PitchClassSet = "",
            IsRootless = false, // analysis.VoicingCharacteristics.IsRootless,
            HasGuideTones = false, // analysis.VoicingCharacteristics.HasGuideTones,
            IsNaturallyOccurring = true,
            HarmonicFunction = null
        };
    }

    // Copied from IndexVoicingsCommand -> simple parsing
    private Voicing ParseDiagram(Fretboard fretboard, string diagram)
    {
        var partsLowToHigh = diagram.Split('-');
        var positions = new Position[partsLowToHigh.Length];
        var midiNotes = new List<GA.Business.Core.Notes.Primitives.MidiNote>();

        for (int i = 0; i < partsLowToHigh.Length; i++)
        {
            var part = partsLowToHigh[i];
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

    private double CosineSimilarity(double[] v1, double[] v2)
    {
        if (v1.Length != v2.Length) return 0.0;
        
        double dot = 0.0, mag1 = 0.0, mag2 = 0.0;
        for (int i = 0; i < v1.Length; i++)
        {
            dot += v1[i] * v2[i];
            mag1 += v1[i] * v1[i];
            mag2 += v2[i] * v2[i];
        }
        
        return mag1 == 0 || mag2 == 0 ? 0 : dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }
}
