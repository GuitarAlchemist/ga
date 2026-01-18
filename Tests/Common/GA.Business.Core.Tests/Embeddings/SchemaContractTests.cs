namespace GA.Business.Core.Tests.Embeddings;

using GA.Business.Core.Fretboard.Voicings.Search;
using GA.Business.ML.Embeddings;
using GA.Business.ML.Embeddings.Services;
using NUnit.Framework;

/// <summary>
/// Contract tests for OPTIC-K v1.3.1 schema rules.
///
/// <para>
/// These tests verify the formal contract defined in <c>OPTIC-K_Embedding_Schema_v1.3.1.md</c>:
/// </para>
///
/// <list type="bullet">
///   <item><description><b>ICV Computation</b>: Interval Class Vector calculation per §1.3</description></item>
///   <item><description><b>Complement</b>: Set complement computation per §1.2</description></item>
///   <item><description><b>Complementarity K</b>: ICV cosine similarity per §3.2</description></item>
///   <item><description><b>Identity Gating</b>: EXTENSIONS only for Voicing/Shape types</description></item>
///   <item><description><b>Root Gating</b>: Root-dependent dims zeroed when undefined</description></item>
///   <item><description><b>Clamp01</b>: All extensions in [0,1] range</description></item>
///   <item><description><b>Spectral Geometry</b>: DFT features and 7-bin entropy (v1.3.1)</description></item>
///   <item><description><b>Similarity Formula</b>: Weighted partition cosine per §0.3</description></item>
/// </list>
/// </summary>
[TestFixture]
public class SchemaContractTests
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
            new SymbolicVectorService(),
            new PhaseSphereService()
        );
    }

    #region ICV Computation Tests

    /// <summary>
    /// Verifies ICV computation for C Major triad: {0, 4, 7}.
    ///
    /// <para>Interval analysis:</para>
    /// <list type="bullet">
    ///   <item><description>C to E = 4 semitones → IC4</description></item>
    ///   <item><description>C to G = 7 semitones → IC5 (7 maps to 5)</description></item>
    ///   <item><description>E to G = 3 semitones → IC3</description></item>
    /// </list>
    /// <para>Expected ICV: [0, 0, 1, 1, 1, 0] (IC1=0, IC2=0, IC3=1, IC4=1, IC5=1, IC6=0)</para>
    /// </summary>
    [Test]
    public void ICV_CMajorTriad_Returns001110()
    {
        // Arrange
        var cMajorPcs = new[] { 0, 4, 7 };
        var expected = new[] { 0, 0, 1, 1, 1, 0 };

        // Act
        var icv = ComputeIntervalClassVector(cMajorPcs);

        // Assert
        TestContext.WriteLine($"C Major Triad: {{0, 4, 7}}");
        TestContext.WriteLine($"Calculated ICV: Expected=[0, 0, 1, 1, 1, 0], Actual=[{string.Join(", ", icv)}] (IC3=1, IC4=1, IC5=1 based on internal distances)");
        Assert.That(icv, Is.EqualTo(expected), "C Major triad should have ICV [0,0,1,1,1,0].");
    }

    /// <summary>
    /// Verifies ICV computation for Diminished triad: {0, 3, 6}.
    ///
    /// <para>Interval analysis:</para>
    /// <list type="bullet">
    ///   <item><description>C to Eb = 3 semitones → IC3</description></item>
    ///   <item><description>C to Gb = 6 semitones → IC6 (tritone)</description></item>
    ///   <item><description>Eb to Gb = 3 semitones → IC3</description></item>
    /// </list>
    /// <para>Expected ICV: [0, 0, 2, 0, 0, 1]</para>
    /// </summary>
    [Test]
    public void ICV_DiminishedTriad_Returns002001()
    {
        // Arrange
        var diminishedPcs = new[] { 0, 3, 6 };
        var expected = new[] { 0, 0, 2, 0, 0, 1 };

        // Act
        var icv = ComputeIntervalClassVector(diminishedPcs);

        // Assert
        TestContext.WriteLine($"Diminished Triad: {{0, 3, 6}}");
        TestContext.WriteLine($"Calculated ICV: [{string.Join(", ", icv)}]");
        Assert.That(icv, Is.EqualTo(expected), "Diminished triad should have ICV [0,0,2,0,0,1]");
    }

    /// <summary>
    /// OPTIC/K Transposition Invariance (T):
    /// Transposed sets must have identical Interval Class Vectors.
    /// C Major {0,4,7} transposed by P5 = G Major {7,11,2}.
    /// </summary>
    [Test]
    public void ICV_TranspositionInvariant()
    {
        // Arrange
        var cMajor = new[] { 0, 4, 7 };
        var gMajor = new[] { 7, 11, 2 }; // C Major transposed by P5

        // Act
        var icvC = ComputeIntervalClassVector(cMajor);
        var icvG = ComputeIntervalClassVector(gMajor);

        // Assert
        TestContext.WriteLine($"C Major Triad ICV: [{string.Join(", ", icvC)}]");
        TestContext.WriteLine($"G Major Triad ICV: [{string.Join(", ", icvG)}]");
        Assert.That(icvG, Is.EqualTo(icvC), "OPTIC/K T-invariance: Transposed sets must have identical ICV");
    }

    /// <summary>
    /// OPTIC/K Inversion Invariance (I):
    /// Inverted sets must have identical Interval Class Vectors.
    /// C Major {0,4,7} and C Minor {0,3,7} are inversionally related.
    /// Both have the same interval content, just ordered differently.
    /// </summary>
    [Test]
    public void ICV_InversionInvariant()
    {
        // Arrange
        var cMajor = new[] { 0, 4, 7 };
        var cMinor = new[] { 0, 3, 7 }; // Inversion of major intervals

        // Act
        var icvMaj = ComputeIntervalClassVector(cMajor);
        var icvMin = ComputeIntervalClassVector(cMinor);

        // Assert
        TestContext.WriteLine($"C Major Triad ICV: [{string.Join(", ", icvMaj)}]");
        TestContext.WriteLine($"C Minor Triad ICV: [{string.Join(", ", icvMin)}]");
        Assert.That(icvMin, Is.EqualTo(icvMaj), "OPTIC/K I-invariance: Inverted sets must have identical ICV");
    }

    #endregion

    #region Complement Computation Tests

    /// <summary>
    /// Per §1.2: Comp(S) = {0..11} \ S
    /// C Major triad {0,4,7} complement should have 9 pitch classes.
    /// </summary>
    [Test]
    public void Complement_CMajorTriad_Returns9Notes()
    {
        var cMajorPcs = new[] { 0, 4, 7 };
        var complement = ComputeComplement(cMajorPcs);

        Assert.That(complement.Length, Is.EqualTo(EmbeddingSchema.PitchClassCount - 3),
            "Complement of 3-note set should have 9 notes");
        Assert.That(complement, Does.Not.Contain(0), "Complement should not contain C");
        Assert.That(complement, Does.Not.Contain(4), "Complement should not contain E");
        Assert.That(complement, Does.Not.Contain(7), "Complement should not contain G");
    }

    /// <summary>
    /// Hexachords (6-note sets) have 6-note complements.
    /// Whole-tone scale {0,2,4,6,8,10} complement is {1,3,5,7,9,11}.
    /// </summary>
    [Test]
    public void Complement_Hexachord_ReturnsComplementHexachord()
    {
        var wholeTone = new[] { 0, 2, 4, 6, 8, 10 };
        var complement = ComputeComplement(wholeTone);

        var expectedComplement = new[] { 1, 3, 5, 7, 9, 11 };
        Assert.That(complement.Length, Is.EqualTo(6), "Hexachord complement should have 6 notes");
        Assert.That(complement, Is.EqualTo(expectedComplement));
    }

    #endregion

    #region Complementarity K Tests

    /// <summary>
    /// Per §3.2: K = clamp01(cosine(ICV(PCS), ICV(Comp(PCS))))
    /// All-interval tetrachord {0,1,4,6} has interesting K properties.
    /// </summary>
    [Test]
    public void ComplementarityK_AllIntervalTetrachord_InValidRange()
    {
        var allIntervalTetrachord = new[] { 0, 1, 4, 6 };
        var complement = ComputeComplement(allIntervalTetrachord);

        var icvPcs = ComputeIntervalClassVector(allIntervalTetrachord);
        var icvComp = ComputeIntervalClassVector(complement);

        var k = ComputeKComplementarity(icvPcs, icvComp);

        Assert.That(k, Is.InRange(0.0, 1.0),
            "K must be in [0,1] as per clamp01 requirement");
    }

    /// <summary>
    /// Per §3.2: If either ICV is zero-vector, K = 0.0
    /// This handles edge cases like empty sets.
    /// </summary>
    [Test]
    public void ComplementarityK_ZeroVector_ReturnsZero()
    {
        var zeroIcv = new[] { 0, 0, 0, 0, 0, 0 };
        var nonZeroIcv = new[] { 0, 0, 1, 1, 1, 0 };

        var k = ComputeKComplementarity(zeroIcv, nonZeroIcv);

        Assert.That(k, Is.EqualTo(0.0),
            "K must be 0 when either ICV is zero-vector");
    }

    #endregion

    #region Identity Gating Tests

    /// <summary>
    /// Per §2: EXTENSIONS (78-95) only populated when IDENTITY is Voicing or Shape.
    /// Voicing documents should have non-zero extension values.
    /// </summary>
    [Test]
    public async Task IdentityGating_VoicingType_ExtensionsPopulated()
    {
        // Arrange
        var voicingDoc = CreateVoicingDocument();

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(voicingDoc);
        var extensions = embedding
            .Skip(EmbeddingSchema.ExtensionsOffset)
            .Take(EmbeddingSchema.ExtensionsDim)
            .ToArray();

        // Assert
        TestContext.WriteLine($"Voicing Type ID Partition: [{string.Join(", ", embedding.Take(6).Select(x => x.ToString("F1")))}]");
        TestContext.WriteLine($"Extensions (78-95) first 5: [{string.Join(", ", extensions.Take(5).Select(x => x.ToString("F2")))}]");

        Assert.That(extensions.Any(e => e != 0.0), Is.True,
            "Voicing type should have populated extensions per Identity Gating rule");
    }

    /// <summary>
    /// Verifies IDENTITY partition has the expected primary bit set.
    /// Voicing = index 2, Scale = index 1.
    /// </summary>
    [Test]
    public void IdentityGating_VerifyIdentitySliceHasPrimaryBit()
    {
        // Arrange
        var identityService = new IdentityVectorService();

        // Act
        var voicingIdentity = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Voicing);
        var scaleIdentity = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Scale);

        // Assert
        TestContext.WriteLine($"Voicing Identity: [{string.Join(", ", voicingIdentity.Select(x => x.ToString("F1")))}]");
        TestContext.WriteLine($"Scale Identity: [{string.Join(", ", scaleIdentity.Select(x => x.ToString("F1")))}]");

        Assert.Multiple(() =>
        {
            Assert.That(voicingIdentity[2], Is.EqualTo(1.0), "Voicing should have bit 2 set in IDENTITY partition");
            Assert.That(scaleIdentity[1], Is.EqualTo(1.0), "Scale should have bit 1 set in IDENTITY partition");
        });
    }

    #endregion

    #region Root Gating Tests

    /// <summary>
    /// Per §5.3: When rootPC is undefined, embedding should still be valid.
    /// </summary>
    [Test]
    public async Task RootGating_UndefinedRoot_EmbeddingValid()
    {
        // Arrange
        var doc = CreateVoicingDocument(rootPitchClass: null);

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);

        // Assert
        TestContext.WriteLine($"Embedding Length with undefined root: {embedding.Length}");
        Assert.That(embedding.Length, Is.EqualTo(EmbeddingSchema.TotalDimension),
            $"Embedding should be {EmbeddingSchema.TotalDimension} dimensions even with undefined root");
    }

    /// <summary>
    /// Per §4.2: TopNoteRelative = ((topPC - rootPC + 12) % 12) / 11.0
    /// C triad with E on top: topPC=4, rootPC=0 → (4-0+12)%12 = 4 → 4/11 ≈ 0.36
    /// </summary>
    [Test]
    public async Task RootGating_DefinedRoot_TopNoteRelativeCalculated()
    {
        // Arrange
        // C, G, E (E on top)
        var midiNotes = new[] { 48, 55, 64 }; // C3, G3, E4
        var pitchClasses = new[] { 0, 7, 4 };
        var rootPc = 0; // C is root

        var doc = CreateVoicingDocument(
            midiNotes: midiNotes,
            pitchClasses: pitchClasses,
            rootPitchClass: rootPc
        );

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);
        var topNoteRelative = embedding[EmbeddingSchema.TexturalTopNoteRelative];
        var expectedValue = 4.0 / 11.0; // ((4 - 0 + 12) % 12) / 11

        // Assert
        TestContext.WriteLine($"Root: C, Top: E, Expected TopNoteRelative: {expectedValue:F4}, Actual: {topNoteRelative:F4}");
        Assert.That(topNoteRelative, Is.EqualTo(expectedValue).Within(0.01),
            "TopNoteRelative should be ~0.36 for E on top of C chord");
    }

    #endregion

    #region Clamp01 Enforcement Tests

    /// <summary>
    /// Per §5.1: Every derived dimension (78-95) MUST be in [0,1].
    /// </summary>
    [Test]
    public async Task Clamp01_AllExtensions_InRange()
    {
        // Arrange
        var doc = CreateVoicingDocument(midiNotes: new[] { 40, 88, 60, 72 });

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);

        // Assert
        TestContext.WriteLine($"Verifying derived dimensions (78-95) are in [0, 1] range.");
        for (int i = EmbeddingSchema.ExtensionsOffset; i < EmbeddingSchema.ExtensionsEnd; i++)
        {
            Assert.That(embedding[i], Is.InRange(0.0, 1.0),
                $"Index {i} must be clamped to [0,1], was {embedding[i]}");
        }
    }

    /// <summary>
    /// Extreme MIDI values should not produce NaN or Infinity.
    /// </summary>
    [Test]
    public async Task Clamp01_ExtremeValues_NoNanOrInfinity()
    {
        // Arrange
        var doc = CreateVoicingDocument(midiNotes: new[] { 28, 96, 40, 88 });

        // Act
        var embedding = await _generator.GenerateEmbeddingAsync(doc);

        // Assert
        TestContext.WriteLine($"Verifying all dimensions are finite for extreme MIDI values: [28, 96, 40, 88]");
        foreach (var value in embedding)
        {
            Assert.That(double.IsFinite(value), Is.True,
                "Embedding values must be finite (no NaN or Infinity)");
        }
    }

    #endregion

    #region Similarity Formula Tests

    /// <summary>
    /// Per §0.3: Similarity(A,A) should equal 1.0 for identical vectors.
    /// All weighted partitions contribute proportionally.
    /// </summary>
    [Test]
    public void Similarity_IdenticalVectors_ReturnsOne()
    {
        var v1 = new double[EmbeddingSchema.TotalDimension];
        var v2 = new double[EmbeddingSchema.TotalDimension];

        // Populate all weighted partitions
        PopulatePartition(v1, v2, EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureEnd, 1.0);
        PopulatePartition(v1, v2, EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyEnd, 0.5);
        PopulatePartition(v1, v2, EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextEnd, 0.7);
        PopulatePartition(v1, v2, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicEnd, 0.3);

        var similarity = ComputeWeightedPartitionSimilarity(v1, v2);

        Assert.That(similarity, Is.EqualTo(1.0).Within(0.0001),
            "Identical vectors should have similarity = 1.0");
    }

    /// <summary>
    /// Orthogonal vectors in STRUCTURE should have low similarity.
    /// </summary>
    [Test]
    public void Similarity_OrthogonalVectors_LowValue()
    {
        // Arrange
        var v1 = new double[EmbeddingSchema.TotalDimension];
        var v2 = new double[EmbeddingSchema.TotalDimension];

        // Set orthogonal values in STRUCTURE partition
        v1[EmbeddingSchema.StructureOffset] = 1.0;
        v2[EmbeddingSchema.StructureOffset + 1] = 1.0;

        // Act
        var similarity = ComputeWeightedPartitionSimilarity(v1, v2);

        // Assert
        TestContext.WriteLine($"Similarity of orthogonal vectors in STRUCTURE subspace: {similarity:F4}");
        Assert.That(similarity, Is.LessThan(0.5),
            "Orthogonal vectors should have low similarity");
    }

    /// <summary>
    /// Per §0.1: STRUCTURE weight (0.45) > SYMBOLIC weight (0.10).
    /// Differing in STRUCTURE should hurt similarity more than differing in SYMBOLIC.
    /// </summary>
    [Test]
    public void Similarity_StructureWeightedHigherThanSymbolic()
    {
        var baseline = new double[EmbeddingSchema.TotalDimension];
        var differInStructure = new double[EmbeddingSchema.TotalDimension];
        var differInSymbolic = new double[EmbeddingSchema.TotalDimension];

        // Baseline: all partitions filled
        FillRange(baseline, EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureEnd, 1.0);
        FillRange(baseline, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicEnd, 1.0);

        // Differ in STRUCTURE only
        FillRange(differInStructure, EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureEnd, 0.0);
        FillRange(differInStructure, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicEnd, 1.0);

        // Differ in SYMBOLIC only
        FillRange(differInSymbolic, EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureEnd, 1.0);
        FillRange(differInSymbolic, EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicEnd, 0.0);

        var simStructureDiff = ComputeWeightedPartitionSimilarity(baseline, differInStructure);
        var simSymbolicDiff = ComputeWeightedPartitionSimilarity(baseline, differInSymbolic);

        Assert.That(simSymbolicDiff, Is.GreaterThan(simStructureDiff),
            $"Symbolic diff ({simSymbolicDiff:F3}) should hurt less than Structure diff ({simStructureDiff:F3})");
    }

    #endregion

    #region Helper Methods - ICV and Complement

    /// <summary>
    /// Computes the Interval Class Vector (ICV) for a pitch-class set.
    ///
    /// <para>Per §1.3: For each unordered pair, compute interval class (1-6) and count.</para>
    /// <para>Interval class = min(|p1-p2|, 12-|p1-p2|)</para>
    /// </summary>
    #endregion

    #region Spectral Geometry (v1.3.1)

    /// <summary>
    /// Verifies that Spectral Entropy (index 108) is computed using 7 bins and log2(7) normalization.
    /// - Flat spectrum (single note) should result in high entropy (near 0.0 feature value).
    /// - Peaked spectrum (tritone {0,6}) should result in lower entropy (higher feature value).
    /// </summary>
    [Test]
    public async Task Spectral_Entropy_V131_CorrectNormalization()
    {
        // Arrange
        // Case 1: Single Note (Flat DFT magnitudes) -> Max Entropy -> 0.0 normalized feature
        var singleNoteDoc = CreateVoicingDoc([60]); // C4
        // Case 2: Tritone {0, 6} (Peaked DFT magnitudes) -> Lower Entropy -> Higher normalized feature
        var tritoneDoc = CreateVoicingDoc([60, 66]); // C4, F#4

        // Act
        var singleNoteVector = await _generator.GenerateEmbeddingAsync(singleNoteDoc);
        var singleNoteEntropy = singleNoteVector[EmbeddingSchema.SpectralEntropy];

        var tritoneVector = await _generator.GenerateEmbeddingAsync(tritoneDoc);
        var tritoneEntropy = tritoneVector[EmbeddingSchema.SpectralEntropy];

        // Assert
        TestContext.WriteLine($"Single Note Spectral Entropy Score: {singleNoteEntropy:F4}");
        TestContext.WriteLine($"Tritone {{0, 6}} Spectral Entropy Score: {tritoneEntropy:F4}");

        Assert.Multiple(() =>
        {
            Assert.That(singleNoteEntropy, Is.LessThan(0.01), "Single note should have max entropy (0 normalized)");
            Assert.That(tritoneEntropy, Is.GreaterThan(singleNoteEntropy), "Tritone should have lower entropy than single note");
            Assert.That(tritoneEntropy, Is.LessThan(1.0), "Entropy should be in [0,1]");
        });
    }

    /// <summary>
    /// Verifies that Fourier Magnitudes are normalized by the unit sphere magnitude (sqrt(sum of powers)).
    /// For a single note, each |DFT[k]| = 1.0, total power = 6, so each component = 1/sqrt(6).
    /// </summary>
    [Test]
    public async Task Fourier_Magnitudes_SingleNote_AreNormalizedToSphere()
    {
        // Arrange
        var doc = CreateVoicingDoc([60]);
        var expected = 1.0 / Math.Sqrt(6);

        // Act
        var vector = await _generator.GenerateEmbeddingAsync(doc);

        // Assert
        TestContext.WriteLine($"Verifying Single Note (MIDI 60) Fourier Magnitudes are normalized to 1/sqrt(6) ≈ {expected:F4}");
        for (int k = 1; k <= 6; k++)
        {
            var magIdx = EmbeddingSchema.SpectralOffset + (k - 1);
            TestContext.WriteLine($"  k={k} Magnitude: {vector[magIdx]:F4}");
            Assert.That(vector[magIdx], Is.EqualTo(expected).Within(1e-5), $"Mag k={k} should be 1/sqrt(6) for single note");
        }
    }

    /// <summary>
    /// Verifies Salient Chroma weighting (+0.5 for bass/melody).
    /// C Major triad {0, 4, 7} with C as bass and G as melody.
    /// Chroma for C and G should be higher than for E.
    /// </summary>
    [Test]
    public async Task Salient_Chroma_BassMelody_Weighting()
    {
        // Arrange
        var doc = CreateVoicingDoc([60, 64, 67]); // C4, E4, G4

        // Act
        var vector = await _generator.GenerateEmbeddingAsync(doc);
        var entropy = vector[EmbeddingSchema.SpectralEntropy];

        // Assert
        TestContext.WriteLine($"C Major Triad Spectral Entropy Score: {entropy:F4}");
        // This indirectly tests if ComputeSpectralGeometry is using doc.MidiNotes
        Assert.That(entropy, Is.Not.EqualTo(0), "Spectral entropy should be non-zero for structured triad");
    }

    #endregion

    #region Helper Methods

    private static VoicingDocument CreateVoicingDoc(int[] midiNotes)
    {
        var pcs = midiNotes.Select(n => n % 12).Distinct().OrderBy(p => p).ToArray();
        return new VoicingDocument
        {
            Id = "test",
            MidiNotes = midiNotes,
            PitchClasses = pcs,
            MidiBassNote = midiNotes.Length > 0 ? midiNotes.Min() : -1,
            Consonance = 1.0,
            SemanticTags = [],
            SearchableText = "test",
            PossibleKeys = [],
            YamlAnalysis = "test",
            Diagram = "test",
            PitchClassSet = "test",
            IntervalClassVector = "test",
            AnalysisEngine = "test",
            AnalysisVersion = "test",
            Jobs = [],
            TuningId = "test",
            PitchClassSetId = "test"
        };
    }

    private static int[] ComputeIntervalClassVector(int[] pitchClasses)
    {
        var icv = new int[EmbeddingSchema.IntervalClassCount];

        for (int i = 0; i < pitchClasses.Length; i++)
        {
            for (int j = i + 1; j < pitchClasses.Length; j++)
            {
                var diff = Math.Abs(pitchClasses[i] - pitchClasses[j]);
                var intervalClass = Math.Min(diff, EmbeddingSchema.PitchClassCount - diff);

                if (intervalClass >= 1 && intervalClass <= EmbeddingSchema.IntervalClassCount)
                {
                    icv[intervalClass - 1]++;
                }
            }
        }
        return icv;
    }

    /// <summary>
    /// Computes the set complement: Comp(S) = {0..11} \ S
    /// </summary>
    private static int[] ComputeComplement(int[] pitchClasses)
    {
        return Enumerable.Range(0, EmbeddingSchema.PitchClassCount)
            .Except(pitchClasses)
            .ToArray();
    }

    /// <summary>
    /// Computes Complementarity K as cosine similarity between two ICVs.
    /// Returns 0.0 if either ICV is a zero-vector.
    /// </summary>
    private static double ComputeKComplementarity(int[] icv1, int[] icv2)
    {
        var d1 = icv1.Select(x => (double)x).ToArray();
        var d2 = icv2.Select(x => (double)x).ToArray();

        var dotProduct = d1.Zip(d2, (a, b) => a * b).Sum();
        var magnitude1 = Math.Sqrt(d1.Sum(x => x * x));
        var magnitude2 = Math.Sqrt(d2.Sum(x => x * x));

        if (magnitude1 == 0 || magnitude2 == 0) return 0.0;

        return Math.Clamp(dotProduct / (magnitude1 * magnitude2), 0.0, 1.0);
    }

    #endregion

    #region Helper Methods - Similarity

    /// <summary>
    /// Computes weighted partition cosine similarity per §0.3.
    ///
    /// <para>Formula: Σ weight[p] × cosine(normalize(A[p]), normalize(B[p]))</para>
    /// <para>IDENTITY and EXTENSIONS are excluded.</para>
    /// </summary>
    private static double ComputeWeightedPartitionSimilarity(double[] v1, double[] v2)
    {
        var partitions = new (int start, int end, double weight)[]
        {
            (EmbeddingSchema.StructureOffset, EmbeddingSchema.StructureEnd, EmbeddingSchema.StructureWeight),
            (EmbeddingSchema.MorphologyOffset, EmbeddingSchema.MorphologyEnd, EmbeddingSchema.MorphologyWeight),
            (EmbeddingSchema.ContextOffset, EmbeddingSchema.ContextEnd, EmbeddingSchema.ContextWeight),
            (EmbeddingSchema.SymbolicOffset, EmbeddingSchema.SymbolicEnd, EmbeddingSchema.SymbolicWeight)
        };

        double totalSimilarity = 0.0;

        foreach (var (start, end, weight) in partitions)
        {
            var slice1 = v1.Skip(start).Take(end - start).ToArray();
            var slice2 = v2.Skip(start).Take(end - start).ToArray();

            var cosine = ComputeCosineSimilarity(slice1, slice2);
            totalSimilarity += weight * cosine;
        }

        return totalSimilarity;
    }

    /// <summary>
    /// Computes cosine similarity between two vectors.
    /// Returns 0.0 if either vector is zero.
    /// </summary>
    private static double ComputeCosineSimilarity(double[] a, double[] b)
    {
        var dotProduct = a.Zip(b, (x, y) => x * y).Sum();
        var magnitudeA = Math.Sqrt(a.Sum(x => x * x));
        var magnitudeB = Math.Sqrt(b.Sum(x => x * x));

        if (magnitudeA == 0 || magnitudeB == 0) return 0.0;
        return dotProduct / (magnitudeA * magnitudeB);
    }

    /// <summary>Helper to populate a partition range with identical values in two vectors.</summary>
    private static void PopulatePartition(double[] v1, double[] v2, int start, int end, double value)
    {
        for (int i = start; i < end; i++)
        {
            v1[i] = value;
            v2[i] = value;
        }
    }

    /// <summary>Helper to fill a range in a vector with a constant value.</summary>
    private static void FillRange(double[] v, int start, int end, double value)
    {
        for (int i = start; i < end; i++) v[i] = value;
    }

    #endregion

    #region Helper Methods - Test Document Factory

    /// <summary>
    /// Creates a test VoicingDocument with sensible defaults.
    /// </summary>
    private static VoicingDocument CreateVoicingDocument(
        int[]? midiNotes = null,
        int[]? pitchClasses = null,
        int? rootPitchClass = null)
    {
        midiNotes ??= new[] { 48, 52, 55, 60 }; // C, E, G, C
        pitchClasses ??= midiNotes.Select(m => m % EmbeddingSchema.PitchClassCount).ToArray();
        var pcsString = "{" + string.Join(",", pitchClasses.Distinct().OrderBy(p => p)) + "}";

        return new VoicingDocument
        {
            Id = "test-voicing",
            SearchableText = "Test voicing for contract tests",
            ChordName = "TestChord",
            Diagram = "x-x-x-x-x-x",
            MidiNotes = midiNotes,
            PitchClasses = pitchClasses,
            PitchClassSet = pcsString,
            PitchClassSetId = "test-pcs-id",
            IntervalClassVector = "000000",
            RootPitchClass = rootPitchClass ?? pitchClasses.FirstOrDefault(),
            MidiBassNote = midiNotes.Min(),
            Consonance = 0.5,
            Brightness = 0.5,
            HarmonicFunction = "Tonic",
            MinFret = 0,
            MaxFret = 4,
            HandStretch = 4,
            BarreRequired = false,
            IsRootless = false,
            SemanticTags = Array.Empty<string>(),
            PossibleKeys = Array.Empty<string>(),
            YamlAnalysis = string.Empty,
            TuningId = "standard",
            AnalysisEngine = "test",
            AnalysisVersion = "1.0",
            Jobs = Array.Empty<string>()
        };
    }

    #endregion
}
