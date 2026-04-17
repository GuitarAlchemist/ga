namespace GA.Business.ML.Embeddings;

using System.IO.Hashing;
using System.Text;

/// <summary>
///     Role a partition plays in the similarity formula.
/// </summary>
public enum PartitionRole
{
    /// <summary>Hard filter — excluded from similarity (e.g. object type).</summary>
    Identity,

    /// <summary>Contributes to weighted cosine similarity.</summary>
    Similarity,

    /// <summary>Informational/derived feature — excluded from similarity.</summary>
    Info,
}

/// <summary>
///     A contiguous range of dimensions in the raw 228-dim embedding vector,
///     with its similarity weight and role. Single source of truth for the
///     partition layout — consumed by <c>OptickIndexWriter</c>, search tooling,
///     and cross-repo schema-hash verification.
/// </summary>
/// <param name="Name">Partition name (e.g. "STRUCTURE").</param>
/// <param name="Start">Inclusive start index in the raw 228-dim space.</param>
/// <param name="End">Inclusive end index in the raw 228-dim space.</param>
/// <param name="SimilarityWeight">Weight used in similarity; 0 if Role != Similarity.</param>
/// <param name="Role">Role this partition plays.</param>
public readonly record struct EmbeddingPartition(
    string Name,
    int Start,
    int End,
    float SimilarityWeight,
    PartitionRole Role)
{
    /// <summary>Number of dimensions in this partition.</summary>
    public int Dim => End - Start + 1;

    /// <summary>Pre-computed sqrt(weight) — zero if the partition is excluded from similarity.</summary>
    public float SqrtWeight => SimilarityWeight > 0f ? MathF.Sqrt(SimilarityWeight) : 0f;
}

/// <summary>
///     Canonical definition of the Musical Embedding Vector Schema (v1.3.1).
///     Implements OPTIC-K Schema v1.3.1.
///     <para>
///         This schema implements the OPTIC/K equivalence theory within a practical ML embedding format.
///         The vector is partitioned into semantic subspaces, each serving a distinct purpose:
///     </para>
///     <list type="table">
///         <listheader>
///             <term>Partition</term><description>Purpose</description>
///         </listheader>
///         <item>
///             <term>IDENTITY (0-5)</term>
///             <description>Object type classification (hard filter, not for similarity)</description>
///         </item>
///         <item>
///             <term>STRUCTURE (6-29)</term><description>OPTIC/K Core: pitch-class set invariants</description>
///         </item>
///         <item>
///             <term>MORPHOLOGY (30-53)</term><description>Physical realization (fretboard geometry)</description>
///         </item>
///         <item>
///             <term>CONTEXT (54-65)</term><description>Temporal motion and harmonic function</description>
///         </item>
///         <item>
///             <term>SYMBOLIC (66-77)</term><description>Technique and style tags</description>
///         </item>
///         <item>
///             <term>EXTENSIONS (78-95)</term><description>Derived textural features (v1.2.1)</description>
///         </item>
///         <item>
///             <term>SPECTRAL (96-108)</term><description>Spectral geometry features (v1.3.1)</description>
///         </item>
///     </list>
///     <para>
///         <b>Similarity Formula</b>: Weighted Partition Cosine
///         <code>Similarity(A,B) = Σ weight[p] × cosine(normalize(A[p]), normalize(B[p]))</code>
///         Where weights are: STRUCTURE=0.45, MORPHOLOGY=0.25, CONTEXT=0.20, SYMBOLIC=0.10
///         IDENTITY, EXTENSIONS and SPECTRAL are excluded from similarity scoring.
///     </para>
///     <para>
///         See <c>OPTIC-K_Embedding_Schema_v1.3.1.md</c> for the complete specification.
///     </para>
/// </summary>
public static class EmbeddingSchema
{
    #region Schema Metadata

    /// <summary>Schema version identifier for compatibility checking.</summary>
    public const string Version = "OPTIC-K-v1.7";

