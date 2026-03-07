namespace GenerateNatData.Phase2;

using System.Numerics;
using GA.Business.ML.Embeddings;

/// <summary>
///     Standalone OPTIC-K v1.7 embedding computer for batch pipeline use.
///     Computes all 228 dimensions from raw MIDI notes and fret positions without DI services.
/// </summary>
/// <remarks>
///     Partitions computed:
///     - IDENTITY   (0-5):    Voicing object type flag
///     - STRUCTURE  (6-29):   PCS presence bits, cardinality, ICV, tonal props
///     - MORPHOLOGY (30-53):  Fret geometry, span, density, string layout
///     - CONTEXT    (54-65):  Zeros (root unknown without chord analysis)
///     - SYMBOLIC   (66-77):  Zeros (no semantic tags in batch mode)
///     - EXTENSIONS (78-95):  Psychoacoustic features from MIDI notes
///     - SPECTRAL   (96-108): DFT magnitude/phase of PCS indicator vector
///     - MODAL      (109-148): Zeros (complex tonal analysis, future work)
///     - HIERARCHY  (149-163): Zeros (future work)
///     - ATONAL_MODAL(164-180): Zeros (future work)
///     - Reserved  (181-227): Zeros
/// </remarks>
public static class EmbeddingComputer
{
    /// <summary>Standard EADGBE open-string MIDI notes, indexed by string position 0..5 (string 1..6).</summary>
    /// <remarks>String 1 = E4 (64), String 6 = E2 (40).</remarks>
    public static readonly int[] StandardTuningMidi = [64, 59, 55, 50, 45, 40];

    /// <summary>
    ///     Computes the full 228-dim OPTIC-K embedding vector from raw scratch-file data.
    /// </summary>
    /// <param name="frets">Per-string fret values: -1=muted, 0=open, 1-24=fretted.</param>
    /// <param name="tuningMidi">Open-string MIDI notes per string (default: standard EADGBE).</param>
    /// <returns>float[228] embedding vector.</returns>
    public static float[] Compute(ReadOnlySpan<sbyte> frets, int[]? tuningMidi = null)
    {
        tuningMidi ??= StandardTuningMidi;
        var stringCount = Math.Min(frets.Length, tuningMidi.Length);

        // Extract MIDI notes and fret values for active (non-muted) strings
        var midiNotes = new List<int>(stringCount);
        var activeFrets = new List<int>(stringCount);
        var minActiveFret = int.MaxValue;
        var maxActiveFret = int.MinValue;

        for (var s = 0; s < stringCount; s++)
        {
            var fret = frets[s];
            if (fret < 0) continue; // muted

            var midi = tuningMidi[s] + fret;
            midiNotes.Add(midi);
            activeFrets.Add(fret);
            if (fret > 0) // exclude open strings from span
            {
                if (fret < minActiveFret) minActiveFret = fret;
                if (fret > maxActiveFret) maxActiveFret = fret;
            }
        }

        var embedding = new float[EmbeddingSchema.TotalDimension];
        var noteCount = midiNotes.Count;
        if (noteCount == 0) return embedding;

        // Compute pitch class set presence
        Span<int> pitchClasses = stackalloc int[noteCount];
        Span<float> pcsBits = stackalloc float[12];
        for (var i = 0; i < noteCount; i++)
        {
            var pc = ((midiNotes[i] % 12) + 12) % 12;
            pitchClasses[i] = pc;
            pcsBits[pc] = 1.0f;
        }

        // ── PARTITION 1: IDENTITY (0-5) ──────────────────────────────────────
        embedding[1] = 1.0f; // ObjectKind.Voicing = index 1

        // ── PARTITION 2: STRUCTURE (6-29) ────────────────────────────────────
        ComputeStructure(embedding, pcsBits, pitchClasses, noteCount);

        // ── PARTITION 3: MORPHOLOGY (30-53) ──────────────────────────────────
        ComputeMorphology(embedding, frets, midiNotes, activeFrets,
            minActiveFret, maxActiveFret, stringCount);

        // ── PARTITIONS 4+5: CONTEXT + SYMBOLIC (54-77) ───────────────────────
        // Zeros: root and harmonic function unknown in batch mode

        // ── PARTITION 6: EXTENSIONS (78-95) ──────────────────────────────────
        ComputeExtensions(embedding, midiNotes, pitchClasses, noteCount);

        // ── PARTITION 7: SPECTRAL (96-108) ───────────────────────────────────
        ComputeSpectral(embedding, pcsBits);

        // Partitions 8-10 (MODAL, HIERARCHY, ATONAL_MODAL): zeros — future work

        return embedding;
    }

