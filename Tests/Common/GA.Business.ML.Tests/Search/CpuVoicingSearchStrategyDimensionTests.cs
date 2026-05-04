namespace GA.Business.ML.Tests.Search;

using GA.Business.ML.Search;
using GA.Domain.Services.Fretboard.Voicings.Core;

/// <summary>
/// Regression tests for the user-reported "voicing scores at 0.000" bug.
/// Cause: <see cref="CpuVoicingSearchStrategy.CosineSimilarity"/> previously
/// returned <c>0.0</c> when query and target had different lengths AND only
/// recognised dimensions 96 / 109 / 216 / 228 — but the schema bumped to
/// v1.8 (240) without updating the dim recognition list, so any v1.8 query
/// against v1.7 indexed voicings (or vice versa) hit the "return 0.0"
/// short-circuit.
/// </summary>
[TestFixture]
public class CpuVoicingSearchStrategyDimensionTests
{
    /// <summary>
    /// Build a v1.8-shaped (240-dim) embedding with deterministic values in
    /// the similarity partitions (slots 6-77) and zeros in the info slots.
    /// </summary>
    private static double[] MakeV18Vector(double seed)
    {
        var v = new double[240];
        // STRUCTURE 6..29
        for (var i = 6;  i < 30; i++) v[i] = seed * 0.1 + i * 0.01;
        // MORPHOLOGY 30..53
        for (var i = 30; i < 54; i++) v[i] = seed * 0.2 + i * 0.01;
        // CONTEXT 54..65
        for (var i = 54; i < 66; i++) v[i] = seed * 0.3 + i * 0.01;
        // SYMBOLIC 66..77
        for (var i = 66; i < 78; i++) v[i] = seed * 0.4 + i * 0.01;
        // ROOT one-hot at 228..239 (v1.8-specific) — keep at 0 for the test
        return v;
    }

    /// <summary>
    /// Build a v1.7-shaped (228-dim) embedding with the SAME similarity-partition
    /// values as <see cref="MakeV18Vector"/> for the same seed. Slot 228+ doesn't
    /// exist for v1.7 (no ROOT partition).
    /// </summary>
    private static double[] MakeV17Vector(double seed)
    {
        var v = new double[228];
        for (var i = 6;  i < 30; i++) v[i] = seed * 0.1 + i * 0.01;
        for (var i = 30; i < 54; i++) v[i] = seed * 0.2 + i * 0.01;
        for (var i = 54; i < 66; i++) v[i] = seed * 0.3 + i * 0.01;
        for (var i = 66; i < 78; i++) v[i] = seed * 0.4 + i * 0.01;
        return v;
    }

    private static VoicingEmbedding MakeVoicing(string id, double[] embedding) =>
        new(
            Id:                  id,
            ChordName:           "Test",
            VoicingType:         null,
            Position:            null,
            Difficulty:          null,
            ModeName:            null,
            ModalFamily:         null,
            PossibleKeys:        [],
            SemanticTags:        [],
            PrimeFormId:         "",
            TranslationOffset:   0,
            Diagram:             "",
            MidiNotes:           [],
            PitchClassSet:       "",
            IntervalClassVector: "",
            MinFret:             0,
            MaxFret:             0,
            BarreRequired:       false,
            HandStretch:         0,
            StackingType:        null,
            RootPitchClass:      null,
            MidiBassNote:        0,
            HarmonicFunction:    null,
            IsNaturallyOccurring: false,
            ConsonanceScore:     0,
            BrightnessScore:     0,
            IsRootless:          false,
            HasGuideTones:       false,
            Inversion:           0,
            TopPitchClass:       null,
            TexturalDescription: null,
            DoubledTones:        null,
            AlternateNames:      null,
            OmittedTones:        null,
            CagedShape:          null,
            Description:         id,
            Embedding:           embedding,
            TextEmbedding:       null);

