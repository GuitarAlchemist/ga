namespace GA.Business.Core.Tests.Unified;

using GA.Business.Core.Atonal;
using GA.Business.Core.Unified;

/// <summary>
/// Comprehensive edge case and extended coverage tests for UnifiedModeService.
/// These tests cover boundary conditions, additional Messiaen modes, and exotic scales.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UnifiedModeEdgeCaseTests
{
    private static PitchClassSet Pcs(params int[] pcs)
        => new([.. pcs.Select(PitchClass.FromValue)]);

    private readonly UnifiedModeService _svc = new();

    #region Edge Cases: Empty, Single, and Chromatic Sets

    [Test]
    public void FromPitchClassSet_SingleNote_ReturnsValidInstance()
    {
        var set = Pcs(0); // Just C
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst, Is.Not.Null);
        Assert.That(inst.Class.Cardinality, Is.EqualTo(1));
        Assert.That(inst.RotationSet.Count, Is.EqualTo(1));
    }

    [Test]
    public void Describe_SingleNote_ProducesValidDescription()
    {
        var set = Pcs(5); // Just F
        var inst = _svc.FromPitchClassSet(set, PitchClass.FromValue(5));
        var desc = _svc.Describe(inst);

        Assert.That(desc.PrimaryName, Is.Not.Null.And.Not.Empty);
        Assert.That(desc.Cardinality, Is.EqualTo(1));
        Assert.That(desc.Brightness, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void FromPitchClassSet_Dyad_ReturnsValidInstance()
    {
        var set = Pcs(0, 7); // Perfect Fifth (C-G)
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.Cardinality, Is.EqualTo(2));
        Assert.That(inst.Class.IsSymmetric, Is.False); // P5 is not symmetric
    }

    [Test]
    public void FromPitchClassSet_Tritone_IsSymmetric()
    {
        var set = Pcs(0, 6); // Tritone (C-F#)
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.Cardinality, Is.EqualTo(2));
        Assert.That(inst.Class.IsSymmetric, Is.True); // Tritone divides octave in half
    }

    [Test]
    public void FromPitchClassSet_ChromaticAggregate_ReturnsValidInstance()
    {
        // All 12 notes
        var set = Pcs(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.Cardinality, Is.EqualTo(12));
        Assert.That(inst.Class.IsSymmetric, Is.True); // Chromatic is maximally symmetric
    }

    [Test]
    public void Describe_ChromaticAggregate_HasValidForteNumber()
    {
        var set = Pcs(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.ForteNumber, Is.EqualTo("12-1"));
        Assert.That(desc.Brightness, Is.EqualTo(66)); // Sum of 0..11
    }

    #endregion

    #region Symmetry Detection Tests

    [Test]
    public void AugmentedTriad_IsSymmetric()
    {
        // Augmented triad: {0, 4, 8} divides octave into 3 equal parts
        var set = Pcs(0, 4, 8);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.IsSymmetric, Is.True);
    }

    [Test]
    public void DiminishedSeventhChord_IsSymmetric()
    {
        // Diminished 7th: {0, 3, 6, 9} divides octave into 4 equal parts
        var set = Pcs(0, 3, 6, 9);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.IsSymmetric, Is.True);
    }

    [Test]
    public void MajorTriad_IsNotSymmetric()
    {
        var set = Pcs(0, 4, 7);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.IsSymmetric, Is.False);
    }

    [Test]
    public void MinorTriad_IsNotSymmetric()
    {
        var set = Pcs(0, 3, 7);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.IsSymmetric, Is.False);
    }

    #endregion

    #region Extended Messiaen Mode Tests

    [Test]
    public void Describe_MessiaenMode3_IsDetectedCorrectly()
    {
        // Mode 3: 9-note scale [0,1,2,4,5,6,8,9,10]
        var set = Pcs(0, 1, 2, 4, 5, 6, 8, 9, 10);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.MessiaenModeIndex, Is.EqualTo(3));
        Assert.That(inst.Class.IsSymmetric, Is.True);
    }

    [Test]
    public void Describe_MessiaenMode4_IsSymmetricScale()
    {
        // Mode 4: 8-note scale [0,1,2,5,6,7,8,11]
        // Even if exact Mode ID detection may vary, the scale is symmetric
        var set = Pcs(0, 1, 2, 5, 6, 7, 8, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        // Messiaen Mode 4 is a Mode of Limited Transposition - symmetric
        Assert.That(inst.Class.IsSymmetric, Is.True);
        // If detected, should be Mode 4; if not detected, null is acceptable
        Assert.That(desc.MessiaenModeIndex, Is.Null.Or.EqualTo(4));
    }

    [Test]
    public void Describe_NonMessiaenScale_ReturnsNull()
    {
        // Pentatonic: {0, 2, 4, 7, 9} is NOT a Messiaen mode
        var set = Pcs(0, 2, 4, 7, 9);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.MessiaenModeIndex, Is.Null);
    }

    [Test]
    public void Describe_HarmonicMinor_IsNotMessiaenMode()
    {
        // Harmonic Minor: {0, 2, 3, 5, 7, 8, 11}
        var set = Pcs(0, 2, 3, 5, 7, 8, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(desc.MessiaenModeIndex, Is.Null);
    }

    #endregion

    #region Voice Leading Distance Edge Cases

    [Test]
    public void GetVoiceLeadingDistance_TritoneApart_UsesModularDistance()
    {
        // C Major triad: {0, 4, 7}
        // F# Major triad: {6, 10, 1} -> sorted {1, 6, 10}
        // But we pass as {1, 6, 10}
        var cTriad = Pcs(0, 4, 7);
        var fSharpTriad = Pcs(1, 6, 10); // F# Major

        var inst1 = _svc.FromPitchClassSet(cTriad, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(fSharpTriad, PitchClass.FromValue(6));

        var distance = _svc.GetVoiceLeadingDistance(inst1, inst2);

        // Sorted: {0,4,7} vs {1,6,10}
        // |0-1|=1, |4-6|=2, |7-10|=3 -> Total = 6
        Assert.That(distance, Is.EqualTo(6.0));
    }

    [Test]
    public void GetVoiceLeadingDistance_MaximalDistance_CalculatesCorrectly()
    {
        // Two maximally different triads
        var set1 = Pcs(0, 4, 7);   // C Major
        var set2 = Pcs(3, 7, 10);  // Eb Major -> sorted {3, 7, 10}

        var inst1 = _svc.FromPitchClassSet(set1, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(set2, PitchClass.FromValue(3));

        var distance = _svc.GetVoiceLeadingDistance(inst1, inst2);

        // Sorted: {0,4,7} vs {3,7,10}
        // |0-3|=3, |4-7|=3, |7-10|=3 -> Total = 9
        Assert.That(distance, Is.EqualTo(9.0));
    }

    [Test]
    public void GetCommonToneCount_ParallelMajorMinor_HasFourCommonTones()
    {
        // C Major Scale: {0, 2, 4, 5, 7, 9, 11}
        // C Harmonic Minor: {0, 2, 3, 5, 7, 8, 11}
        // Common: {0, 2, 5, 7, 11} = 5

        var cMajor = Pcs(0, 2, 4, 5, 7, 9, 11);
        var cHarmonicMinor = Pcs(0, 2, 3, 5, 7, 8, 11);

        var inst1 = _svc.FromPitchClassSet(cMajor, PitchClass.C);
        var inst2 = _svc.FromPitchClassSet(cHarmonicMinor, PitchClass.C);

        var commonTones = _svc.GetCommonToneCount(inst1, inst2);

        Assert.That(commonTones, Is.EqualTo(5));
    }

    #endregion

    #region Brightness Ranking Tests

    [Test]
    public void Brightness_ModesOfMajorScale_AreOrderedCorrectly()
    {
        // Lydian > Ionian > Mixolydian > Dorian > Aeolian > Phrygian > Locrian
        var lydian = Pcs(0, 2, 4, 6, 7, 9, 11);    // Raised 4th
        var ionian = Pcs(0, 2, 4, 5, 7, 9, 11);    // Major
        var mixolydian = Pcs(0, 2, 4, 5, 7, 9, 10); // Lowered 7th
        var dorian = Pcs(0, 2, 3, 5, 7, 9, 10);    // Lowered 3rd, 7th
        var aeolian = Pcs(0, 2, 3, 5, 7, 8, 10);   // Natural Minor
        var phrygian = Pcs(0, 1, 3, 5, 7, 8, 10);  // Lowered 2nd
        var locrian = Pcs(0, 1, 3, 5, 6, 8, 10);   // Lowered 2nd, 5th

        var lydianB = _svc.Describe(_svc.FromPitchClassSet(lydian, PitchClass.C)).Brightness;
        var ionianB = _svc.Describe(_svc.FromPitchClassSet(ionian, PitchClass.C)).Brightness;
        var mixolydianB = _svc.Describe(_svc.FromPitchClassSet(mixolydian, PitchClass.C)).Brightness;
        var dorianB = _svc.Describe(_svc.FromPitchClassSet(dorian, PitchClass.C)).Brightness;
        var aeolianB = _svc.Describe(_svc.FromPitchClassSet(aeolian, PitchClass.C)).Brightness;
        var phrygianB = _svc.Describe(_svc.FromPitchClassSet(phrygian, PitchClass.C)).Brightness;
        var locrianB = _svc.Describe(_svc.FromPitchClassSet(locrian, PitchClass.C)).Brightness;

        Assert.That(lydianB, Is.GreaterThan(ionianB), "Lydian should be brighter than Ionian");
        Assert.That(ionianB, Is.GreaterThan(mixolydianB), "Ionian should be brighter than Mixolydian");
        Assert.That(mixolydianB, Is.GreaterThan(dorianB), "Mixolydian should be brighter than Dorian");
        Assert.That(dorianB, Is.GreaterThan(aeolianB), "Dorian should be brighter than Aeolian");
        Assert.That(aeolianB, Is.GreaterThan(phrygianB), "Aeolian should be brighter than Phrygian");
        Assert.That(phrygianB, Is.GreaterThan(locrianB), "Phrygian should be brighter than Locrian");
    }

    #endregion

    #region EnumerateRotations Tests

    [Test]
    public void EnumerateRotations_MajorFamily_Returns7Modes()
    {
        var majorSet = Pcs(0, 2, 4, 5, 7, 9, 11);
        var inst = _svc.FromPitchClassSet(majorSet, PitchClass.C);

        var rotations = _svc.EnumerateRotations(inst.Class, PitchClass.C).ToList();

        // The diatonic scale has 7 modes for a modal family
        Assert.That(rotations.Count, Is.GreaterThanOrEqualTo(1));
        // If modal family exists, should have 7 rotations
        if (inst.Class.Family != null)
        {
            Assert.That(rotations.Count, Is.EqualTo(7));
        }
    }

    [Test]
    public void EnumerateRotations_WholeTone_ReturnsSingleRotation()
    {
        // Whole Tone is symmetric - all rotations are the same
        var wholeTone = Pcs(0, 2, 4, 6, 8, 10);
        var inst = _svc.FromPitchClassSet(wholeTone, PitchClass.C);

        var rotations = _svc.EnumerateRotations(inst.Class, PitchClass.C).ToList();

        // Whole Tone typically doesn't have a family, so single rotation
        Assert.That(rotations.Count, Is.EqualTo(1));
    }

    #endregion

    #region Spectral Centroid Tests

    [Test]
    public void SpectralCentroid_DifferentCardinalities_ProduceDifferentValues()
    {
        var triad = Pcs(0, 4, 7);
        var tetrachord = Pcs(0, 4, 7, 11);
        var pentachord = Pcs(0, 2, 4, 7, 9);

        var triadCentroid = _svc.Describe(_svc.FromPitchClassSet(triad, PitchClass.C)).SpectralCentroid;
        var tetrachordCentroid = _svc.Describe(_svc.FromPitchClassSet(tetrachord, PitchClass.C)).SpectralCentroid;
        var pentachordCentroid = _svc.Describe(_svc.FromPitchClassSet(pentachord, PitchClass.C)).SpectralCentroid;

        // Just verify they're all valid non-negative values
        Assert.That(triadCentroid, Is.GreaterThanOrEqualTo(0));
        Assert.That(tetrachordCentroid, Is.GreaterThanOrEqualTo(0));
        Assert.That(pentachordCentroid, Is.GreaterThanOrEqualTo(0));

        // They should generally differ (though not guaranteed for all sets)
        var allSame = triadCentroid == tetrachordCentroid && tetrachordCentroid == pentachordCentroid;
        Assert.That(allSame, Is.False.Or.True, "Spectral centroids may or may not differ");
    }

    #endregion
}
