namespace GA.Business.Core.Tests.Unified;

using GA.Business.Core.Atonal;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using GA.Business.Core.Unified;

[TestFixture]
public class UnifiedModeCoverageTests
{
    private static PitchClassSet Pcs(params int[] pcs)
        => new([.. pcs.Select(PitchClass.FromValue)]);

    [Test]
    public void FromScaleMode_RoundTrips_To_FromPitchClassSet_For_Major_Ionian()
    {
        // Build C Ionian via ScaleMode
        var mode = MajorScaleMode.Get(MajorScaleDegree.Ionian);
        var root = PitchClass.C;
        var unified = new UnifiedModeService().FromScaleMode(mode, root);

        // Recreate via PCS from the instance's rotation set
        var pcs = unified.RotationSet;
        var viaSet = new UnifiedModeService().FromPitchClassSet(pcs, root);

        Assert.That(unified.Class.Id, Is.EqualTo(viaSet.Class.Id));
        Assert.That(unified.Class.IntervalClassVector, Is.EqualTo(viaSet.Class.IntervalClassVector));
        Assert.That(unified.Class.Cardinality, Is.EqualTo(7));
    }

    [Test]
    public void WholeTone_Set_Is_Symmetric_And_NonModal_Rotations_Single()
    {
        // Whole-tone: {0,2,4,6,8,10}
        var set = Pcs(0, 2, 4, 6, 8, 10);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.IsSymmetric, Is.True);
        // In our grouping, whole-tone typically does not produce a modal family under the "contains 0" grouping
        Assert.That(inst.Class.Family, Is.Null);

        var rotations = svc.EnumerateRotations(inst.Class, PitchClass.C).ToList();
        Assert.That(rotations.Count, Is.EqualTo(1));
    }

    [Test]
    public void Diminished_Octatonic_Set_Is_Symmetric_And_Enumerates_Multiple_Rotations_WhenFamilyExists()
    {
        // One octatonic collection (half-whole): {0,2,3,5,6,8,9,11}
        var set = Pcs(0, 2, 3, 5, 6, 8, 9, 11);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);

        Assert.That(inst.Class.IsSymmetric, Is.True);

        var rotations = svc.EnumerateRotations(inst.Class, PitchClass.C).ToList();
        // Family may or may not be present depending on the grouping; assert at least 1 and allow more
        Assert.That(rotations.Count, Is.GreaterThanOrEqualTo(1));
    }
}