    /// <summary>Total embedding vector dimension (228 for v1.7 with Atonal Modal).</summary>
    public const int TotalDimension = 228;

    #endregion

    #region Partition Registry

    /// <summary>
    ///     Authoritative partition registry. Every consumer that needs partition
    ///     boundaries, weights, or roles (OptickIndexWriter, query builders,
    ///     diagnostic tools) should read from this array rather than redefining
    ///     the layout locally. Prevents silent drift between writer and reader.
    /// </summary>
    public static readonly EmbeddingPartition[] Partitions =
    [
        new("IDENTITY",     0,   5,   0.00f, PartitionRole.Identity),
        new("STRUCTURE",    6,   29,  0.45f, PartitionRole.Similarity),
        new("MORPHOLOGY",   30,  53,  0.25f, PartitionRole.Similarity),
        new("CONTEXT",      54,  65,  0.20f, PartitionRole.Similarity),
        new("SYMBOLIC",     66,  77,  0.10f, PartitionRole.Similarity),
        new("EXTENSIONS",   78,  95,  0.00f, PartitionRole.Info),
        new("SPECTRAL",     96,  108, 0.00f, PartitionRole.Info),
        new("MODAL",        109, 148, 0.10f, PartitionRole.Similarity),
        new("HIERARCHY",    149, 163, 0.00f, PartitionRole.Info),
        new("ATONAL_MODAL", 164, 227, 0.00f, PartitionRole.Info),
    ];

    /// <summary>Partitions that contribute to similarity scoring (used by v4 compact index).</summary>
    public static IEnumerable<EmbeddingPartition> SimilarityPartitions =>
        Partitions.Where(p => p.Role == PartitionRole.Similarity);

    /// <summary>
    ///     Compact dimension — sum of similarity partition dims (112 for v1.7).
    ///     Used by the OPTK v4 binary format to drop info-only partitions from storage.
    /// </summary>
    public static int CompactDimension =>
        SimilarityPartitions.Sum(p => p.Dim);

    /// <summary>
    ///     Canonical OPTK v4 layout string. CRC32 of this string = v4 schema hash.
    ///     Both the C# writer and Rust reader compute from this exact byte sequence.
    /// </summary>
    public static readonly string CompactLayoutV4 = BuildCompactLayoutV4();

    /// <summary>
    ///     Schema hash for the OPTK v4 format. Writers emit this in the header;
    ///     readers compare against their own computation to detect format drift.
    /// </summary>
    public static readonly uint SchemaHashV4 =
        Crc32.HashToUInt32(Encoding.UTF8.GetBytes(CompactLayoutV4));

    private static string BuildCompactLayoutV4()
    {
        var sb = new StringBuilder("optk-v4:");
        var pos = 0;
        var first = true;
        foreach (var p in SimilarityPartitions)
        {
            if (!first) sb.Append(',');
            sb.Append(p.Name).Append(':').Append(pos).Append('-').Append(pos + p.Dim - 1);
            pos += p.Dim;
            first = false;
        }
        return sb.ToString();
    }

    #endregion

    #region Partition Offsets and Dimensions

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 1: IDENTITY (0-5)
    // Object type classification. Used for hard filtering, NOT similarity scoring.
    // Encoded as one-hot or soft one-hot.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Starting index of the IDENTITY partition.</summary>
    public const int IdentityOffset = 0;

    /// <summary>Number of dimensions in IDENTITY partition (6 object types).</summary>
    public const int IdentityDim = 6;

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 2: STRUCTURE (6-29)
    // OPTIC/K Core: encodes pitch-class set invariants.
    // - Indices 6-17: Pitch-Class Chroma (12d) - O+P invariant
    // - Index 18: Cardinality (C)
    // - Indices 19-24: Interval Class Vector (6d) - T+I invariant
    // - Index 25: Complementarity (K)
    // - Indices 26-29: Tonal properties (Tonic-ness, Dominant-pull, Tension, Stability)
    // SIMILARITY WEIGHT: 0.45 (highest - musical identity)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Starting index of the STRUCTURE partition.</summary>
    public const int StructureOffset = 6;

