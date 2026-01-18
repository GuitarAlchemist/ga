namespace GA.Business.ML.Embeddings;

using System;
using System.Linq;
using System.Threading.Tasks;
using Services;
using Core.Tonal.Hierarchies; // ComplexityCalculator
using GA.Business.Core.Abstractions;

/// <summary>
/// Orchestrates the generation of the 109-dimensional canonical musical embedding (v1.3.1).
/// Implements OPTIC-K Schema v1.3.1.
///
/// <para>
/// This generator produces embeddings according to the OPTIC-K v1.3.1 schema, which encodes:
/// </para>
///
/// <list type="bullet">
///   <item><description><b>IDENTITY (0-5)</b>: Object type classification</description></item>
///   <item><description><b>STRUCTURE (6-29)</b>: OPTIC/K pitch-class set invariants</description></item>
///   <item><description><b>MORPHOLOGY (30-53)</b>: Physical/fretboard realization</description></item>
///   <item><description><b>CONTEXT (54-65)</b>: Harmonic function and temporal motion</description></item>
///   <item><description><b>SYMBOLIC (66-77)</b>: Technique and style tags</description></item>
///   <item><description><b>EXTENSIONS (78-95)</b>: Derived psychoacoustic features</description></item>
///   <item><description><b>SPECTRAL (96-108)</b>: Spectral geometry features (DFT-based)</description></item>
/// </list>
///
/// <para>
/// The EXTENSIONS partition (78-95) is computed by <see cref="ComputeExtensions"/> and includes:
/// </para>
/// <list type="bullet">
///   <item><description>Context Dynamics (78-79): Harmonic inertia and resolution pressure</description></item>
///   <item><description>Textural Features (80-82): Pitch doubling characteristics</description></item>
///   <item><description>Spectral Color (84-89): Psychoacoustic texture descriptors</description></item>
///   <item><description>Extended Texture (90-95): Voicing-specific characteristics</description></item>
/// </list>
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MusicalEmbeddingGenerator"/> class.
/// </remarks>
public class MusicalEmbeddingGenerator(
    IdentityVectorService identityService,
    TheoryVectorService theoryService,
    MorphologyVectorService morphologyService,
    ContextVectorService contextService,
    SymbolicVectorService symbolicService,
    PhaseSphereService phaseSphereService) : IEmbeddingGenerator
{
    /// <summary>Returns the total embedding dimension (216 for v1.4/v1.5).</summary>
    public int Dimension => EmbeddingSchema.TotalDimension;

    /// <summary>
    /// Generates a high-dimensional embedding for a voicing document.
    /// </summary>
    /// <param name="doc">The voicing document containing pitch data and metadata.</param>
    /// <returns>A double array representing the embedding vector.</returns>
    public async Task<double[]> GenerateEmbeddingAsync(VoicingDocument doc)
    {
        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 1: IDENTITY (0-5)
        // ═══════════════════════════════════════════════════════════════════════
        var identityVector = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Voicing);

        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 2: STRUCTURE (6-29)
        // ═══════════════════════════════════════════════════════════════════════
        var structureVector = theoryService.ComputeEmbedding(
            pitchClasses: doc.PitchClasses,
            rootPitchClass: doc.RootPitchClass,
            intervalClassVector: doc.IntervalClassVector,
            consonance: doc.Consonance,
            brightness: doc.Brightness,
            complementarity: 0.0
        );

        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 3: MORPHOLOGY (30-53)
        // ═══════════════════════════════════════════════════════════════════════
        var melodyPc = doc.MidiNotes.Length > 0 ? doc.MidiNotes.Max() % EmbeddingSchema.PitchClassCount : (int?)null;
        var morphologyVector = morphologyService.ComputeEmbedding(
            bassPitchClass: doc.MidiBassNote >= 0 ? doc.MidiBassNote : null,
            melodyPitchClass: melodyPc,
            normalizedSpan: doc.HandStretch / (double)EmbeddingSchema.PitchClassCount,
            normalizedNoteCount: doc.MidiNotes.Length / 6.0,
            isRootless: doc.IsRootless,
            averageFret: (doc.MinFret + doc.MaxFret) / 2.0,
            barreRequired: doc.BarreRequired
        );

        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 4: CONTEXT (54-65)
        // ═══════════════════════════════════════════════════════════════════════
        var contextVector = contextService.ComputeEmbedding(
            harmonicFunction: doc.HarmonicFunction,
            stabilityDelta: 0.0,
            tension: 1.0 - doc.Consonance,
            isResolution: false
        );

        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 5: SYMBOLIC (66-77)
        // ═══════════════════════════════════════════════════════════════════════
        var tags = doc.SemanticTags.ToList();
        if (!string.IsNullOrEmpty(doc.CagedShape))
        {
            var shapeTag = $"{doc.CagedShape.ToLowerInvariant()}-shape";
            tags.Add(shapeTag);
        }
        var symbolicVector = symbolicService.ComputeEmbedding(tags);

        // ═══════════════════════════════════════════════════════════════════════
        // COMBINE BASE PARTITIONS
        // ═══════════════════════════════════════════════════════════════════════
        var combined = new double[Dimension];

        Array.Copy(identityVector, 0, combined, EmbeddingSchema.IdentityOffset, identityVector.Length);
        Array.Copy(structureVector, 0, combined, EmbeddingSchema.StructureOffset, structureVector.Length);
        Array.Copy(morphologyVector, 0, combined, EmbeddingSchema.MorphologyOffset, morphologyVector.Length);
        Array.Copy(contextVector, 0, combined, EmbeddingSchema.ContextOffset, contextVector.Length);
        Array.Copy(symbolicVector, 0, combined, EmbeddingSchema.SymbolicOffset, symbolicVector.Length);

        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 6: EXTENSIONS (78-95)
        // ═══════════════════════════════════════════════════════════════════════
        ComputeExtensions(combined, doc);

        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 7: SPECTRAL GEOMETRY (96-108) — Phase Sphere Integration
        // ═══════════════════════════════════════════════════════════════════════
        PopulateSpectralPartition(combined, doc);

        // ═══════════════════════════════════════════════════════════════════════
        // PARTITION 9: HIERARCHY (128-135) — Complexity
        // ═══════════════════════════════════════════════════════════════════════
        var pcsSet = new Core.Atonal.PitchClassSet(doc.PitchClasses.Select(Core.Atonal.PitchClass.FromValue));
        combined[EmbeddingSchema.HierarchyComplexityScore] = ComplexityCalculator.CalculateScore(pcsSet);

        return combined;
    }

    private void PopulateSpectralPartition(double[] combined, VoicingDocument doc)
    {
        if (doc.PitchClasses.Length == 0) return;

        // 1. Compute weighted spectral vector using PhaseSphereService
        var spec = phaseSphereService.ComputeWeightedSpectralVector(doc.MidiNotes);
        var normalized = phaseSphereService.NormalizeToSphere(spec);

        // 2. Map to embedding slots
        var magnitudeIndices = new[] {
            EmbeddingSchema.FourierMagK1, EmbeddingSchema.FourierMagK2, EmbeddingSchema.FourierMagK3,
            EmbeddingSchema.FourierMagK4, EmbeddingSchema.FourierMagK5, EmbeddingSchema.FourierMagK6
        };
        var phaseIndices = new[] {
            EmbeddingSchema.FourierPhaseK1, EmbeddingSchema.FourierPhaseK2, EmbeddingSchema.FourierPhaseK3,
            EmbeddingSchema.FourierPhaseK4, EmbeddingSchema.FourierPhaseK5, EmbeddingSchema.FourierPhaseK6
        };

        for (int k = 0; k < 6; k++)
        {
            combined[magnitudeIndices[k]] = Clamp01(normalized[k].Magnitude);
            // Normalize phase [-pi, pi] to [0, 1]
            combined[phaseIndices[k]] = Clamp01((normalized[k].Phase + Math.PI) / (2.0 * Math.PI));
        }

        // 3. Entropy
        var entropy = phaseSphereService.ComputeSpectralEntropy(doc.PitchClasses);
        combined[EmbeddingSchema.SpectralEntropy] = Math.Clamp(1.0 - entropy, 0.0, 1.0);
    }

    /// <summary>
    /// Computes v1.2.1 extension features (indices 78-95).
    /// </summary>
    private static void ComputeExtensions(double[] embedding, VoicingDocument doc)
    {
        var midiNotes = doc.MidiNotes;
        var pitchClasses = doc.PitchClasses;
        var rootPc = doc.RootPitchClass ?? 0;
        var hasDefinedRoot = doc.RootPitchClass.HasValue;
        var n = midiNotes.Length;

        // Early exit: no notes means all extensions stay at 0.0
        if (n == 0) return;

        // ═══════════════════════════════════════════════════════════════════════
        // PRECOMPUTE SHARED VALUES
        // ═══════════════════════════════════════════════════════════════════════
        var tension = 1.0 - doc.Consonance;
        var stability = doc.Consonance;
        var uniquePCs = pitchClasses.Distinct().Count();
        var topPc = midiNotes.Max() % EmbeddingSchema.PitchClassCount;
        var sortedMidi = midiNotes.OrderBy(m => m).ToArray();
        var mean = sortedMidi.Average();
        var stddev = Math.Sqrt(sortedMidi.Average(m => Math.Pow(m - mean, 2)));
        var span = sortedMidi.Max() - sortedMidi.Min();

        // ═══════════════════════════════════════════════════════════════════════
        // CONTEXT DYNAMICS (78-79)
        // ═══════════════════════════════════════════════════════════════════════
        embedding[EmbeddingSchema.HarmonicInertia] = Clamp01(stability * (1.0 - tension));
        embedding[EmbeddingSchema.ResolutionPressure] = Clamp01(0.7 * tension + 0.3 * (1.0 - stability));

        // ═══════════════════════════════════════════════════════════════════════
        // TEXTURAL FEATURES (80-82)
        // ═══════════════════════════════════════════════════════════════════════
        var doublingRatio = (double)(n - uniquePCs) / Math.Max(1, n);
        embedding[EmbeddingSchema.TexturalDoublingRatio] = Clamp01(doublingRatio);

        if (hasDefinedRoot)
        {
            var rootCount = pitchClasses.Count(pc => pc == rootPc);
            embedding[EmbeddingSchema.TexturalRootDoubled] = rootCount > 1 ? 1.0 : 0.0;
        }

        if (hasDefinedRoot)
        {
            var topRelative = ((topPc - rootPc + EmbeddingSchema.PitchClassCount) % EmbeddingSchema.PitchClassCount) / 11.0;
            embedding[EmbeddingSchema.TexturalTopNoteRelative] = Clamp01(topRelative);
        }

        // ═══════════════════════════════════════════════════════════════════════
        // SPECTRAL COLOR (84-89)
        // ═══════════════════════════════════════════════════════════════════════
        embedding[EmbeddingSchema.SpectralMeanRegister] =
            Clamp01((mean - EmbeddingSchema.MinMidi) / EmbeddingSchema.MidiRange);

        var registerSpread = Clamp01(stddev / EmbeddingSchema.SpreadMax);
        embedding[EmbeddingSchema.SpectralRegisterSpread] = registerSpread;

        var lowCount = sortedMidi.Count(m => m < EmbeddingSchema.LowThreshold);
        embedding[EmbeddingSchema.SpectralLowEndWeight] = (double)lowCount / n;

        var highCount = sortedMidi.Count(m => m > EmbeddingSchema.HighThreshold);
        embedding[EmbeddingSchema.SpectralHighEndWeight] = (double)highCount / n;

        var smallIntervals = 0;
        for (var i = 0; i < sortedMidi.Length - 1; i++)
        {
            if (sortedMidi[i + 1] - sortedMidi[i] <= EmbeddingSchema.CloseIntervalThreshold)
                smallIntervals++;
        }
        var localClustering = (double)smallIntervals / Math.Max(1, n - 1);
        embedding[EmbeddingSchema.SpectralLocalClustering] = Clamp01(localClustering);

        var beatRisk = 0.0;
        for (var i = 0; i < sortedMidi.Length - 1; i++)
        {
            var interval = sortedMidi[i + 1] - sortedMidi[i];
            if (interval <= EmbeddingSchema.CloseIntervalThreshold)
            {
                var midpoint = (sortedMidi[i] + sortedMidi[i + 1]) / 2.0;
                var weight = 1.0 - Clamp01((midpoint - EmbeddingSchema.MinMidi) / EmbeddingSchema.MidiRange);
                beatRisk += weight;
            }
        }
        embedding[EmbeddingSchema.SpectralRoughnessProxy] = Clamp01(beatRisk / Math.Max(1, n - 1));

        // ═══════════════════════════════════════════════════════════════════════
        // RELATIONAL (83)
        // ═══════════════════════════════════════════════════════════════════════
        embedding[EmbeddingSchema.RelationalSmoothnessBudget] =
            Clamp01(0.5 * doublingRatio + 0.7 * (1 - registerSpread) - 0.3 * localClustering);

        // ═══════════════════════════════════════════════════════════════════════
        // EXTENDED TEXTURE (90-95)
        // ═══════════════════════════════════════════════════════════════════════
        embedding[EmbeddingSchema.ExtendedBassMelodySpan] = Clamp01(span / EmbeddingSchema.SpanMax);

        if (hasDefinedRoot)
        {
            var majorThirdPc = (rootPc + 4) % EmbeddingSchema.PitchClassCount;
            var minorThirdPc = (rootPc + 3) % EmbeddingSchema.PitchClassCount;
            var thirdCount = pitchClasses.Count(pc => pc == majorThirdPc || pc == minorThirdPc);
            embedding[EmbeddingSchema.ExtendedThirdDoubled] = thirdCount > 1 ? 1.0 : 0.0;
        }

        if (hasDefinedRoot)
        {
            var fifthPc = (rootPc + 7) % EmbeddingSchema.PitchClassCount;
            var fifthCount = pitchClasses.Count(pc => pc == fifthPc);
            embedding[EmbeddingSchema.ExtendedFifthDoubled] = fifthCount > 1 ? 1.0 : 0.0;
        }

        embedding[EmbeddingSchema.ExtendedOpenPosition] =
            span > EmbeddingSchema.OpenPositionThreshold ? 1.0 : 0.0;

        var innerCount = sortedMidi.Count(m =>
            m > EmbeddingSchema.LowThreshold && m < EmbeddingSchema.HighThreshold);
        embedding[EmbeddingSchema.ExtendedInnerVoiceDensity] = (double)innerCount / Math.Max(1, n);

        if (hasDefinedRoot)
        {
            var hasRoot = pitchClasses.Contains(rootPc);
            embedding[EmbeddingSchema.ExtendedOmittedRoot] = hasRoot ? 0.0 : 1.0;
        }
    }


    private static double Clamp01(double value) => Math.Min(1.0, Math.Max(0.0, value));
}