    private static void ComputeStructure(
        float[] embedding,
        ReadOnlySpan<float> pcsBits,
        ReadOnlySpan<int> pitchClasses,
        int noteCount)
    {
        var offset = EmbeddingSchema.StructureOffset; // 6

        // PCS presence bits (dims 6-17, 12 dims)
        for (var pc = 0; pc < 12; pc++)
            embedding[offset + pc] = pcsBits[pc];

        // Cardinality (dim 18) — normalized by max chord size (6 strings)
        var uniquePcCount = 0;
        for (var pc = 0; pc < 12; pc++)
            if (pcsBits[pc] > 0) uniquePcCount++;
        embedding[offset + 12] = uniquePcCount / 12.0f;

        // ICV (dims 19-24, 6 dims) — counts of each interval class 1-6
        Span<float> icv = stackalloc float[6];
        for (var i = 0; i < 12; i++)
        {
            if (pcsBits[i] == 0) continue;
            for (var j = i + 1; j < 12; j++)
            {
                if (pcsBits[j] == 0) continue;
                var interval = j - i;
                var ic = interval > 6 ? 12 - interval : interval; // interval class 1-6
                icv[ic - 1]++;
            }
        }

        // Normalize ICV by maximum possible (C(uniquePcCount,2))
        var maxPairs = Math.Max(1, uniquePcCount * (uniquePcCount - 1) / 2.0f);
        for (var k = 0; k < 6; k++)
            embedding[offset + 13 + k] = icv[k] / maxPairs;

        // Complementarity (dim 25) — ratio of unique PCs to 12
        embedding[offset + 19] = 1.0f - uniquePcCount / 12.0f;

        // Tonal properties (dims 26-29, 4 dims) — zeros (require root analysis)
        // offset+20..+23 remain 0.0f
    }

    private static void ComputeMorphology(
        float[] embedding,
        ReadOnlySpan<sbyte> frets,
        List<int> midiNotes,
        List<int> activeFrets,
        int minActiveFret,
        int maxActiveFret,
        int stringCount)
    {
        var offset = EmbeddingSchema.MorphologyOffset; // 30
        var noteCount = midiNotes.Count;
        if (noteCount == 0) return;

        // Fret span of non-open fretting hand positions (dims 30-31)
        var fretSpan = minActiveFret == int.MaxValue ? 0 : maxActiveFret - minActiveFret;
        embedding[offset + 0] = fretSpan / 24.0f;               // normalized span [0,1]
        embedding[offset + 1] = noteCount / (float)stringCount;  // note density

        // Average fret (dim 32, normalized 0-1)
        var avgFret = activeFrets.Count > 0
            ? activeFrets.Average()
            : 0.0;
        embedding[offset + 2] = (float)(avgFret / 24.0);

        // Barre indicator (dim 33): min active fret > 0 and 3+ strings share that fret
        var barreIndicator = 0.0f;
        if (minActiveFret > 0 && minActiveFret != int.MaxValue)
        {
            var atMinFret = 0;
            for (var s = 0; s < frets.Length && s < stringCount; s++)
                if (frets[s] == minActiveFret) atMinFret++;
            if (atMinFret >= 3) barreIndicator = 1.0f;
        }

        embedding[offset + 3] = barreIndicator;

        // Open string count (dim 34, normalized)
        var openCount = 0;
        for (var s = 0; s < frets.Length && s < stringCount; s++)
            if (frets[s] == 0) openCount++;
        embedding[offset + 4] = openCount / (float)stringCount;

        // Bass / treble string indices (dim 35-36)
        // Bass = highest MIDI = last in sorted midiNotes
        var bassNote = midiNotes.Min();
        var trebleNote = midiNotes.Max();
        embedding[offset + 5] = ((bassNote % 12) + 12) % 12 / 11.0f;
        embedding[offset + 6] = ((trebleNote % 12) + 12) % 12 / 11.0f;

        // Bass-treble span in semitones (dim 37, normalized by 4 octaves = 48)
        embedding[offset + 7] = Math.Min(1.0f, (trebleNote - bassNote) / 48.0f);

        // Per-string normalized fret positions (dims 38-43, one per string)
        for (var s = 0; s < Math.Min(stringCount, 6); s++)
        {
            var fret = s < frets.Length ? frets[s] : (sbyte)(-1);
            embedding[offset + 8 + s] = fret < 0 ? 0.0f : fret / 24.0f;
        }

        // String activity bits (dims 44-49, one per string)
        for (var s = 0; s < Math.Min(stringCount, 6); s++)
        {
            var fret = s < frets.Length ? frets[s] : (sbyte)(-1);
            embedding[offset + 14 + s] = fret >= 0 ? 1.0f : 0.0f;
        }

        // Min fret normalized (dim 50)
        embedding[offset + 20] = minActiveFret == int.MaxValue ? 0.0f : minActiveFret / 24.0f;

        // IsRootless proxy: 0.0 (unknown without root analysis) (dim 51)
        // dims 51-53 remain 0.0f
    }

