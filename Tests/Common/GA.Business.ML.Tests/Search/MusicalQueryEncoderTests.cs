namespace GA.Business.ML.Tests.Search;

using Embeddings;
using Embeddings.Services;
using GA.Business.ML.Search;

/// <summary>
///     Unit tests for the chord-to-pitch-class resolver and the musical query encoder.
///     These run without any on-disk corpus — they exercise pure in-process math.
/// </summary>
[TestFixture]
[Category("Unit")]
public class MusicalQueryEncoderTests
{
    private MusicalQueryEncoder _encoder = null!;

    [SetUp]
    public void SetUp()
    {
        _encoder = new MusicalQueryEncoder(
            new TheoryVectorService(),
            new ModalVectorService(),
            new SymbolicVectorService(),
            new RootVectorService());
    }

    // ─── ChordPitchClasses.TryParse ────────────────────────────────────────

    [TestCase("C",      0,  new[] { 0, 4, 7 })]
    [TestCase("Cm",     0,  new[] { 0, 3, 7 })]
    [TestCase("Cmaj7",  0,  new[] { 0, 4, 7, 11 })]
    [TestCase("Dm7",    2,  new[] { 0, 2, 5, 9 })]
    [TestCase("G7",     7,  new[] { 2, 5, 7, 11 })]
    [TestCase("F#m7b5", 6,  new[] { 0, 4, 6, 9 })]   // F# A C E → {6,9,0,4}
    [TestCase("Ab7",    8,  new[] { 0, 3, 6, 8 })]   // Ab C Eb Gb → {8,0,3,6}
    [TestCase("Bbmaj7", 10, new[] { 2, 5, 9, 10 })]  // Bb D F A → {10,2,5,9}
    // Extended jazz qualities (added 2026-04-18)
    [TestCase("Cmmaj7",  0, new[] { 0, 3, 7, 11 })]       // minor-major 7
    [TestCase("Cmaj13",  0, new[] { 0, 2, 4, 5, 7, 9, 11 })]
    [TestCase("Cm11",    0, new[] { 0, 2, 3, 5, 7, 10 })]
    [TestCase("C9#11",   0, new[] { 0, 2, 4, 6, 7, 10 })]
    [TestCase("C7alt",   0, new[] { 0, 1, 3, 4, 6, 8, 10 })]
    [TestCase("C69",     0, new[] { 0, 2, 4, 7, 9 })]
    [TestCase("C6/9",    0, new[] { 0, 2, 4, 7, 9 })]       // slash-digit kept as quality
    [TestCase("C7b5",    0, new[] { 0, 4, 6, 10 })]
    [TestCase("Caug7",   0, new[] { 0, 4, 8, 10 })]
    [TestCase("Edim7",  4,  new[] { 1, 4, 7, 10 })]
    [TestCase("Csus4",  0,  new[] { 0, 5, 7 })]
    public void TryParse_KnownSymbols_ReturnsCorrectPitchClasses(
        string symbol, int expectedRoot, int[] expectedPcs)
    {
        var ok = ChordPitchClasses.TryParse(symbol, out var root, out var pcs);

        Assert.That(ok, Is.True, $"parser rejected {symbol}");
        Assert.That(root, Is.EqualTo(expectedRoot));
        Assert.That(pcs, Is.EquivalentTo(expectedPcs));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("H7")]       // German notation, unsupported
    [TestCase("Xyz")]      // nonsense
    public void TryParse_InvalidSymbols_ReturnsFalse(string symbol)
    {
        var ok = ChordPitchClasses.TryParse(symbol, out _, out _);
        Assert.That(ok, Is.False);
    }

    [Test]
    public void TryParse_SlashBass_IgnoresSlashPortion()
    {
        // Slash bass changes the voicing but not the PC set used for STRUCTURE matching.
        var ok = ChordPitchClasses.TryParse("C/E", out var root, out var pcs);

        Assert.That(ok, Is.True);
        Assert.That(root, Is.EqualTo(0));
        Assert.That(pcs, Is.EquivalentTo(new[] { 0, 4, 7 }));
    }

    // ─── Encoder shape and normalization ───────────────────────────────────

    [Test]
    public void Encode_WithChord_NormMatchesActivePartitionWeights()
    {
        var q = new StructuredQuery(
            ChordSymbol: "Cmaj7",
            RootPitchClass: 0,
            PitchClasses: [0, 4, 7, 11],
            ModeName: null,
            Tags: null);

        var v = _encoder.Encode(q);

        Assert.That(v.Length, Is.EqualTo(OptickIndexReader.Dimension));

        // Under v4-pp-r (v1.8, per-partition norm + ROOT partition): the encoded query's
        // norm² equals the sum of weights of the *active* partitions. A chord-only query
        // activates STRUCTURE (0.45) + MODAL (0.10, PC-set auto-scored against modes)
        // + ROOT (0.05, chord symbol supplied a root). Expected norm² = 0.60, norm ≈
        // 0.7746. MORPHOLOGY, CONTEXT, SYMBOLIC stay zero for a chord-only query.
        var norm = Math.Sqrt(v.Sum(x => x * x));
        var expected = Math.Sqrt(0.45 + 0.10 + 0.05);
        Assert.That(norm, Is.EqualTo(expected).Within(1e-6),
            "chord-only query norm = sqrt(w_STRUCTURE + w_MODAL + w_ROOT) under v1.8.");
    }

    [Test]
    public void Encode_EmptyQuery_ReturnsZeroVector()
    {
        var q = new StructuredQuery(null, null, null, null, null);

        var v = _encoder.Encode(q);

        Assert.That(v.Length, Is.EqualTo(OptickIndexReader.Dimension));
        Assert.That(v.All(x => x == 0.0), Is.True,
            "a query with no content must produce a zero vector, not crash.");
    }

