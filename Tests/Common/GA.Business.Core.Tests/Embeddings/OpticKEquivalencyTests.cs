namespace GA.Business.Core.Tests.Embeddings;

using ML.Embeddings.Services;

[TestFixture]
public class OpticKEquivalencyTests
{
    [SetUp]
    public void SetUp() => _theoryService = new();

    private TheoryVectorService _theoryService;

    [Test]
    public void Transposition_Invariance_ICV_Match()
    {
        // C Major: {0, 4, 7} -> ICV: 001110
        // G Major: {7, 11, 2} -> ICV: 001110
        var cMajorPcs = new[] { 0, 4, 7 };
        var gMajorPcs = new[] { 7, 11, 2 };
        const string icv = "001110";
        var vC = _theoryService.ComputeEmbedding(cMajorPcs, intervalClassVector: icv);
        var vG = _theoryService.ComputeEmbedding(gMajorPcs, intervalClassVector: icv);
        // ICV indices are 13-18
        for (var i = 13; i <= 18; i++)
        {
            Assert.That(vC[i], Is.EqualTo(vG[i]), $"Index {i} should match for transpositionally equivalent sets.");
        }
    }

    [Test]
    public void Involution_Invariance_ICV_Match()
    {
        // C Major: {0, 4, 7} -> ICV: 001110
        // C Minor: {0, 3, 7} -> ICV: 001110 (Inversion of Major)
        var cMajorPcs = new[] { 0, 4, 7 };
        var cMinorPcs = new[] { 0, 3, 7 };
        const string icv = "001110";
        var vMaj = _theoryService.ComputeEmbedding(cMajorPcs, intervalClassVector: icv);
        var vMin = _theoryService.ComputeEmbedding(cMinorPcs, intervalClassVector: icv);
        // ICV indices are 13-18
        for (var i = 13; i <= 18; i++)
        {
            Assert.That(vMaj[i], Is.EqualTo(vMin[i]), $"Index {i} should match for inversionally equivalent sets.");
        }
    }

    [Test]
    public void Cardinality_Encoding_Correct()
    {
        var pcs3 = new[] { 0, 4, 7 };
        var pcs4 = new[] { 0, 4, 7, 10 };
        var v3 = _theoryService.ComputeEmbedding(pcs3);
        var v4 = _theoryService.ComputeEmbedding(pcs4);
        // Index 12 is Cardinality (Weighted: * 2.0)
        Assert.That(v3[12], Is.EqualTo(0.5));
        Assert.That(v4[12], Is.EqualTo(4.0 / 12.0 * 2.0).Within(0.001));
    }

    [Test]
    public void Identity_Encoding_OneHot_Correct()
    {
        var identityService = new IdentityVectorService();
        var vChord = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Chord);
        var vScale = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Scale);
        // Chord is index 0, Scale is index 1
        Assert.That(vChord[0], Is.EqualTo(1.0));
        Assert.That(vChord[1], Is.EqualTo(0.0));
        Assert.That(vScale[1], Is.EqualTo(1.0));
        Assert.That(vScale[0], Is.EqualTo(0.0));
    }

    [Test]
    public void Composite_Identity_Logic_Works()
    {
        var identityService = new IdentityVectorService();
        var vVoicing = identityService.ComputeEmbedding(IdentityVectorService.ObjectKind.Voicing);
        // A Voicing (2) is also a Chord (0) and a PitchClassSet (5)
        Assert.That(vVoicing[2], Is.EqualTo(1.0), "Should be a Voicing");
        Assert.That(vVoicing[0], Is.EqualTo(1.0), "A Voicing should also be a Chord");
        Assert.That(vVoicing[5], Is.EqualTo(1.0), "A Chord should imply a PitchClassSet");
    }

    [Test]
    public void Invariant32_StructureVectorIsIdenticalForSamePcSet_IgnoringPerceptualArgs()
    {
        // Invariant #32: Same PC-set across octaves → cosine(STRUCTURE, STRUCTURE') == 1.0.
        // The caller used to pass octave-dependent `consonance` and `brightness` into STRUCTURE,
        // breaking the invariant. The service now derives dims 20-21 from the ICV and ignores
        // those parameters. Two callers with identical PC-set + ICV but different perceptual
        // inputs must produce identical STRUCTURE vectors.
        var pcs = new[] { 0, 4, 7 };
        const string icv = "001110";

        var vLowRegister = _theoryService.ComputeEmbedding(pcs, rootPitchClass: 0,
            intervalClassVector: icv, consonance: 0.9, brightness: 0.2);
        var vHighRegister = _theoryService.ComputeEmbedding(pcs, rootPitchClass: 0,
            intervalClassVector: icv, consonance: 0.3, brightness: 0.95);

        Assert.That(vLowRegister, Is.EqualTo(vHighRegister),
            "STRUCTURE must be octave-invariant — perceptual args must not affect the vector.");
    }

    [Test]
    public void IcvDerivedConsonance_RangesZeroToOne_AndFavoursThirdsAndFifths()
    {
        // Major triad ICV 001110: all three intervals are consonant (ic3, ic4, ic5).
        var cMajor = _theoryService.ComputeEmbedding(new[] { 0, 4, 7 },
            intervalClassVector: "001110");
        // Tritone pair {0,6}: single ic6, fully dissonant.
        var tritone = _theoryService.ComputeEmbedding(new[] { 0, 6 },
            intervalClassVector: "000001");

        Assert.That(cMajor[20], Is.EqualTo(1.0).Within(1e-9),
            "Major triad should be maximally consonant under the ICV proxy.");
        Assert.That(tritone[20], Is.EqualTo(0.0).Within(1e-9),
            "Tritone pair should be maximally dissonant under the ICV proxy.");
    }

    [Test]
    public void IcvDerivedBrightness_RangesZeroToOne_AndMapsBrightToOneDarkToZero()
    {
        // Pure bright intervals: ic4 + ic5, no ic1/ic6 → brightness ≈ 1.0.
        var bright = _theoryService.ComputeEmbedding(new[] { 0, 4, 7 },
            intervalClassVector: "001110");
        // Pure tritone {0, 6}: only ic6, no bright content → brightness = 0.0.
        var dark = _theoryService.ComputeEmbedding(new[] { 0, 6 },
            intervalClassVector: "000001");
        // Empty ICV → neutral 0.5.
        var neutral = _theoryService.ComputeEmbedding(new[] { 0 },
            intervalClassVector: "000000");

        Assert.That(bright[21], Is.GreaterThan(0.8));
        Assert.That(dark[21], Is.LessThan(0.2));
        Assert.That(neutral[21], Is.EqualTo(0.5).Within(1e-9));
    }
}
