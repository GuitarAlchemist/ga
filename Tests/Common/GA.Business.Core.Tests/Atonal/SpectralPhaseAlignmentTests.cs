namespace GA.Business.Core.Tests.Atonal;

using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Verifies the phase-aligned similarity operator against the numeric oracle in
///     docs/research/2026-07-04-optick-spectral-phase-alignment.md §4, plus the
///     exhaustive separation guarantee: it distinguishes all 23 homometric Z-pairs
///     (and major/minor chirality) that the interval-class vector cannot.
/// </summary>
[TestFixture]
public class SpectralPhaseAlignmentTests
{
    private static PitchClassSet Pcs(params int[] pcs)
        => new(pcs.Select(PitchClass.FromValue));

    // ── §4 oracle table ──────────────────────────────────────────────────────

    [Test]
    public void Similarity_RecoversTransposition_ForMajorTriads()
    {
        // C major {0,4,7} vs D major {2,6,9} = T₂ — same shape, aligned, t* = 10.
        var r = SpectralPhaseAlignment.Similarity(Pcs(0, 4, 7), Pcs(2, 6, 9));
        TestContext.WriteLine($"S={r.Similarity:F4} t*=[{string.Join(",", r.AligningTranspositions)}]");
        Assert.That(r.Similarity, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(r.AligningTranspositions, Does.Contain(10));
    }

    [Test]
    public void Similarity_PreservesChirality_MajorVsMinor()
    {
        // Major {0,4,7} vs minor {0,3,7}: ICV-identical (both 3-11) — plain S must NOT be 1
        // (chirality preserved), but the TnI path recovers their equivalence.
        var major = Pcs(0, 4, 7);
        var minor = Pcs(0, 3, 7);

        var plain = SpectralPhaseAlignment.Similarity(major, minor);
        var tni = SpectralPhaseAlignment.SimilarityTnI(major, minor);

        TestContext.WriteLine($"S={plain.Similarity:F4}  S_TnI={tni.Similarity:F4}");
        Assert.Multiple(() =>
        {
            Assert.That(plain.Similarity, Is.EqualTo(0.5714).Within(1e-3), "chirality-preserving S");
            Assert.That(tni.Similarity, Is.EqualTo(1.0).Within(1e-9), "TnI-equivalent");
            Assert.That(tni.Inverted, Is.True, "the winning TnI alignment is the inversion");
        });
    }

    [Test]
    public void Similarity_SeparatesZPair_4Z15_4Z29()
    {
        var r = SpectralPhaseAlignment.Similarity(Pcs(0, 1, 4, 6), Pcs(0, 1, 3, 7));
        var tni = SpectralPhaseAlignment.SimilarityTnI(Pcs(0, 1, 4, 6), Pcs(0, 1, 3, 7));
        TestContext.WriteLine($"4-Z15 vs 4-Z29: S={r.Similarity:F4} S_TnI={tni.Similarity:F4}");
        Assert.Multiple(() =>
        {
            Assert.That(r.Similarity, Is.EqualTo(0.3333).Within(1e-3));
            Assert.That(tni.Similarity, Is.EqualTo(0.6667).Within(1e-3));
        });
    }

    [Test]
    public void Similarity_SeparatesZPair_6Z17_6Z43()
    {
        var r = SpectralPhaseAlignment.Similarity(Pcs(0, 1, 2, 4, 7, 8), Pcs(0, 1, 2, 5, 6, 8));
        TestContext.WriteLine($"6-Z17 vs 6-Z43: S={r.Similarity:F4}");
        Assert.That(r.Similarity, Is.EqualTo(0.4000).Within(1e-3));
    }

    // ── exhaustive separation (the product guarantee) ────────────────────────

    [Test]
    public void SimilarityTnI_SeparatesAll23ZRelatedPairs()
    {
        // Z-related = same ICV, different set class. Grouping set classes by ICV yields
        // the 23 homometric pairs of 12-TET — but only after excluding the degenerate
        // classes: SetClass.Items includes the empty set and the singleton, which BOTH
        // carry the null ICV <0 0 0 0 0 0> and would otherwise form a spurious 24th
        // "pair" (trivially unseparable). Real Z-pairs are cardinality 4..8.
        var zPairs = SetClass.Items
            .Where(sc => sc.Cardinality >= 3)
            .GroupBy(sc => sc.IntervalClassVector.Id)
            .Where(g => g.Count() == 2)
            .Select(g => (A: g.First(), B: g.Last()))
            .ToList();

        Assert.That(zPairs, Has.Count.EqualTo(23), "the 23 12-TET Z-pairs (guards the enumeration)");

        var worst = double.NegativeInfinity;
        var separated = 0;
        foreach (var (a, b) in zPairs)
        {
            // Precondition: ICV genuinely cannot tell them apart.
            Assert.That(a.IntervalClassVector.Id, Is.EqualTo(b.IntervalClassVector.Id));

            var s = SpectralPhaseAlignment.SimilarityTnI(a.PrimeForm, b.PrimeForm).Similarity;
            TestContext.WriteLine($"card {a.Cardinality}: {a.PrimeForm} / {b.PrimeForm}  ICV={a.IntervalClassVector.Id}  S_TnI={s:F4}");
            Assert.That(s, Is.LessThan(1.0 - 1e-6), $"Z-pair {a} / {b} not separated: S_TnI={s:F4}");
            worst = Math.Max(worst, s);
            separated++;
        }

        TestContext.WriteLine($"separated {separated}/{zPairs.Count}; worst S_TnI = {worst:F4}");
        Assert.That(separated, Is.EqualTo(zPairs.Count));
    }

    [Test]
    public void Similarity_IsOne_OnEveryTransposition_Randomized()
    {
        var rng = new Random(42);
        for (var iter = 0; iter < 1000; iter++)
        {
            var card = rng.Next(1, 12); // 1..11 (skip empty / aggregate — degenerate)
            var pool = Enumerable.Range(0, 12).OrderBy(_ => rng.Next()).Take(card).ToArray();
            var t = rng.Next(0, 12);

            var a = Pcs(pool);
            var b = Pcs([.. pool.Select(p => (p + t) % 12)]);

            var r = SpectralPhaseAlignment.Similarity(a, b);
            Assert.That(r.Similarity, Is.EqualTo(1.0).Within(1e-9),
                $"iter {iter}: card={card} t={t} pcs=[{string.Join(",", pool)}] S={r.Similarity}");
            Assert.That(r.AligningTranspositions, Does.Contain((12 - t) % 12));
        }
    }

    // ── zero-denominator convention (ga#513) ─────────────────────────────────

    [Test]
    public void EmptySet_IsTriviallyAligned()
    {
        var r = SpectralPhaseAlignment.Similarity(new PitchClassSet([]), Pcs(0, 4, 7));
        Assert.That(r.Similarity, Is.EqualTo(1.0).Within(1e-9), "empty set is fixed by every T_t");
        Assert.That(r.AligningTranspositions, Has.Count.EqualTo(12));
    }

    [Test]
    public void DisjointSupport_IsMaximallyDissimilar()
    {
        // Augmented triad {0,4,8} (spectral support {3,6}) vs diminished 7th
        // {0,3,6,9} (support {4}) share no periodicity content → S := 0, no t*.
        var r = SpectralPhaseAlignment.Similarity(Pcs(0, 4, 8), Pcs(0, 3, 6, 9));
        TestContext.WriteLine($"aug vs dim7: S={r.Similarity:F4} t*count={r.AligningTranspositions.Count}");
        Assert.Multiple(() =>
        {
            Assert.That(r.Similarity, Is.EqualTo(0.0).Within(1e-9));
            Assert.That(r.AligningTranspositions, Is.Empty);
        });
    }
}