    /// <summary>Number of dimensions in STRUCTURE partition.</summary>
    public const int StructureDim = 24;

    /// <summary>Similarity weight for STRUCTURE partition (0.45 = highest priority).</summary>
    public const double StructureWeight = 0.45;

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 3: MORPHOLOGY (30-53)
    // Physical realization: fretboard geometry, fingering, ergonomics.
    // SIMILARITY WEIGHT: 0.25 (playability)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Starting index of the MORPHOLOGY partition.</summary>
    public const int MorphologyOffset = 30;

    /// <summary>Number of dimensions in MORPHOLOGY partition.</summary>
    public const int MorphologyDim = 24;

    /// <summary>Similarity weight for MORPHOLOGY partition (0.25).</summary>
    public const double MorphologyWeight = 0.25;

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 4: CONTEXT (54-65)
    // Temporal motion: harmonic function, tension/resolution, voice-leading potential.
    // SIMILARITY WEIGHT: 0.20
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Starting index of the CONTEXT partition.</summary>
    public const int ContextOffset = 54;

    /// <summary>Number of dimensions in CONTEXT partition.</summary>
    public const int ContextDim = 12;

    /// <summary>Similarity weight for CONTEXT partition (0.20).</summary>
    public const double ContextWeight = 0.20;

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 5: SYMBOLIC (66-77)
    // Knowledge tags: technique labels (Drop-2, Shell, Quartal) and style lineage (Jazz, Hendrix).
    // SIMILARITY WEIGHT: 0.10 (lowest - stylistic preference)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Starting index of the SYMBOLIC partition.</summary>
    public const int SymbolicOffset = 66;

    /// <summary>Number of dimensions in SYMBOLIC partition.</summary>
    public const int SymbolicDim = 12;

    /// <summary>Similarity weight for SYMBOLIC partition (0.10 = lowest priority).</summary>
    public const double SymbolicWeight = 0.10;

    /// <summary>Number of technique-related dimensions in SYMBOLIC (first half).</summary>
    public const int KnowledgeTechniqueDim = 6;

    /// <summary>Number of style-related dimensions in SYMBOLIC (second half).</summary>
    public const int KnowledgeStyleDim = 6;

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 6: EXTENSIONS (78-95)
    // Derived features added in v1.2.1. Informational only - excluded from similarity.
    // Only populated when IDENTITY is Voicing or Shape.
    // All values clamped to [0,1].
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Starting index of the EXTENSIONS partition.</summary>
    public const int ExtensionsOffset = 78;

    /// <summary>Number of dimensions in EXTENSIONS partition.</summary>
    public const int ExtensionsDim = 18;

    /// <summary>Ending index of EXTENSIONS (exclusive, = TotalDimension).</summary>
    public const int ExtensionsEnd = 96;

    #endregion

    #region Psychoacoustic Constants

    // ═══════════════════════════════════════════════════════════════════════════
    // PSYCHOACOUSTIC THRESHOLDS
    // Used for spectral color calculations. Based on guitar's typical range.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Minimum MIDI note for normalization (E2, guitar's lowest standard note).</summary>
    public const int MinMidi = 40;

    /// <summary>Maximum MIDI note for normalization (E6, guitar's practical upper limit).</summary>
    public const int MaxMidi = 88;

    /// <summary>Low register threshold (E3). Notes below this are "muddy" territory.</summary>
    public const int LowThreshold = 52;

    /// <summary>High register threshold (E5). Notes above this are "bright/airy" territory.</summary>
    public const int HighThreshold = 76;

