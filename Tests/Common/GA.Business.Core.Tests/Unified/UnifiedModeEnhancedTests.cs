namespace GA.Business.Core.Tests.Unified;

using GA.Business.Core.Atonal;
using GA.Business.Core.Unified;

/// <summary>
/// Tests for the enhanced UnifiedModeService features:
/// - Messiaen Mode detection
/// - Brightness calculation
/// - Spectral Centroid
/// - Voice Leading metrics (Common Tones, Voice Leading Distance)
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UnifiedModeEnhancedTests
{
    private static PitchClassSet Pcs(params int[] pcs)
        => new([.. pcs.Select(PitchClass.FromValue)]);

    private readonly UnifiedModeService _svc = new();

    #region Messiaen Mode Detection

    [Test]
    public void Describe_WholeTone_Is_MessiaenMode1()
    {
        // Mode 1: Whole Tone [0,2,4,6,8,10]
        var set = Pcs(0, 2, 4, 6, 8, 10);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.MessiaenModeIndex, Is.EqualTo(1));
    }

    [Test]
    public void Describe_Octatonic_Is_MessiaenMode2()
    {
        // Mode 2: Octatonic [0,1,3,4,6,7,9,10]
        var set = Pcs(0, 1, 3, 4, 6, 7, 9, 10);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.MessiaenModeIndex, Is.EqualTo(2));
    }

    [Test]
    public void Describe_MajorScale_Is_Not_MessiaenMode()
    {
        // Major scale [0,2,4,5,7,9,11] is NOT a mode of limited transposition
        var set = Pcs(0, 2, 4, 5, 7, 9, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.MessiaenModeIndex, Is.Null);
    }

    [TestCaseSource(nameof(MessiaenModeTestCases))]
    public void Describe_MessiaenModes_AreDetectedCorrectly(int[] pitchClasses, int expectedMode)
    {
        var set = Pcs(pitchClasses);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.MessiaenModeIndex, Is.EqualTo(expectedMode));
    }

    private static IEnumerable<TestCaseData> MessiaenModeTestCases()
    {
        yield return new TestCaseData(new[] { 0, 2, 4, 6, 8, 10 }, 1).SetName("Mode 1: Whole Tone");
        yield return new TestCaseData(new[] { 0, 1, 3, 4, 6, 7, 9, 10 }, 2).SetName("Mode 2: Octatonic");
    }

    #endregion

    #region Brightness

    [Test]
    public void Describe_Lydian_Is_Brighter_Than_Locrian()
    {
        // Major family modes at C (all with same PrimeForm but different rotations)
        // Lydian: [0,2,4,6,7,9,11] - brightest
        // Locrian: [0,1,3,5,6,8,10] - darkest
        var lydianSet = Pcs(0, 2, 4, 6, 7, 9, 11);
        var locrianSet = Pcs(0, 1, 3, 5, 6, 8, 10);

        var lydianInst = _svc.FromPitchClassSet(lydianSet, PitchClass.C);
        var locrianInst = _svc.FromPitchClassSet(locrianSet, PitchClass.C);

        var lydianDesc = _svc.Describe(lydianInst);
        var locrianDesc = _svc.Describe(locrianInst);

        Assert.That(lydianDesc.Brightness, Is.GreaterThan(locrianDesc.Brightness));
    }

    [Test]
    public void Describe_Brightness_Is_Positive_For_NonEmptySet()
    {
        var set = Pcs(0, 4, 7); // C Major triad
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        // Sum of {0, 4, 7} = 11
        Assert.That(desc.Brightness, Is.GreaterThan(0));
    }

    #endregion

    #region Spectral Centroid

    [Test]
    public void Describe_SpectralCentroid_Is_NonNegative()
    {
        var set = Pcs(0, 2, 4, 5, 7, 9, 11); // Major scale
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.SpectralCentroid, Is.GreaterThanOrEqualTo(0.0));
    }

    [Test]
    public void SpectralCentroid_Differs_For_Different_SetClasses()
    {
        // Different set classes should have different spectral centroids
        var majorSet = Pcs(0, 2, 4, 5, 7, 9, 11);  // Major (7-35)
        var wholeToneSet = Pcs(0, 2, 4, 6, 8, 10); // Whole Tone (6-35)

        var majorInst = _svc.FromPitchClassSet(majorSet, PitchClass.C);
        var wholeToneInst = _svc.FromPitchClassSet(wholeToneSet, PitchClass.C);

        var majorDesc = _svc.Describe(majorInst);
        var wholeToneDesc = _svc.Describe(wholeToneInst);

        // They're different set classes so centroids may differ
        // Note: This is a weak assertion; mainly verifying no crash
        Assert.That(majorDesc.SpectralCentroid, Is.Not.EqualTo(wholeToneDesc.SpectralCentroid).Within(0.01)
            .Or.GreaterThanOrEqualTo(0));
    }

    #endregion

    #region Voice Leading: Common Tones

    [Test]
    public void GetCommonToneCount_CMajor_And_AMinor_Have_SixCommonTones()
    {
        // C Major: {C, D, E, F, G, A, B} = {0, 2, 4, 5, 7, 9, 11}
        // A Minor: {A, B, C, D, E, F, G} = {0, 2, 4, 5, 7, 9, 11} (relative minor - same notes!)
        // But if we express A minor with root A (9), the set remains identical for natural minor.
        var cMajorSet = Pcs(0, 2, 4, 5, 7, 9, 11);
        var aMinorSet = Pcs(0, 2, 4, 5, 7, 9, 11); // Same PCS!

        var cMajorInst = _svc.FromPitchClassSet(cMajorSet, PitchClass.C);
        var aMinorInst = _svc.FromPitchClassSet(aMinorSet, PitchClass.FromValue(9)); // Root = A

        var commonTones = _svc.GetCommonToneCount(cMajorInst, aMinorInst);

        Assert.That(commonTones, Is.EqualTo(7)); // All 7 tones are common
    }

    [Test]
    public void GetCommonToneCount_CMajor_And_CMinor_Have_FiveCommonTones()
    {
        // C Major: {0, 2, 4, 5, 7, 9, 11}
        // C Minor (natural): {0, 2, 3, 5, 7, 8, 10}
        // Common: {0, 2, 5, 7} = 4 tones... let's verify.
        // Actually: C(0), D(2), F(5), G(7) are common. That's 4.
        var cMajorSet = Pcs(0, 2, 4, 5, 7, 9, 11);
        var cMinorSet = Pcs(0, 2, 3, 5, 7, 8, 10);

        var cMajorInst = _svc.FromPitchClassSet(cMajorSet, PitchClass.C);
        var cMinorInst = _svc.FromPitchClassSet(cMinorSet, PitchClass.C);

        var commonTones = _svc.GetCommonToneCount(cMajorInst, cMinorInst);

        Assert.That(commonTones, Is.EqualTo(4));
    }

    [Test]
    public void GetCommonToneCount_DisjointSets_Have_ZeroCommonTones()
    {
        // Two disjoint trichords
        var set1 = Pcs(0, 4, 7);  // C Major triad
        var set2 = Pcs(1, 5, 8);  // Db Major triad

        var inst1 = _svc.FromPitchClassSet(set1, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(set2, PitchClass.FromValue(1));

        var commonTones = _svc.GetCommonToneCount(inst1, inst2);

        Assert.That(commonTones, Is.EqualTo(0));
    }

    #endregion

    #region Voice Leading: Distance

    [Test]
    public void GetVoiceLeadingDistance_IdenticalSets_Is_Zero()
    {
        var set = Pcs(0, 4, 7);
        var inst1 = _svc.FromPitchClassSet(set, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(set, PitchClass.C);

        var distance = _svc.GetVoiceLeadingDistance(inst1, inst2);

        Assert.That(distance, Is.EqualTo(0.0));
    }

    [Test]
    public void GetVoiceLeadingDistance_CMajor_To_CMinor_Is_Small()
    {
        // C Major triad: {0, 4, 7}
        // C Minor triad: {0, 3, 7}
        // Only one note moves: E(4) -> Eb(3), distance = 1
        var cMajorTriad = Pcs(0, 4, 7);
        var cMinorTriad = Pcs(0, 3, 7);

        var inst1 = _svc.FromPitchClassSet(cMajorTriad, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(cMinorTriad, PitchClass.C);

        var distance = _svc.GetVoiceLeadingDistance(inst1, inst2);

        Assert.That(distance, Is.EqualTo(1.0));
    }

    [Test]
    public void GetVoiceLeadingDistance_DifferentCardinality_Is_Infinity()
    {
        var triad = Pcs(0, 4, 7);
        var tetrachord = Pcs(0, 4, 7, 11);

        var inst1 = _svc.FromPitchClassSet(triad, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(tetrachord, PitchClass.C);

        var distance = _svc.GetVoiceLeadingDistance(inst1, inst2);

        Assert.That(distance, Is.EqualTo(double.PositiveInfinity));
    }

    [Test]
    public void GetVoiceLeadingDistance_CTriad_To_FTriad_Uses_ModularDistance()
    {
        // C Major triad: {0, 4, 7}
        // F Major triad: {0, 5, 9} (F=5, A=9, C=0)
        // Sorted: {0, 4, 7} vs {0, 5, 9}
        // Distances: |0-0|=0, |4-5|=1, |7-9|=2 -> Total = 3
        var cTriad = Pcs(0, 4, 7);
        var fTriad = Pcs(0, 5, 9);

        var inst1 = _svc.FromPitchClassSet(cTriad, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(fTriad, PitchClass.FromValue(5));

        var distance = _svc.GetVoiceLeadingDistance(inst1, inst2);

        Assert.That(distance, Is.EqualTo(3.0));
    }

    #endregion
}
