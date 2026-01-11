
namespace GA.Business.Core.AI.Embeddings;

using System;
using System.Linq;
using System.Threading.Tasks;
using GA.Business.Core.Fretboard.Voicings.Search;

/// <summary>
/// Orchestrates the generation of the 96-dimensional canonical musical embedding (v1.2.1).
/// Layout: Identity (0-5), Structure (6-29), Morphology (30-53), Context (54-65), Symbolic (66-77), Extensions (78-95).
/// </summary>
public class MusicalEmbeddingGenerator : IEmbeddingGenerator
{
    private readonly IdentityVectorService _identityService;
    private readonly TheoryVectorService _theoryService;
    private readonly MorphologyVectorService _morphologyService;
    private readonly ContextVectorService _contextService;
    private readonly SymbolicVectorService _symbolicService;

    public int Dimension => EmbeddingSchema.TotalDimension;

    public MusicalEmbeddingGenerator(
        IdentityVectorService identityService,
        TheoryVectorService theoryService,
        MorphologyVectorService morphologyService,
        ContextVectorService contextService,
        SymbolicVectorService symbolicService)
    {
        _identityService = identityService;
        _theoryService = theoryService;
        _morphologyService = morphologyService;
        _contextService = contextService;
        _symbolicService = symbolicService;
    }

    public Task<double[]> GenerateEmbeddingAsync(VoicingDocument doc)
    {
        // 1. Identity Vector (O)
        var identityVector = _identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Voicing);

        // 2. Structure Vector (T) - OPTIC/K
        var structureVector = _theoryService.ComputeEmbedding(
            pitchClasses: doc.PitchClasses,
            rootPitchClass: doc.RootPitchClass,
            intervalClassVector: doc.IntervalClassVector,
            consonance: doc.Consonance,
            brightness: doc.Brightness,
            complementarity: 0.0 // Placeholder for K-equivalence
        );

        // 3. Morphology Vector (P)
        var melodyPc = doc.MidiNotes.Length > 0 ? doc.MidiNotes.Max() % 12 : (int?)null;
        var morphologyVector = _morphologyService.ComputeEmbedding(
            bassPitchClass: doc.MidiBassNote >= 0 ? doc.MidiBassNote : null,
            melodyPitchClass: melodyPc,
            normalizedSpan: doc.HandStretch / 12.0,
            normalizedNoteCount: doc.MidiNotes.Length / 6.0,
            isRootless: doc.IsRootless,
            averageFret: (doc.MinFret + doc.MaxFret) / 2.0,
            barreRequired: doc.BarreRequired
        );

        // 4. Context Vector (C)
        var contextVector = _contextService.ComputeEmbedding(
            harmonicFunction: doc.HarmonicFunction,
            stabilityDelta: 0.0,
            tension: 1.0 - doc.Consonance,
            isResolution: false
        );

        // 5. Symbolic Vector (K)
        var tags = doc.SemanticTags.ToList();
        if (!string.IsNullOrEmpty(doc.CagedShape))
        {
             // Normalize "C" -> "c-shape"
             var shapeTag = $"{doc.CagedShape.ToLowerInvariant()}-shape";
             tags.Add(shapeTag);
        }
        var symbolicVector = _symbolicService.ComputeEmbedding(tags);

        // Combine base vectors
        var combined = new double[Dimension];

        // Use Schema Offsets for safety
        Array.Copy(identityVector, 0, combined, EmbeddingSchema.IdentityOffset, identityVector.Length);
        Array.Copy(structureVector, 0, combined, EmbeddingSchema.StructureOffset, structureVector.Length);
        Array.Copy(morphologyVector, 0, combined, EmbeddingSchema.MorphologyOffset, morphologyVector.Length);
        Array.Copy(contextVector, 0, combined, EmbeddingSchema.ContextOffset, contextVector.Length);
        Array.Copy(symbolicVector, 0, combined, EmbeddingSchema.SymbolicOffset, symbolicVector.Length);

        // 6. Extensions Vector (v1.2.1)
        ComputeExtensions(combined, doc);