    /// <summary>Maximum expected standard deviation for register spread normalization.</summary>
    public const double SpreadMax = 12.0;

    /// <summary>Maximum expected bass-melody span in semitones (4 octaves).</summary>
    public const double SpanMax = 48.0;

    /// <summary>Interval threshold for "close" intervals in clustering/roughness calculations.</summary>
    public const int CloseIntervalThreshold = 2;

    /// <summary>Open position threshold: voicing spans more than one octave.</summary>
    public const int OpenPositionThreshold = 12;

    /// <summary>Number of pitch classes in the chromatic scale.</summary>
    public const int PitchClassCount = 12;

    /// <summary>Number of interval classes (1-6).</summary>
    public const int IntervalClassCount = 6;

    #endregion

    #region Extension Feature Indices (78-95)

    // ═══════════════════════════════════════════════════════════════════════════
    // CONTEXT DYNAMICS (78-79)
    // Derived from tension/stability values in STRUCTURE.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 78: Harmonic Inertia - resistance to change.
    ///     Formula: clamp01(stability × (1 - tension))
    ///     High stability + Low tension = High inertia (chord wants to stay).
    /// </summary>
    public const int HarmonicInertia = 78;

    /// <summary>
    ///     Index 79: Resolution Pressure - urge to resolve.
    ///     Formula: clamp01(0.7 × tension + 0.3 × (1 - stability))
    ///     High tension = High pressure to move to next chord.
    /// </summary>
    public const int ResolutionPressure = 79;

    // ═══════════════════════════════════════════════════════════════════════════
    // TEXTURAL FEATURES (80-82)
    // Pitch doubling and melody note characteristics.
    // Indices 81-82 are ROOT-GATED: zeroed if rootPC is undefined.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 80: Doubling Ratio - proportion of doubled pitch classes.
    ///     Formula: (N - uniquePCs) / max(1, N)
    ///     0 = no doubling (thin), 1 = maximum doubling (thick/full).
    /// </summary>
    public const int TexturalDoublingRatio = 80;

    /// <summary>
    ///     Index 81: Root Doubled - binary flag for root reinforcement.
    ///     Formula: 1.0 if count(rootPC) > 1 else 0.0
    ///     ROOT-GATED: Returns 0.0 if rootPC is undefined.
    /// </summary>
    public const int TexturalRootDoubled = 81;

    /// <summary>
    ///     Index 82: Top Note Relative - interval class of melody to root.
    ///     Formula: ((topPC - rootPC + 12) % 12) / 11.0
    ///     0 = root on top, ~0.36 = 3rd on top, ~0.64 = 5th on top.
    ///     ROOT-GATED: Returns 0.0 if rootPC is undefined.
    /// </summary>
    public const int TexturalTopNoteRelative = 82;

    // ═══════════════════════════════════════════════════════════════════════════
    // RELATIONAL (83)
    // Voice-leading potential.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 83: Smoothness Budget - potential for smooth voice-leading.
    ///     Formula: clamp01(0.5×DoublingRatio + 0.7×(1-RegisterSpread) - 0.3×LocalClustering)
    ///     Higher = more voice-leading options available.
    /// </summary>
    public const int RelationalSmoothnessBudget = 83;

    // ═══════════════════════════════════════════════════════════════════════════
    // SPECTRAL COLOR (84-89)
    // Psychoacoustic texture features based on realized MIDI pitches.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 84: Mean Register - brightness proxy.
    ///     Formula: clamp01((mean(p) - MinMidi) / (MaxMidi - MinMidi))
    ///     0 = very low register, 1 = very high register.
    /// </summary>
    public const int SpectralMeanRegister = 84;

    /// <summary>
    ///     Index 85: Register Spread - voicing openness.
    ///     Formula: clamp01(stddev(p) / SpreadMax)
    ///     0 = tight/closed voicing, 1 = wide/spread voicing.
    /// </summary>
    public const int SpectralRegisterSpread = 85;