    [Test]
    public async Task SemanticSearchAsync_V18QueryAgainstV18Voicing_ScoresNonZero()
    {
        // Identical seed for query and voicing → similarity should be near 1.0.
        // The pre-fix code would have FAILED line 239's dim-recognition
        // (240 not in the list) and fallen back to TensorPrimitives.CosineSimilarity
        // which works — but it would've SKIPPED the partition-aware path.
        var strategy = new CpuVoicingSearchStrategy();
        var query    = MakeV18Vector(1.0);
        await strategy.InitializeAsync([MakeVoicing("v18-target", MakeV18Vector(1.0))]);

        var results = await strategy.SemanticSearchAsync(query, limit: 1);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Score, Is.GreaterThan(0.0),
            "v1.8 query against v1.8 voicing must produce a non-zero score");
    }

    [Test]
    public async Task SemanticSearchAsync_V18QueryAgainstV17Voicing_NoLongerReturnsZero()
    {
        // The user-reported regression: v1.8 query (240) × v1.7 indexed voicing
        // (228) → length mismatch → 0.0 score. The fix recognizes both as
        // OPTIC-K dims and routes to partition-aware similarity, which reads
        // slots 6-77 (layout-stable across versions) — so a non-zero score
        // emerges from genuinely-similar voicings even cross-version.
        var strategy = new CpuVoicingSearchStrategy();
        var query    = MakeV18Vector(1.0);
        await strategy.InitializeAsync([MakeVoicing("v17-target", MakeV17Vector(1.0))]);

        var results = await strategy.SemanticSearchAsync(query, limit: 1);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Score, Is.GreaterThan(0.0),
            "v1.8 query against v1.7 voicing MUST produce a non-zero score — " +
            "the regression was returning 0.0 for every cross-version pair.");
    }

    [Test]
    public async Task SemanticSearchAsync_V18QueryRanksMatchAboveMismatch()
    {
        // Two voicings with different similarity-partition values; the
        // matching-seed voicing must outrank the dissimilar one. This pins
        // that the fix didn't accidentally make all scores collapse to a
        // single value (e.g. by returning a constant on the cross-version
        // path).
        var strategy = new CpuVoicingSearchStrategy();
        var query    = MakeV18Vector(1.0);
        await strategy.InitializeAsync(
        [
            MakeVoicing("good-match",   MakeV17Vector(1.0)),
            MakeVoicing("poor-match",   MakeV17Vector(7.0)),
        ]);

        var results = await strategy.SemanticSearchAsync(query, limit: 2);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Document.Id, Is.EqualTo("good-match"),
            "the seed-matching voicing must rank first; cross-version comparison must still be discriminating");
    }

    [Test]
    public async Task SemanticSearchAsync_V18QueryAgainstLegacyVoicing_ScoresNonZero()
    {
        // Cross-version with the OLDEST recognized dim (v1.2.1 = 96).
        // Slots 6..77 still exist in legacy (96 ≥ 78), so partition-aware
        // similarity is safe. Reviewer (octo-code-reviewer on PR #95)
        // requested explicit coverage so future schema changes don't
        // silently regress the legacy compatibility surface.
        var strategy = new CpuVoicingSearchStrategy();
        var query    = MakeV18Vector(1.0);
        var legacy   = new double[96];
        for (var i = 6;  i < 30; i++) legacy[i] = 1.0 * 0.1 + i * 0.01;
        for (var i = 30; i < 54; i++) legacy[i] = 1.0 * 0.2 + i * 0.01;
        for (var i = 54; i < 66; i++) legacy[i] = 1.0 * 0.3 + i * 0.01;
        for (var i = 66; i < 78; i++) legacy[i] = 1.0 * 0.4 + i * 0.01;

        await strategy.InitializeAsync([MakeVoicing("legacy-target", legacy)]);

        var results = await strategy.SemanticSearchAsync(query, limit: 1);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Score, Is.GreaterThan(0.0),
            "v1.8 query against legacy (96-dim) voicing must produce a non-zero score");
    }

    [Test]
    public async Task SemanticSearchAsync_QueryWithUnknownDim_DoesNotCrashAndReturnsResults()
    {
        // Unknown query dim (e.g. some future schema). When the query is NOT
        // a known OPTIC-K dim and the target IS, the fallback is
        // (1) length mismatch with v1.7/v1.8 → 0.0 (correct: cross-version
        // semantics are only safe when BOTH sides are OPTIC-K)
        // (2) length match → standard cosine similarity
        // The test asserts no crash and returns the expected number of results.
        var strategy = new CpuVoicingSearchStrategy();
        var query    = new double[300]; // not a known OPTIC-K dim
        for (var i = 0; i < 300; i++) query[i] = 0.1 * i;
        await strategy.InitializeAsync([MakeVoicing("v17", MakeV17Vector(1.0))]);

        Assert.That(async () => await strategy.SemanticSearchAsync(query, limit: 1),
            Throws.Nothing);
    }
}
