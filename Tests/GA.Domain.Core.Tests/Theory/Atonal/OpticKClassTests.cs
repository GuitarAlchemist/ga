namespace GA.Domain.Core.Tests.Theory.Atonal;

using System.Linq;
using NUnit.Framework;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Exhaustive regression tests for the OPTIC-K (complement) rung of the Callender–Quinn–Tymoczko
///     equivalence hierarchy (https://harmoniousapp.net/p/ec/Equivalence-Groups): the K operation on
///     <see cref="SetClass" /> and the <see cref="OpticKClass" /> grouping.
/// </summary>
[TestFixture]
public class OpticKClassTests
{
    // --- SetClass.Complement (the K operation) --------------------------------------------------

    [Test]
    public void Complement_IsInvolution_ForEverySetClass()
    {
        Assert.Multiple(() =>
        {
            foreach (var sc in SetClass.Items)
            {
                Assert.That(sc.Complement.Complement, Is.EqualTo(sc),
                    $"Complement is not involutive for {sc}");
            }
        });
    }

    [Test]
    public void Complement_Cardinality_IsTwelveMinusOriginal()
    {
        Assert.Multiple(() =>
        {
            foreach (var sc in SetClass.Items)
            {
                Assert.That(sc.Complement.Cardinality.Value, Is.EqualTo(12 - sc.Cardinality.Value),
                    $"Complement cardinality wrong for {sc}");
            }
        });
    }

    // --- OPTIC-K grouping (the complement-rows) -------------------------------------------------

    [Test]
    public void Items_PartitionAllSetClasses_NoOverlapNoGap()
    {
        var members = OpticKClass.Items.SelectMany(g => g.Members).ToList();
        var distinct = members.Distinct().Count();

        TestContext.WriteLine($"OPTIC-K classes: {OpticKClass.Items.Count}");
        TestContext.WriteLine($"Grouped set classes: {members.Count} (distinct {distinct}) of {SetClass.Items.Count}");

        Assert.Multiple(() =>
        {
            Assert.That(distinct, Is.EqualTo(members.Count),
                "Overlap: a set class appears in more than one OPTIC-K class");
            Assert.That(distinct, Is.EqualTo(SetClass.Items.Count),
                "Gap: not every set class is assigned to an OPTIC-K class");
        });
    }

    [Test]
    public void Items_EachClassIsComplementClosed()
    {
        Assert.Multiple(() =>
        {
            foreach (var group in OpticKClass.Items)
            {
                foreach (var member in group.Members)
                {
                    Assert.That(group.Members.Contains(member.Complement), Is.True,
                        $"OPTIC-K class {group} is not complement-closed");
                }
            }
        });
    }

    [Test]
    public void SelfComplementaryClasses_AreAllHexachords()
    {
        Assert.Multiple(() =>
        {
            foreach (var group in OpticKClass.Items.Where(g => g.IsSelfComplementary))
            {
                Assert.That(group.Representative.Cardinality.Value, Is.EqualTo(6),
                    $"Self-complementary OPTIC-K class {group} is not a hexachord");
            }
        });
    }

    [Test]
    public void Items_Counts_MatchExhaustiveEnumeration()
    {
        var all = OpticKClass.Items.Count;
        var touching39 = OpticKClass.Items.Count(g => g.Members.Any(m => m.Cardinality.Value is >= 3 and <= 9));
        var selfComp = OpticKClass.Items.Count(g => g.IsSelfComplementary);

        TestContext.WriteLine($"OPTIC-K classes (all cardinalities): {all}");
        TestContext.WriteLine($"OPTIC-K classes touching cardinality 3-9: {touching39}");
        TestContext.WriteLine($"Self-complementary OPTIC-K classes: {selfComp}");

        Assert.Multiple(() =>
        {
            // 122 total matches Callender–Quinn–Tymoczko and harmoniousapp.net.
            Assert.That(all, Is.EqualTo(122), "Total OPTIC-K class count drifted");

            // 20 self-complementary hexachords (Babbitt's hexachord theorem: of the 50 hexachord set
            // classes, 20 equal their own complement, the other 30 form 15 complement pairs → 35 classes).
            Assert.That(selfComp, Is.EqualTo(20), "Self-complementary hexachord count drifted");

            // 114, NOT the 115 harmoniousapp.net cites. The 8 OPTIC-K classes entirely outside card 3-9
            // are exactly (0<->12), (1<->11), and the six (2<->10) pairs; 122 - 8 = 114. GA's exhaustive
            // enumeration is the authority here; the web figure is off by one.
            Assert.That(touching39, Is.EqualTo(114), "Cardinality-3-9 OPTIC-K class count drifted");
        });
    }
}