    /// <summary>
    ///     Index 86: Low End Weight - proportion of notes in muddy register.
    ///     Formula: count(p &lt; LowThreshold) / N
    ///     Higher = more bass-heavy/thick texture.
    /// </summary>
    public const int SpectralLowEndWeight = 86;

    /// <summary>
    ///     Index 87: High End Weight - proportion of notes in bright register.
    ///     Formula: count(p &gt; HighThreshold) / N
    ///     Higher = more airy/bright texture.
    /// </summary>
    public const int SpectralHighEndWeight = 87;

    /// <summary>
    ///     Index 88: Local Clustering - density of small adjacent intervals.
    ///     Formula: count(diff(sort(p)) &lt;= 2) / max(1, N-1)
    ///     Higher = more clustered/dense voicing.
    /// </summary>
    public const int SpectralLocalClustering = 88;

    /// <summary>
    ///     Index 89: Roughness Proxy - psychoacoustic dissonance estimate.
    ///     Low-register clusters weighted more heavily (beating is more audible).
    ///     Formula: clamp01(Σ(low-weighted close intervals) / max(1, N-1))
    /// </summary>
    public const int SpectralRoughnessProxy = 89;

    // ═══════════════════════════════════════════════════════════════════════════
    // EXTENDED TEXTURE (90-95)
    // Additional voicing characteristics added in v1.2.1.
    // Indices 91, 92, 95 are ROOT-GATED.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 90: Bass-Melody Span - total voicing width.
    ///     Formula: clamp01((max(p) - min(p)) / SpanMax)
    ///     0 = narrow/intimate, 1 = wide/orchestral.
    /// </summary>
    public const int ExtendedBassMelodySpan = 90;

    /// <summary>
    ///     Index 91: Third Doubled - binary flag for doubled 3rd.
    ///     Formula: 1.0 if count(3rd PC) > 1 else 0.0
    ///     Indicates potential muddiness or specific voicing style.
    ///     ROOT-GATED: Returns 0.0 if rootPC is undefined.
    /// </summary>
    public const int ExtendedThirdDoubled = 91;

    /// <summary>
    ///     Index 92: Fifth Doubled - binary flag for doubled 5th.
    ///     Formula: 1.0 if count(5th PC) > 1 else 0.0
    ///     Common in power chords; can indicate hollow/powerful sound.
    ///     ROOT-GATED: Returns 0.0 if rootPC is undefined.
    /// </summary>
    public const int ExtendedFifthDoubled = 92;

    /// <summary>
    ///     Index 93: Open Position - binary flag for spread voicing.
    ///     Formula: 1.0 if (max(p) - min(p)) > OpenPositionThreshold else 0.0
    ///     1 = voicing spans more than one octave.
    /// </summary>
    public const int ExtendedOpenPosition = 93;

    /// <summary>
    ///     Index 94: Inner Voice Density - proportion of mid-register notes.
    ///     Formula: count(LowThreshold &lt; p &lt; HighThreshold) / max(1, N)
    ///     Higher = more active inner voices (full texture vs. shell voicing).
    /// </summary>
    public const int ExtendedInnerVoiceDensity = 94;

    /// <summary>
    ///     Index 95: Omitted Root - binary flag for rootless voicing.
    ///     Formula: 1.0 if rootPC ∉ pitchClasses else 0.0
    ///     Common in jazz (rootless voicings rely on bass player).
    ///     ROOT-GATED: Returns 0.0 if rootPC is undefined.
    /// </summary>
    public const int ExtendedOmittedRoot = 95;

    #endregion

    #region Derived Constants

    /// <summary>End of IDENTITY partition (exclusive).</summary>
    public const int IdentityEnd = IdentityOffset + IdentityDim;

    /// <summary>End of STRUCTURE partition (exclusive).</summary>
    public const int StructureEnd = StructureOffset + StructureDim;

