namespace GA.Business.Core.Tests.Embeddings;

using GA.Business.Core.AI.Embeddings;
using GA.Business.Core.Fretboard.Voicings.Core;
using GA.Business.Core.Fretboard.Voicings.Search;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

[TestFixture]
public class AdvancedEmbeddingScenarios
{
    private MusicalEmbeddingGenerator _generator;

    [SetUp]
    public void SetUp()
    {
        _generator = new MusicalEmbeddingGenerator(
            new IdentityVectorService(),
            new TheoryVectorService(),
            new MorphologyVectorService(),
            new ContextVectorService(),
            new SymbolicVectorService()
        );
    }

    [Test]
    public async Task TonalShift_CMajor_vs_GMajor_StructuralSimilarity()
    {
        // C Major (x32010)
        var docC = CreateDummyDocument("x32010", "C", [0, 4, 7], "001110");
        // G Major (320003)
        var docG = CreateDummyDocument("320003", "G", [7, 11, 2], "001110");

        var vC = await _generator.GenerateEmbeddingAsync(docC);
        var vG = await _generator.GenerateEmbeddingAsync(docG);

        var structureC = GetSubspace(vC, EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureDim);
        var structureG = GetSubspace(vG, EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureDim);

        var sim = CosineSimilarity(structureC, structureG);

        // They should have VERY high structural similarity because they share:
        // 1. Cardinality
        // 2. ICV
        // 3. Functional properties (Triad consonance/brightness)
        // Note: Pitch Class Chroma differs, but ICV remains constant.
        Assert.That(sim, Is.GreaterThan(0.6), "Tonal shifts of the same chord type should be structurally similar.");
    }

    [Test]
    public async Task SymbolicImpact_C7_with_HendrixTag()
    {
        var docNormal = CreateDummyDocument("x323xx", "C7", [0, 4, 10], "000100");
        var docHendrix = CreateDummyDocument("x323xx", "C7", [0, 4, 10], "000100", tags: ["hendrix"]);

        var vNormal = await _generator.GenerateEmbeddingAsync(docNormal);
        var vHendrix = await _generator.GenerateEmbeddingAsync(docHendrix);

        var symbolicNormal = GetSubspace(vNormal, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim);
        var symbolicHendrix = GetSubspace(vHendrix, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim);

        var sim = CosineSimilarity(symbolicNormal, symbolicHendrix);

        Assert.That(sim, Is.EqualTo(0.0), "Untagged vs Tagged should have zero symbolic similarity if no tags match.");
        Assert.That(symbolicHendrix.Any(v => v > 0.1), Is.True, "Symbolic vector should have energy when tags are present.");
    }

    [Test]
    public async Task SymbolicImpact_JamesBondChord_Tag()
    {
        var docNormal = CreateDummyDocument("x21000", "Em(maj7)", [4, 7, 11, 3], "100111");
        var docBond = CreateDummyDocument("x21000", "Em(maj7)", [4, 7, 11, 3], "100111", tags: ["james-bond-chord"]);

        var vNormal = await _generator.GenerateEmbeddingAsync(docNormal);
        var vBond = await _generator.GenerateEmbeddingAsync(docBond);

        var symbolicNormal = GetSubspace(vNormal, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim);
        var symbolicBond = GetSubspace(vBond, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim);

        Assert.That(symbolicBond[11], Is.EqualTo(1.0), "James Bond chord tag should set the 'Iconic' bit (Index 11).");
        Assert.That(symbolicNormal[11], Is.EqualTo(0.0));
    }

    [Test]
    public async Task ContextualShift_SameChord_DifferentFunction()
    {
        var docTonic = CreateDummyDocument("x32010", "C", [0, 4, 7], "001110", harmonicFunction: "Tonic");
        var docDominant = CreateDummyDocument("x32010", "C", [0, 4, 7], "001110", harmonicFunction: "Dominant");

        var vTonic = await _generator.GenerateEmbeddingAsync(docTonic);
        var vDominant = await _generator.GenerateEmbeddingAsync(docDominant);

        var contextTonic = GetSubspace(vTonic, EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextDim);
        var contextDominant = GetSubspace(vDominant, EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextDim);

        var sim = CosineSimilarity(contextTonic, contextDominant);

        Assert.That(sim, Is.LessThan(0.1), "Opposite harmonic functions should have low contextual similarity.");
    }

    [Test]
    public async Task MorphologyShift_Open_vs_Barre()
    {
        // Open C: x32010
        // Barre C: x35553
        var docOpen = CreateDummyDocument("x32010", "C", [0, 4, 7], "001110", minFret: 0, maxFret: 3, stretch: 3);
        var docBarre = CreateDummyDocument("x35553", "C", [0, 4, 7], "001110", minFret: 3, maxFret: 5, stretch: 2, barre: true);

        var vOpen = await _generator.GenerateEmbeddingAsync(docOpen);
        var vBarre = await _generator.GenerateEmbeddingAsync(docBarre);

        var morphOpen = GetSubspace(vOpen, EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyDim);
        var morphBarre = GetSubspace(vBarre, EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyDim);

        var sim = CosineSimilarity(morphOpen, morphBarre);

        // Morphology should reflect the difference in fret position and barre requirement
        Assert.That(sim, Is.LessThan(0.8), "Different physical forms of the same chord should have lower morphology similarity.");
    }

    private VoicingDocument CreateDummyDocument(
        string diagram, 
        string chordName, 
        int[] pcs, 
        string icv, 
        string[]? tags = null,
        string? harmonicFunction = null,
        int minFret = 0,
        int maxFret = 12,
        int stretch = 0,
        bool barre = false)
    {
        return new VoicingDocument
        {
            Id = "test",
            Diagram = diagram,
            ChordName = chordName,
            PitchClasses = pcs,
            IntervalClassVector = icv,
            Consonance = 0.8,
            Brightness = 0.6,
            VoicingType = "Normal",
            PossibleKeys = [], 
            SemanticTags = tags ?? [], 
            Jobs = [], 
            AnalysisEngine = "Test", 
            AnalysisVersion = "1.0",
            SearchableText = "", 
            YamlAnalysis = "", 
            TuningId = "Standard", 
            PitchClassSetId = "", 
            PitchClassSet = "",
            MidiNotes = pcs.Select(p => 60 + p).ToArray(),
            HarmonicFunction = harmonicFunction,
            MinFret = minFret,
            MaxFret = maxFret,
            HandStretch = stretch,
            BarreRequired = barre
        };
    }

    private double[] GetSubspace(double[] full, int offset, int dim)
    {
        var slice = new double[dim];
        Array.Copy(full, offset, slice, 0, dim);
        return slice;
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
        return (mag1 == 0 || mag2 == 0) ? 0.0 : dot / (Math.Sqrt(mag1) * Math.Sqrt(mag2));
    }

    [Test]
    public async Task SymbolicImpact_MultiSourceYAML_Tags()
    {
        // "sweep-picking" -> GuitarTechniques.yaml (Bit 3)
        // "smooth-voice-leading" -> VoiceLeading.yaml (Bit 9)
        var doc = CreateDummyDocument("x32010", "C", [0, 4, 7], "100111", tags: ["sweep-picking", "smooth-voice-leading"]);

        var v = await _generator.GenerateEmbeddingAsync(doc);
        var symbolic = GetSubspace(v, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicDim);

        Assert.Multiple(() =>
        {
            Assert.That(symbolic[3], Is.EqualTo(1.0), "Sweep picking should set Bit 3.");
            Assert.That(symbolic[9], Is.EqualTo(1.0), "Smooth voice leading should set Bit 9.");
        });
    }
}