    private static void ComputeExtensions(
        float[] embedding,
        List<int> midiNotes,
        ReadOnlySpan<int> pitchClasses,
        int noteCount)
    {
        if (noteCount == 0) return;

        var sortedMidi = (int[])[.. midiNotes.OrderBy(m => m)];
        var mean = sortedMidi.Average();
        var variance = sortedMidi.Average(m => (m - mean) * (m - mean));
        var stddev = Math.Sqrt(variance);
        var span = sortedMidi[^1] - sortedMidi[0];

        // Context dynamics (78-79) — consonance unknown, use neutral 0.5
        const double consonance = 0.5;
        const double tension = 0.5;
        embedding[EmbeddingSchema.HarmonicInertia] = (float)Clamp01(consonance * (1 - tension));
        embedding[EmbeddingSchema.ResolutionPressure] = (float)Clamp01(0.7 * tension + 0.3 * (1 - consonance));

        // Textural doubling ratio (80)
        var uniquePcCount = new HashSet<int>(pitchClasses.ToArray()).Count;
        var doublingRatio = (double)(noteCount - uniquePcCount) / Math.Max(1, noteCount);
        embedding[EmbeddingSchema.TexturalDoublingRatio] = (float)Clamp01(doublingRatio);

        // Dims 81-82 (root-gated): zeros — root unknown

        // Spectral color (84-89)
        embedding[EmbeddingSchema.SpectralMeanRegister] =
            (float)Clamp01((mean - EmbeddingSchema.MinMidi) / EmbeddingSchema.MidiRange);

        var registerSpread = Clamp01(stddev / EmbeddingSchema.SpreadMax);
        embedding[EmbeddingSchema.SpectralRegisterSpread] = (float)registerSpread;

        var lowCount = sortedMidi.Count(m => m < EmbeddingSchema.LowThreshold);
        embedding[EmbeddingSchema.SpectralLowEndWeight] = (float)((double)lowCount / noteCount);

        var highCount = sortedMidi.Count(m => m > EmbeddingSchema.HighThreshold);
        embedding[EmbeddingSchema.SpectralHighEndWeight] = (float)((double)highCount / noteCount);

        var smallIntervals = 0;
        var beatRisk = 0.0;
        for (var i = 0; i < sortedMidi.Length - 1; i++)
        {
            var diff = sortedMidi[i + 1] - sortedMidi[i];
            if (diff <= EmbeddingSchema.CloseIntervalThreshold)
            {
                smallIntervals++;
                var midpoint = (sortedMidi[i] + sortedMidi[i + 1]) / 2.0;
                var weight = 1.0 - Clamp01((midpoint - EmbeddingSchema.MinMidi) / EmbeddingSchema.MidiRange);
                beatRisk += weight;
            }
        }

        var localClustering = (double)smallIntervals / Math.Max(1, noteCount - 1);
        embedding[EmbeddingSchema.SpectralLocalClustering] = (float)Clamp01(localClustering);
        embedding[EmbeddingSchema.SpectralRoughnessProxy] = (float)Clamp01(beatRisk / Math.Max(1, noteCount - 1));

        // Relational smoothness (83)
        embedding[EmbeddingSchema.RelationalSmoothnessBudget] =
            (float)Clamp01(0.5 * doublingRatio + 0.7 * (1 - registerSpread) - 0.3 * localClustering);

        // Extended texture (90-95)
        embedding[EmbeddingSchema.ExtendedBassMelodySpan] = (float)Clamp01(span / EmbeddingSchema.SpanMax);
        embedding[EmbeddingSchema.ExtendedOpenPosition] = span > EmbeddingSchema.OpenPositionThreshold ? 1.0f : 0.0f;

        var innerCount = sortedMidi.Count(m => m > EmbeddingSchema.LowThreshold && m < EmbeddingSchema.HighThreshold);
        embedding[EmbeddingSchema.ExtendedInnerVoiceDensity] = (float)((double)innerCount / Math.Max(1, noteCount));

        // Dims 91, 92, 95 (root-gated): zeros — root unknown
    }

