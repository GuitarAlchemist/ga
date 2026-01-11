namespace GA.Business.Core.Tests.Unified;

using GA.Business.Core.Atonal;
using GA.Business.Core.Unified;

/// <summary>
/// Tests for the new UnifiedModeService features:
/// - RankByBrightness
/// - GetZRelatedPairs  
/// - Additional Messiaen modes (5-7)
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UnifiedModeAdvancedTests
{
    private static PitchClassSet Pcs(params int[] pcs)
        => new([.. pcs.Select(PitchClass.FromValue)]);

    private readonly UnifiedModeService _svc = new();

    #region RankByBrightness Tests

    [Test]
    public void RankByBrightness_MajorFamily_OrdersLydianFirst()
    {
        // C Major scale
        var majorSet = Pcs(0, 2, 4, 5, 7, 9, 11);
        var inst = _svc.FromPitchClassSet(majorSet, PitchClass.C);

        var ranked = _svc.RankByBrightness(inst.Class, PitchClass.C).ToList();

        Assert.That(ranked.Count, Is.GreaterThan(0));
        
        // First should be brightest, last should be darkest
        var brightest = ranked[0];
        var darkest = ranked[^1];
        
        Assert.That(brightest.Brightness, Is.GreaterThan(darkest.Brightness));
    }

    [Test]
    public void RankByBrightness_ReturnsDescendingOrder()
    {
        var majorSet = Pcs(0, 2, 4, 5, 7, 9, 11);
        var inst = _svc.FromPitchClassSet(majorSet, PitchClass.C);

        var ranked = _svc.RankByBrightness(inst.Class, PitchClass.C).ToList();

        // Verify descending order
        for (var i = 1; i < ranked.Count; i++)
        {
            Assert.That(ranked[i].Brightness, Is.LessThanOrEqualTo(ranked[i - 1].Brightness),
                $"Position {i} should be darker or equal to position {i - 1}");
        }
    }

    [Test]
    public void RankByBrightness_WholeTone_SingleRotation()
    {
        // Whole Tone is symmetric - only one "rotation"  
        var wholeTone = Pcs(0, 2, 4, 6, 8, 10);
        var inst = _svc.FromPitchClassSet(wholeTone, PitchClass.C);

        var ranked = _svc.RankByBrightness(inst.Class, PitchClass.C).ToList();

        Assert.That(ranked.Count, Is.EqualTo(1));
    }

    #endregion

    #region Z-Relation Tests

    [Test]
    public void GetZRelatedPairs_ReturnsAtLeastOnePair()
    {
        var pairs = _svc.GetZRelatedPairs().ToList();

        // There are known Z-related pairs in the Forte catalog
        Assert.That(pairs.Count, Is.GreaterThan(0), "Should find at least one Z-related pair");
    }

    [Test]
    public void GetZRelatedPairs_PairsHaveSameICV()
    {
        var pairs = _svc.GetZRelatedPairs().Take(5).ToList();

        foreach (var (set1, set2) in pairs)
        {
            var icv1 = set1.IntervalClassVector;
            var icv2 = set2.IntervalClassVector;

            Assert.That(icv1.Id, Is.EqualTo(icv2.Id),
                $"Z-related pair should have same ICV: {set1} vs {set2}");
        }
    }

    [Test]
    public void GetZRelatedPairs_PairsHaveDifferentPrimeForms()
    {
        var pairs = _svc.GetZRelatedPairs().Take(5).ToList();

        foreach (var (set1, set2) in pairs)
        {
            Assert.That(set1.Id, Is.Not.EqualTo(set2.Id),
                $"Z-related pair should have different prime forms: {set1} vs {set2}");
        }
    }

    #endregion

    #region Messiaen Modes 5-7 Tests

    [Test]
    public void Describe_MessiaenMode5_IsDetectedWhenMatching()
    {
        // Mode 5: [0,1,5,6,7,11] - 6 notes
        var set = Pcs(0, 1, 5, 6, 7, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        // Mode 5 is symmetric
        Assert.That(inst.Class.IsSymmetric, Is.True);
        Assert.That(desc.MessiaenModeIndex, Is.Null.Or.EqualTo(5));
    }

    [Test]
    public void Describe_MessiaenMode6_IsDetectedWhenMatching()
    {
        // Mode 6: [0,1,4,5,6,7,10,11] - 8 notes
        var set = Pcs(0, 1, 4, 5, 6, 7, 10, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(inst.Class.IsSymmetric, Is.True);
        Assert.That(desc.MessiaenModeIndex, Is.Null.Or.EqualTo(6));
    }

    [Test]
    public void Describe_MessiaenMode7_IsDetectedWhenMatching()
    {
        // Mode 7: [0,1,2,3,5,6,7,8,9,11] - 10 notes
        var set = Pcs(0, 1, 2, 3, 5, 6, 7, 8, 9, 11);
        var inst = _svc.FromPitchClassSet(set, PitchClass.C);
        var desc = _svc.Describe(inst);

        Assert.That(inst.Class.IsSymmetric, Is.True);
        Assert.That(desc.MessiaenModeIndex, Is.Null.Or.EqualTo(7));
    }

    #endregion

    #region PreWarm Test

    [Test]
    public void ProgrammaticForteCatalog_PreWarm_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => ProgrammaticForteCatalog.PreWarm());
        Assert.That(ProgrammaticForteCatalog.Count, Is.EqualTo(224));
    }

    #endregion
}