    /// <summary>End of MORPHOLOGY partition (exclusive).</summary>
    public const int MorphologyEnd = MorphologyOffset + MorphologyDim;

    /// <summary>End of CONTEXT partition (exclusive).</summary>
    public const int ContextEnd = ContextOffset + ContextDim;

    /// <summary>End of SYMBOLIC partition (exclusive).</summary>
    public const int SymbolicEnd = SymbolicOffset + SymbolicDim;

    /// <summary>MIDI range for normalization (MaxMidi - MinMidi).</summary>
    public const double MidiRange = MaxMidi - MinMidi;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 7: SPECTRAL GEOMETRY (96-107) — NEW in v1.3
    // DFT-based features for harmonic navigation (not classification).
    // Per Lewin's Lemma: ICV = |DFT|², so DFT provides finer geometry.
    // EXCLUDED from similarity scoring (informational only).
    // ═══════════════════════════════════════════════════════════════════════════

    #region Spectral Geometry Partition (v1.3)

    /// <summary>Starting index of the SPECTRAL partition.</summary>
    public const int SpectralOffset = 96;

    /// <summary>Number of dimensions in SPECTRAL partition (6 magnitudes + 6 phases + 1 entropy).</summary>
    public const int SpectralDim = 13;

    /// <summary>End of SPECTRAL partition (exclusive).</summary>
    public const int SpectralEnd = SpectralOffset + SpectralDim;

    // ═══════════════════════════════════════════════════════════════════════════
    // FOURIER MAGNITUDES (96-101)
    // Which periodicities dominate the pitch-class set.
    // Normalized by sqrt(cardinality).
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 96: Fourier Magnitude k=1 — Chromatic clumping.
    ///     Formula: |DFT[1]| / sqrt(N)
    ///     High value = pitches clustered together on the circle.
    /// </summary>
    public const int FourierMagK1 = 96;

    /// <summary>
    ///     Index 97: Fourier Magnitude k=2 — Whole-tone structure.
    ///     Formula: |DFT[2]| / sqrt(N)
    ///     High value = whole-tone scale affinity (ic2, ic6 dominance).
    /// </summary>
    public const int FourierMagK2 = 97;

    /// <summary>
    ///     Index 98: Fourier Magnitude k=3 — Diminished structure.
    ///     Formula: |DFT[3]| / sqrt(N)
    ///     High value = minor-third cycle affinity (diminished chords).
    /// </summary>
    public const int FourierMagK3 = 98;

    /// <summary>
    ///     Index 99: Fourier Magnitude k=4 — Augmented structure.
    ///     Formula: |DFT[4]| / sqrt(N)
    ///     High value = major-third cycle affinity (augmented chords).
    /// </summary>
    public const int FourierMagK4 = 99;

    /// <summary>
    ///     Index 100: Fourier Magnitude k=5 — Diatonic structure.
    ///     Formula: |DFT[5]| / sqrt(N)
    ///     High value = fifths cycle affinity (key-ness, diatonicism).
    ///     This is the most important for tonal music.
    /// </summary>
    public const int FourierMagK5 = 100;

    /// <summary>
    ///     Index 101: Fourier Magnitude k=6 — Tritone structure.
    ///     Formula: |DFT[6]| / sqrt(N)
    ///     High value = tritone symmetry (dominant seventh, Lydian).
    /// </summary>
    public const int FourierMagK6 = 101;

    // ═══════════════════════════════════════════════════════════════════════════
    // FOURIER PHASES (102-107)
    // Position on the pitch circle for each periodicity.
    // Enables spectral voice-leading distance.
    // Normalized to [0,1] via (arg + π) / (2π).
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 102: Fourier Phase k=1 — Position for chromatic clumping.
    ///     Formula: (arg(DFT[1]) + π) / (2π)
    /// </summary>
    public const int FourierPhaseK1 = 102;