    private static void ComputeSpectral(float[] embedding, ReadOnlySpan<float> pcsBits)
    {
        // Compute DFT of the 12-element PCS indicator vector
        // DFT[k] = Σ_{n=0}^{11} x[n] * e^{-2πikn/12}
        var magnitudes = new float[6];
        var phases = new float[6];
        var totalPower = 0.0;

        for (var k = 1; k <= 6; k++)
        {
            var re = 0.0;
            var im = 0.0;
            for (var n = 0; n < 12; n++)
            {
                if (pcsBits[n] == 0) continue;
                var angle = -2.0 * Math.PI * k * n / 12.0;
                re += Math.Cos(angle);
                im += Math.Sin(angle);
            }

            var magnitude = Math.Sqrt(re * re + im * im);
            magnitudes[k - 1] = (float)magnitude;
            phases[k - 1] = (float)((Math.Atan2(im, re) + Math.PI) / (2.0 * Math.PI));
            totalPower += magnitude * magnitude;
        }

        // Normalize magnitudes by sqrt(cardinality) following EmbeddingSchema convention
        var cardinality = 0;
        for (var pc = 0; pc < 12; pc++)
            if (pcsBits[pc] > 0) cardinality++;
        var normFactor = cardinality > 0 ? Math.Sqrt(cardinality) : 1.0;

        int[] magIndices = [
            EmbeddingSchema.FourierMagK1, EmbeddingSchema.FourierMagK2, EmbeddingSchema.FourierMagK3,
            EmbeddingSchema.FourierMagK4, EmbeddingSchema.FourierMagK5, EmbeddingSchema.FourierMagK6
        ];
        int[] phaseIndices = [
            EmbeddingSchema.FourierPhaseK1, EmbeddingSchema.FourierPhaseK2, EmbeddingSchema.FourierPhaseK3,
            EmbeddingSchema.FourierPhaseK4, EmbeddingSchema.FourierPhaseK5, EmbeddingSchema.FourierPhaseK6
        ];

        for (var k = 0; k < 6; k++)
        {
            embedding[magIndices[k]] = (float)Clamp01(magnitudes[k] / normFactor);
            embedding[phaseIndices[k]] = phases[k]; // already in [0,1]
        }

        // Spectral entropy (108): 1 - normalized entropy of power spectrum
        if (totalPower > 0)
        {
            var entropy = 0.0;
            for (var k = 0; k < 6; k++)
            {
                var p = (magnitudes[k] * magnitudes[k]) / totalPower;
                if (p > 0) entropy -= p * Math.Log2(p);
            }

            const double maxEntropy = 2.807; // log2(7)
            embedding[EmbeddingSchema.SpectralEntropy] = (float)Clamp01(1.0 - entropy / maxEntropy);
        }
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);
}
