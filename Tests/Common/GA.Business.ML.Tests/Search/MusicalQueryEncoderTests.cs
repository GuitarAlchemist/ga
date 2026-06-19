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
    public void SetUp() => _encoder = new MusicalQueryEncoder(
            new TheoryVectorService(),
            new ModalVectorService(),
            new SymbolicVectorService(),
            new RootVectorService());

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

    [TestCase("E7(#9)",  4, new[] { 4, 8, 11, 2, 7 })]    // Hendrix chord: E G# B D + #9=F##(=G)
    [TestCase("C7(b9)",  0, new[] { 0, 4, 7, 10, 1 })]    // C7b9: C E G Bb + b9=Db
    [TestCase("G7(b5)",  7, new[] { 7, 11, 1, 5 })]       // G7b5: G B Db F
    [TestCase("G7(#5)",  7, new[] { 7, 11, 3, 5 })]       // G7#5: G B D# F
    [TestCase("C(add9)", 0, new[] { 0, 4, 7, 2 })]        // Cadd9: C E G + 9=D
    public void TryParse_ParenWrappedAlterations_FallBackToDeparenthesizedQuality(
        string symbol, int expectedRoot, int[] expectedPcs)
    {
        // 2026-05-12 review: removing '(' and ')' from TypedMusicalQueryExtractor's
        // tokenizer (companion fix) makes these queries arrive as single tokens
        // like "E7(#9)" instead of split into ["E7", "#9"]. Without the
        // paren-strip retry in TryParse, the quality string "7(#9)" wouldn't
        // match the dictionary key "7#9" and the chord would be dropped entirely
        // — a worse outcome than the pre-fix partial parse.
        var ok = ChordPitchClasses.TryParse(symbol, out var root, out var pcs);
        Assert.That(ok, Is.True, $"paren-stripped retry should accept {symbol}");
        Assert.That(root, Is.EqualTo(expectedRoot));
        Assert.That(pcs, Is.EquivalentTo(expectedPcs));
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

    // ─── ICV reconciliation (writer/reader symmetry, 2026-05-12 plan) ──────

    [TestCase(new[] { 0, 4, 7 },               "001110", TestName = "MajorTriad_ICV_001110")]
    [TestCase(new[] { 0, 3, 7 },               "001110", TestName = "MinorTriad_ICV_001110")]
    [TestCase(new[] { 0, 4, 7, 11 },           "101220", TestName = "Maj7_ICV_101220")]   // Cmaj7
    [TestCase(new[] { 0, 2, 5, 9 },            "012120", TestName = "Min7_ICV_012120")]   // Dm7 = D F A C → IC5 hits twice ((0,5) + (2,9))
    [TestCase(new[] { 0, 3, 6, 9 },            "004002", TestName = "Dim7_ICV_004002")]   // fully diminished
    [TestCase(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 }, "999996", TestName = "Chromatic12_ICV_capped_at_9_except_IC6")]   // raw [12,12,12,12,12,6] clamped at 9 → "999996"
    public void ComputeIcvString_KnownPcSets_ProducesExpectedIcv(int[] pcs, string expectedIcv)
    {
        var icv = MusicalQueryEncoder.ComputeIcvString(pcs);

        Assert.That(icv, Has.Length.EqualTo(6),
            "ICV string must be exactly 6 digits — one per IC bin.");
        Assert.That(icv, Is.EqualTo(expectedIcv),
            $"PC-set {{{string.Join(",", pcs)}}} → expected ICV {expectedIcv}, got {icv}.");
    }

    [Test]
    public void ComputeIcvString_EmptyAndSinglePc_ReturnsAllZeros()
    {
        Assert.That(MusicalQueryEncoder.ComputeIcvString([]),  Is.EqualTo("000000"));
        Assert.That(MusicalQueryEncoder.ComputeIcvString([5]), Is.EqualTo("000000"));
    }

    [Test]
    public void ComputeIcvString_DuplicatePcs_AreDeduplicatedBeforeCounting()
    {
        // Duplicates would otherwise double-count intervals — Cmaj written twice as
        // [0, 0, 4, 4, 7, 7] must still produce the major-triad ICV "001110".
        var icv = MusicalQueryEncoder.ComputeIcvString([0, 0, 4, 4, 7, 7]);
        Assert.That(icv, Is.EqualTo("001110"));
    }

    [TestCase("001110",      new[] { 0, 0, 1, 1, 1, 0 }, TestName = "ParseIcv_DigitPerPosition_MajorTriad")]
    [TestCase("101220",      new[] { 1, 0, 1, 2, 2, 0 }, TestName = "ParseIcv_DigitPerPosition_Maj7")]
    [TestCase("<0 0 1 1 1 0>", new[] { 0, 0, 1, 1, 1, 0 }, TestName = "ParseIcv_BracketSpace_MajorTriad")]
    [TestCase("<2 5 4 3 6 1>", new[] { 2, 5, 4, 3, 6, 1 }, TestName = "ParseIcv_BracketSpace_MajorScale")]
    [TestCase("<10 5 4 3 6 1>", new[] { 9, 5, 4, 3, 6, 1 }, TestName = "ParseIcv_BracketSpace_ClampsTo9")]
    public void ParseIcv_AcceptsBothFormats(string input, int[] expected)
    {
        var actual = TheoryVectorService.ParseIcv(input);
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ParseIcv_NullOrEmpty_ReturnsAllZeros()
    {
        Assert.That(TheoryVectorService.ParseIcv(null),       Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0 }));
        Assert.That(TheoryVectorService.ParseIcv(string.Empty), Is.EqualTo(new[] { 0, 0, 0, 0, 0, 0 }));
    }

    [TestCase(new[] { 0, 4, 7 },     "<0 0 1 1 1 0>", TestName = "RoundTrip_MajorTriad")]
    [TestCase(new[] { 0, 4, 7, 11 }, "<1 0 1 2 2 0>", TestName = "RoundTrip_Maj7")]
    [TestCase(new[] { 0, 2, 5, 9 },  "<0 1 2 1 2 0>", TestName = "RoundTrip_Min7")]
    public void RoundTrip_QueryAndCorpusFormat_ProduceSameCounts(int[] pcs, string corpusBracketForm)
    {
        // Symmetry guarantee: query-side ComputeIcvString → ParseIcv must produce
        // the same int[6] as corpus-side IntervalClassVectorId.ToString() → ParseIcv.
        // This is the contract the encoder docstring claims and what the corpus rebuild
        // unlocks. Pre-fix the corpus side parsed the bracket form character-by-character
        // and silently produced positionally-shifted garbage; pre-fix the query side
        // wrote zeros. Post-fix both paths land on the same int[6].
        var queryString  = MusicalQueryEncoder.ComputeIcvString(pcs);
        var queryCounts  = TheoryVectorService.ParseIcv(queryString);
        var corpusCounts = TheoryVectorService.ParseIcv(corpusBracketForm);

        Assert.That(queryCounts, Is.EqualTo(corpusCounts),
            $"Query ICV string {queryString} and corpus bracket form {corpusBracketForm} " +
            "must parse to identical IC counts. If this drifts, the writer/reader symmetry " +
            "regresses and 4+PC chord queries will misrank against indexed subsets.");
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
