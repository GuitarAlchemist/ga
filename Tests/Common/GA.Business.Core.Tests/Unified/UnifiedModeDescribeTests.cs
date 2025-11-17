namespace GA.Business.Core.Tests.Unified;

using GA.Business.Core.Atonal;
using GA.Business.Core.Unified;

[TestFixture]
public class UnifiedModeDescribeTests
{
    private static PitchClassSet Pcs(params int[] pcs)
        => new([.. pcs.Select(PitchClass.FromValue)]);

    [Test]
    public void Describe_Includes_PrimeForm_And_Forte_For_Trichord_Example()
    {
        // 3-11 archetype prime form often represented by [0,1,5]
        var set = Pcs(0, 1, 5);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);
        var desc = svc.Describe(inst);

        Assert.That(desc.PrimeForm, Is.Not.Null.And.Not.Empty);
        // Our Forte value is an internally stable label; ensure non-null and of the form "n-m"
        Assert.That(desc.ForteNumber, Is.Not.Null.And.Not.Empty);
        Assert.That(desc.ForteNumber, Does.Match("^\\d+-\\d+$"));
        // If mapped in catalog, it should be the canonical 3-11
        Assert.That(desc.ForteNumber, Is.EqualTo("3-11"));
    }

    [Test]
    public void Describe_Includes_PrimeForm_And_Forte_For_Tetrachord_Example()
    {
        // A common tetrachord such as [0,1,4,6] or [0,2,5,7]; pick [0,2,5,7]
        var set = Pcs(0, 2, 5, 7);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);
        var desc = svc.Describe(inst);

        Assert.That(desc.PrimeForm, Is.Not.Null.And.Not.Empty);
        Assert.That(desc.ForteNumber, Is.Not.Null.And.Not.Empty);
        Assert.That(desc.ForteNumber, Does.Match("^\\d+-\\d+$"));
        Assert.That(desc.ForteNumber, Is.EqualTo("4-23"));
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
        Assert.That(desc.ForteNumber, Is.EqualTo("7-35"));

        // Enumerate rotations and ensure family size >= 1 and includes a member equal to provided set
        var rotations = svc.EnumerateRotations(inst.Class, PitchClass.C).ToList();
        Assert.That(rotations.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(rotations.Any(r => r.RotationSet.Id.Equals(set.Id)), Is.True);
    }

    [Test]
    public void Describe_WholeTone_Includes_Canonical_Forte_6_35()
    {
        // Whole-tone scale
        var set = Pcs(0, 2, 4, 6, 8, 10);
        var svc = new UnifiedModeService();
        var inst = svc.FromPitchClassSet(set, PitchClass.C);
        var desc = svc.Describe(inst);

        Assert.That(desc.ForteNumber, Is.EqualTo("6-35"));
    }
}