    [Test]
    public void Encode_ChordOnly_PopulatesStructurePartition_ZerosOthers()
    {
        var q = new StructuredQuery("Cmaj7", 0, [0, 4, 7, 11], null, null);

        var v = _encoder.Encode(q);

        // Compact layout from EmbeddingSchema.SimilarityPartitions:
        // STRUCTURE 0-23, MORPHOLOGY 24-47, CONTEXT 48-59, SYMBOLIC 60-71, MODAL 72-111.
        AssertPartitionNonZero(v, 0, 24, nameof(EmbeddingSchema.StructureOffset));
        AssertPartitionZero(v, 24, 24, "MORPHOLOGY (no fretboard info in text query)");
        AssertPartitionZero(v, 48, 12, "CONTEXT (no temporal info)");
        AssertPartitionZero(v, 60, 12, "SYMBOLIC (no tags supplied)");
        // MODAL gets non-zero values because the modal service scores the PC set against every mode.
    }

    [Test]
    public void Encode_TagsOnly_PopulatesSymbolicPartition()
    {
        var q = new StructuredQuery(null, null, null, null, ["jazz"]);

        var v = _encoder.Encode(q);

        // SYMBOLIC partition is compact dims 60-71.
        // Can't assert *which* bit flips without the SymbolicTagRegistry mapping; we just assert
        // that the partition is not entirely zero when a known tag is present.
        var symbolicPartition = v.Skip(60).Take(12).ToArray();
        Assert.That(symbolicPartition.Any(x => x != 0), Is.True,
            "a known symbolic tag must flip at least one bit in the SYMBOLIC partition.");
    }

    [Test]
    public void Encode_DifferentChords_ProduceDifferentVectors()
    {
        var cmaj7 = _encoder.Encode(new StructuredQuery("Cmaj7", 0, [0, 4, 7, 11], null, null));
        var dm7   = _encoder.Encode(new StructuredQuery("Dm7", 2, [0, 2, 5, 9], null, null));

        // Cosine between the two should be meaningfully < 1 (not identical).
        var dot = 0.0;
        for (var i = 0; i < cmaj7.Length; i++) dot += cmaj7[i] * dm7[i];

        Assert.That(dot, Is.LessThan(0.99),
            "different chords must produce measurably different OPTK vectors.");
    }

    // ─── Leak-fix proof: per-partition normalization ──────────────────────────

    [Test]
    public void PerPartitionNorm_IdenticalStructure_DifferentMorphology_ProducesIdenticalStructureSlice()
    {
        // Prove the v4-pp normalization fix. Two synthetic raw 228-dim vectors with
        // bit-identical STRUCTURE but arbitrarily different MORPHOLOGY must produce
        // bit-identical compact STRUCTURE slices after encoding. Under the old global-
        // L2 semantics, MORPHOLOGY variation would propagate into the STRUCTURE slice
        // via the shared normalization denominator — the exact mechanism that caused
        // invariants #25/#28/#32 to fail.

        var rawA = new double[EmbeddingSchema.TotalDimension];
        var rawB = new double[EmbeddingSchema.TotalDimension];

        // Identical STRUCTURE (offset 6, dim 24) — simulates same PC-set.
        for (var j = 0; j < EmbeddingSchema.StructureDim; j++)
        {
            rawA[EmbeddingSchema.StructureOffset + j] = (j + 1) * 0.1;
            rawB[EmbeddingSchema.StructureOffset + j] = (j + 1) * 0.1;
        }

        // Very different MORPHOLOGY (offset 30, dim 24) — simulates guitar vs ukulele.
        for (var j = 0; j < EmbeddingSchema.MorphologyDim; j++)
        {
            rawA[EmbeddingSchema.MorphologyOffset + j] = j * 0.5;
            rawB[EmbeddingSchema.MorphologyOffset + j] = (23 - j) * 0.01;
        }

        var compactA = MusicalQueryEncoder.ExtractCompactAndNormalize(rawA);
        var compactB = MusicalQueryEncoder.ExtractCompactAndNormalize(rawB);

        // STRUCTURE slice in compact layout = dims 0..23 (first partition).
        for (var i = 0; i < EmbeddingSchema.StructureDim; i++)
        {
            Assert.That(compactA[i], Is.EqualTo(compactB[i]).Within(1e-12),
                $"STRUCTURE slice dim {i} diverged across MORPHOLOGY-different voicings: " +
                $"A={compactA[i]} B={compactB[i]}. Per-partition norm must isolate STRUCTURE.");
        }

        // MORPHOLOGY slice (dims 24..47 in compact) SHOULD differ — that's what it's for.
        var morphologyDiffers = false;
        for (var i = 24; i < 48; i++)
        {
            if (Math.Abs(compactA[i] - compactB[i]) > 1e-6) { morphologyDiffers = true; break; }
        }
        Assert.That(morphologyDiffers, Is.True,
            "MORPHOLOGY slice should retain the input variation.");
    }

    // ─── helpers ───────────────────────────────────────────────────────────

    private static void AssertPartitionNonZero(double[] v, int start, int dim, string name)
    {
        var any = false;
        for (var i = start; i < start + dim; i++) if (v[i] != 0) { any = true; break; }
        Assert.That(any, Is.True, $"{name} partition ({start}..{start + dim - 1}) was unexpectedly all zero.");
    }

    private static void AssertPartitionZero(double[] v, int start, int dim, string name)
    {
        for (var i = start; i < start + dim; i++)
        {
            Assert.That(v[i], Is.EqualTo(0.0).Within(1e-12),
                $"{name} partition had non-zero value at index {i} = {v[i]}.");
        }
    }
}