        return Task.FromResult(combined);
    }

    /// <summary>
    /// Computes v1.2.1 extension features (indices 78-95).
    /// </summary>
    private void ComputeExtensions(double[] embedding, VoicingDocument doc)
    {
        var midiNotes = doc.MidiNotes;
        var pitchClasses = doc.PitchClasses;
        var rootPc = doc.RootPitchClass ?? 0;
        var n = midiNotes.Length;

        if (n == 0) return; // Leave as zeros if no notes

        // Derived values
        var tension = 1.0 - doc.Consonance;
        var stability = doc.Consonance;
        var uniquePCs = pitchClasses.Distinct().Count();
        var topPc = midiNotes.Max() % 12;

        // 78: Harmonic Inertia
        embedding[EmbeddingSchema.HarmonicInertia] = Clamp01(stability * (1.0 - tension));

        // 79: Resolution Pressure
        embedding[EmbeddingSchema.ResolutionPressure] = Clamp01(0.7 * tension + 0.3 * (1.0 - stability));

        // 80: Doubling Ratio
        var doublingRatio = (double)(n - uniquePCs) / Math.Max(1, n);
        embedding[EmbeddingSchema.Textural_DoublingRatio] = Clamp01(doublingRatio);

        // 81: Root Doubled
        var rootCount = pitchClasses.Count(pc => pc == rootPc);
        embedding[EmbeddingSchema.Textural_RootDoubled] = rootCount > 1 ? 1.0 : 0.0;

        // 82: Top Note Relative
        var topRelative = ((topPc - rootPc + 12) % 12) / 11.0;
        embedding[EmbeddingSchema.Textural_TopNoteRelative] = Clamp01(topRelative);

        // Spectral Color (84-89)
        var sortedMidi = midiNotes.OrderBy(m => m).ToArray();
        var mean = sortedMidi.Average();
        var stddev = Math.Sqrt(sortedMidi.Average(m => Math.Pow(m - mean, 2)));
        var span = sortedMidi.Max() - sortedMidi.Min();

        // 84: Mean Register (Brightness)
        embedding[EmbeddingSchema.Spectral_MeanRegister] = Clamp01((mean - EmbeddingSchema.MinMidi) / (EmbeddingSchema.MaxMidi - EmbeddingSchema.MinMidi));

        // 85: Register Spread
        var registerSpread = Clamp01(stddev / EmbeddingSchema.SpreadMax);
        embedding[EmbeddingSchema.Spectral_RegisterSpread] = registerSpread;

        // 86: Low End Weight
        var lowCount = sortedMidi.Count(m => m < EmbeddingSchema.LowThreshold);
        embedding[EmbeddingSchema.Spectral_LowEndWeight] = (double)lowCount / n;

        // 87: High End Weight
        var highCount = sortedMidi.Count(m => m > EmbeddingSchema.HighThreshold);
        embedding[EmbeddingSchema.Spectral_HighEndWeight] = (double)highCount / n;

        // 88: Local Clustering
        var smallIntervals = 0;
        for (int i = 0; i < sortedMidi.Length - 1; i++)
        {
            if (sortedMidi[i + 1] - sortedMidi[i] <= 2) smallIntervals++;
        }
        embedding[EmbeddingSchema.Spectral_LocalClustering] = (double)smallIntervals / Math.Max(1, n - 1);

        // 89: Roughness Proxy (weighted by register)
        var beatRisk = 0.0;
        for (int i = 0; i < sortedMidi.Length - 1; i++)
        {
            var interval = sortedMidi[i + 1] - sortedMidi[i];
            if (interval <= 2)
            {
                var midpoint = (sortedMidi[i] + sortedMidi[i + 1]) / 2.0;
                var weight = 1.0 - Clamp01((midpoint - EmbeddingSchema.MinMidi) / (EmbeddingSchema.MaxMidi - EmbeddingSchema.MinMidi));
                beatRisk += weight;
            }
        }
        embedding[EmbeddingSchema.Spectral_RoughnessProxy] = Clamp01(beatRisk / Math.Max(1, n - 1));

        // 83: Smoothness Budget (derived)
        var localClustering = embedding[EmbeddingSchema.Spectral_LocalClustering];
        embedding[EmbeddingSchema.Relational_SmoothnessBudget] = Clamp01(0.5 * doublingRatio + 0.7 * (1 - registerSpread) - 0.3 * localClustering);

        // Extended Texture (90-95)
        // 90: Bass-Melody Span
        embedding[EmbeddingSchema.Extended_BassMelodySpan] = Clamp01(span / EmbeddingSchema.SpanMax);

        // 91: Third Doubled (check both major and minor 3rd)
        var majorThirdPc = (rootPc + 4) % 12;
        var minorThirdPc = (rootPc + 3) % 12;
        var thirdCount = pitchClasses.Count(pc => pc == majorThirdPc || pc == minorThirdPc);
        embedding[EmbeddingSchema.Extended_ThirdDoubled] = thirdCount > 1 ? 1.0 : 0.0;

        // 92: Fifth Doubled
        var fifthPc = (rootPc + 7) % 12;
        var fifthCount = pitchClasses.Count(pc => pc == fifthPc);
        embedding[EmbeddingSchema.Extended_FifthDoubled] = fifthCount > 1 ? 1.0 : 0.0;

        // 93: Open Position (span > octave)
        embedding[EmbeddingSchema.Extended_OpenPosition] = span > 12 ? 1.0 : 0.0;

        // 94: Inner Voice Density
        var innerCount = sortedMidi.Count(m => m > EmbeddingSchema.LowThreshold && m < EmbeddingSchema.HighThreshold);
        embedding[EmbeddingSchema.Extended_InnerVoiceDensity] = (double)innerCount / Math.Max(1, n);

        // 95: Omitted Root
        var hasRoot = pitchClasses.Contains(rootPc);
        embedding[EmbeddingSchema.Extended_OmittedRoot] = hasRoot ? 0.0 : 1.0;
    }

    private static double Clamp01(double value) => Math.Min(1.0, Math.Max(0.0, value));

    public Task<double[]> GenerateEmbeddingAsync(string text)
    {
        return Task.FromResult(new double[Dimension]);
    }

    public async Task<double[][]> GenerateBatchEmbeddingsAsync(IEnumerable<VoicingDocument> documents)
    {
        var docs = documents.ToList();
        var results = new double[docs.Count][];
        
        for (int i = 0; i < docs.Count; i++)
        {
            results[i] = await GenerateEmbeddingAsync(docs[i]);
        }
        
        return results;
    }
}