    /// <summary>
    ///     Index 103: Fourier Phase k=2 — Position on whole-tone cycle.
    ///     Formula: (arg(DFT[2]) + π) / (2π)
    /// </summary>
    public const int FourierPhaseK2 = 103;

    /// <summary>
    ///     Index 104: Fourier Phase k=3 — Position on diminished cycle.
    ///     Formula: (arg(DFT[3]) + π) / (2π)
    /// </summary>
    public const int FourierPhaseK3 = 104;

    /// <summary>
    ///     Index 105: Fourier Phase k=4 — Position on augmented cycle.
    ///     Formula: (arg(DFT[4]) + π) / (2π)
    /// </summary>
    public const int FourierPhaseK4 = 105;

    /// <summary>
    ///     Index 106: Fourier Phase k=5 — Position on diatonic/fifths cycle.
    ///     Formula: (arg(DFT[5]) + π) / (2π)
    ///     Most important phase for tonal voice-leading.
    /// </summary>
    public const int FourierPhaseK5 = 106;

    /// <summary>
    ///     Index 107: Fourier Phase k=6 — Position on tritone cycle.
    ///     Formula: (arg(DFT[6]) + π) / (2π)
    /// </summary>
    public const int FourierPhaseK6 = 107;

    // ═══════════════════════════════════════════════════════════════════════════
    // SPECTRAL ENTROPY (108)
    // Measures how "organized" or "peaky" the power spectrum is.
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    ///     Index 108: Spectral Entropy.
    ///     Formula: 1.0 - (Entropy / MaxEntropy)
    ///     MaxEntropy = log2(7) ≈ 2.807 (for 6 bins + DC).
    ///     High (1.0) = Organized/Pure. Low (0.0) = Chaotic/Noisy.
    /// </summary>
    public const int SpectralEntropy = 108;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 8: MODAL FLAVORS (109-122) — NEW in v1.4
    // Encodes characteristic modal colors for semantic search.
    // Each bit represents presence of a specific modal "flavor".
    // INCLUDED in similarity scoring (SYMBOLIC weight applies).
    // ═══════════════════════════════════════════════════════════════════════════

    #region Modal Flavor Partition (v1.6)

    /// <summary>Starting index of the MODAL partition.</summary>
    public const int ModalOffset = 109;

    /// <summary>Number of dimensions in MODAL partition (40 slots).</summary>
    public const int ModalDim = 40;

    /// <summary>End of MODAL partition (exclusive).</summary>
    public const int ModalEnd = ModalOffset + ModalDim;

    // === Major Scale Modes (109-115) ===
    public const int ModalIonian = 109;
    public const int ModalDorian = 110;
    public const int ModalPhrygian = 111;
    public const int ModalLydian = 112;
    public const int ModalMixolydian = 113;
    public const int ModalAeolian = 114;
    public const int ModalLocrian = 115;

    // === Harmonic Minor Modes (116-122) ===
    public const int ModalHarmonicMinor = 116;
    public const int ModalLocrianNatural6 = 117;
    public const int ModalIonianAugmented = 118;
    public const int ModalDorianSharp4 = 119;
    public const int ModalPhrygianDominant = 120;
    public const int ModalLydianSharp2 = 121;
    public const int ModalAlteredDoubleFlat7 = 122;

    // === Melodic Minor Modes (123-129) ===
    public const int ModalMelodicMinor = 123;
    public const int ModalDorianFlat2 = 124;
    public const int ModalLydianAugmented = 125;
    public const int ModalLydianDominant = 126;
    public const int ModalMixolydianFlat6 = 127;
    public const int ModalLocrianNatural2 = 128;
    public const int ModalAltered = 129;

    // === Harmonic Major Modes (130-136) ===
    public const int ModalHarmonicMajor = 130;
    public const int ModalDorianFlat5 = 131;
    public const int ModalPhrygianFlat4 = 132;
    public const int ModalLydianFlat3 = 133;
    public const int ModalMixolydianFlat2 = 134;
    public const int ModalLydianAugmentedSharp2 = 135;
    public const int ModalLocrianDoubleFlat7 = 136;

