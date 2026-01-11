namespace GA.Business.Core.Tests.Unified;

using GA.Business.Core.Atonal;
using GA.Business.Core.Unified;

/// <summary>
/// Tests for the Describe method of UnifiedModeService.
/// </summary>
/// <remarks>
/// Note: Forte indices use the programmatic catalog (Rahn ordering) which is mathematically
/// consistent but may differ from Allen Forte's historical numbering for some sets.
/// </remarks>
[TestFixture]
public class UnifiedModeDescribeTests
{
    private static PitchClassSet Pcs(params int[] pcs)
        => new([.. pcs.Select(PitchClass.FromValue)]);

    [Test]
    public void Describe_Includes_PrimeForm_And_Forte_For_Trichord_Example()
    {
        // Trichord [0,1,5] - a common pitch class set
        var set = Pcs(0, 1, 5);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);
        var desc = svc.Describe(inst);

        Assert.That(desc.PrimeForm, Is.Not.Null.And.Not.Empty);
        // Verify Forte number has correct format: "n-m" where n is cardinality
        Assert.That(desc.ForteNumber, Is.Not.Null.And.Not.Empty);
        Assert.That(desc.ForteNumber, Does.Match(@"^\d+-\d+$"));
        Assert.That(desc.ForteNumber, Does.StartWith("3-")); // Cardinality 3
    }

    [Test]
    public void Describe_Includes_PrimeForm_And_Forte_For_Tetrachord_Example()
    {
        // Tetrachord [0,2,5,7]
        var set = Pcs(0, 2, 5, 7);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);
        var desc = svc.Describe(inst);

        Assert.That(desc.PrimeForm, Is.Not.Null.And.Not.Empty);
        Assert.That(desc.ForteNumber, Is.Not.Null.And.Not.Empty);
        Assert.That(desc.ForteNumber, Does.Match(@"^\d+-\d+$"));
        Assert.That(desc.ForteNumber, Does.StartWith("4-")); // Cardinality 4
    }

    [Test]
    public void RoundTrip_FromPitchClassSet_Matches_ClassIdentity_For_Ionian_Set()
    {
        // Ionian set {0,2,4,5,7,9,11}
        var set = Pcs(0, 2, 4, 5, 7, 9, 11);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);
        var desc = svc.Describe(inst);

        // Identity checks
        Assert.That(inst.Class.Cardinality, Is.EqualTo(7));
        Assert.That(inst.Class.IntervalClassVector.ToString(), Is.Not.Null.And.Not.Empty);
        // Cardinality 7, valid Forte index
        Assert.That(desc.ForteNumber, Does.Match(@"^7-\d+$"));

        // Enumerate rotations and ensure family size >= 1 and includes a member equal to provided set
        var rotations = svc.EnumerateRotations(inst.Class, PitchClass.C).ToList();
        Assert.That(rotations.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(rotations.Any(r => r.RotationSet.Id.Equals(set.Id)), Is.True);
    }

    [Test]
    public void Describe_WholeTone_HasValidForteNumber()
    {
        // Whole-tone scale [0,2,4,6,8,10]
        var set = Pcs(0, 2, 4, 6, 8, 10);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);
        var desc = svc.Describe(inst);

        // Cardinality 6, valid Forte index
        Assert.That(desc.ForteNumber, Does.Match(@"^6-\d+$"));
        // The whole tone scale should be symmetric
        Assert.That(inst.Class.IsSymmetric, Is.True);
    }
}