    // === Pentatonic and Other (137+) ===
    public const int ModalPentatonicMajor = 137;
    public const int ModalPentatonicMinor = 138;
    public const int ModalBlues = 139;
    public const int ModalWholeTone = 140;
    public const int ModalDiminished = 141;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 9: HIERARCHY (149-156) — NEW in v1.5/v1.6
    // Structural complexity and hierarchical depth.
    // ═══════════════════════════════════════════════════════════════════════════

    #region Hierarchy Partition (v1.6)

    /// <summary>Starting index of the HIERARCHY partition.</summary>
    public const int HierarchyOffset = 149;

    /// <summary>Number of dimensions in HIERARCHY partition.</summary>
    public const int HierarchyDim = 8;

    /// <summary>End of HIERARCHY partition (exclusive).</summary>
    public const int HierarchyEnd = HierarchyOffset + HierarchyDim;

    /// <summary>
    ///     Index 149: Harmonic Complexity Score.
    ///     Normalized structural depth (0=Note, 1=Polychord).
    /// </summary>
    public const int HierarchyComplexityScore = 149;

    #endregion

    // ═══════════════════════════════════════════════════════════════════════════
    // PARTITION 10: ATONAL MODAL (164-180) — NEW in v1.7
    // Bridge between tonal modes and atonal modal families.
    // Maps EVERY set class to a structural modal coordinate.
    // ═══════════════════════════════════════════════════════════════════════════

    #region Atonal Modal Partition (v1.7)

    /// <summary>Starting index of the ATONAL_MODAL partition.</summary>
    public const int AtonalModalOffset = 164;

    /// <summary>Number of dimensions in ATONAL_MODAL partition.</summary>
    public const int AtonalModalDim = 17;

    /// <summary>End of ATONAL_MODAL partition (exclusive).</summary>
    public const int AtonalModalEnd = AtonalModalOffset + AtonalModalDim;

    /// <summary>Index 164: Mode ranking in family (0.0 = prime, 1.0 = last).</summary>
    public const int AtonalModeRank = 164;

    /// <summary>Index 165: Family member count (normalized by 12).</summary>
    public const int AtonalFamilySize = 165;

    /// <summary>Index 166: Is Symmetric/Monomodal (1.0 = yes).</summary>
    public const int AtonalIsSymmetric = 166;

    /// <summary>Index 167: Is Primary Tonal Mode (Hero Mode) (1.0 = yes).</summary>
    public const int AtonalIsHeroMode = 167;

    /// <summary>Indices 168-173: Normalized ICV Profile (6d).</summary>
    public const int AtonalIcvProfileOffset = 168;

    /// <summary>Index 174: Set Class Prime Form ID (normalized 0-4095).</summary>
    public const int AtonalPrimeFormId = 174;

    /// <summary>Index 175: Pitch Class Diversity (Entropy of spacing).</summary>
    public const int AtonalPcDiversity = 175;

    /// <summary>Index 176: Step Size Brightness (Preference for large intervals).</summary>
    public const int AtonalStepBrightness = 176;

    /// <summary>Index 177: Consonance Potential (IC 3,4,5 dominance).</summary>
    public const int AtonalConsonancePotential = 177;

    /// <summary>Index 178: Dissonance Index (IC 1,2,6 dominance).</summary>
    public const int AtonalDissonanceIndex = 178;

    /// <summary>Index 179: Center of Gravity (Mass balance on chromatic circle).</summary>
    public const int AtonalCenterOfGravity = 179;

    /// <summary>Index 180: Z-Relationship Flag (1.0 if ICV shared by multiple set classes).</summary>
    public const int AtonalIsZRelated = 180;

    #endregion
}
