namespace GA.Domain.Core.Tests.Theory.Atonal;

using System.Linq;
using NUnit.Framework;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Exhaustive regression tests for the OPTC rung (<see cref="TranspositionClass" />) — transposition-only
///     equivalence, between <see cref="PitchClassSet" /> (OPC) and <see cref="SetClass" /> (OPTIC).
///     Cross-reference: https://harmoniousapp.net/p/ec/Equivalence-Groups.
/// </summary>
[TestFixture]
public class TranspositionClassTests
{
    [Test]
    public void Items_Counts_MatchExhaustiveEnumeration()
    {
        var all = TranspositionClass.Items.Count;
        var card39 = TranspositionClass.Items.Count(t => t.Cardinality.Value is >= 3 and <= 9);
        var symmetric = TranspositionClass.Items.Count(t => t.IsInversionallySymmetric);

        TestContext.WriteLine($"Tn-types (all cardinalities): {all}");
        TestContext.WriteLine($"Tn-types cardinality 3-9: {card39}");
        TestContext.WriteLine($"inversionally symmetric: {symmetric}");

        Assert.Multiple(() =>
        {
            // 352 binary necklaces over all cardinalities. harmoniousapp.net cites 350 (it excludes the
            // empty set and the aggregate) and 336 for cardinality 3-9 — the latter matches exactly.
            Assert.That(all, Is.EqualTo(352), "Tn-type count drifted");
            Assert.That(card39, Is.EqualTo(336), "Cardinality-3-9 Tn-type count drifted");

            // 96 symmetric (1 Tn-type per set class) + 128 asymmetric set classes (2 Tn-types) = 224 set
            // classes; 96 + 128*2 = 352 Tn-types.
            Assert.That(symmetric, Is.EqualTo(96), "Inversionally-symmetric Tn-type count drifted");
        });
    }

    [Test]
    public void Items_CoverEverySetClass_OneOrTwoPerClass()
    {
        var bySetClass = TranspositionClass.Items.GroupBy(t => t.SetClass).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(bySetClass.Count, Is.EqualTo(SetClass.Items.Count),
                "Tn-types do not cover every set class exactly");
            Assert.That(bySetClass.All(g => g.Count() is 1 or 2), Is.True,
                "A set class maps to a number of Tn-types other than 1 or 2");
            Assert.That(bySetClass.Where(g => g.Count() == 1).All(g => g.Single().IsInversionallySymmetric),
                Is.True, "A single-Tn-type set class is not inversionally symmetric");
        });
    }

    [Test]
    public void MajorAndMinorTriad_AreDistinctTnTypes_SameSetClass()
    {
        var major = new TranspositionClass(PitchClassSet.Parse("047")); // C E G
        var minor = new TranspositionClass(PitchClassSet.Parse("037")); // C Eb G

        Assert.Multiple(() =>
        {
            Assert.That(major, Is.Not.EqualTo(minor), "Major/minor should be different transposition classes");
            Assert.That(major.SetClass, Is.EqualTo(minor.SetClass), "Major/minor should share one set class");
            Assert.That(major.IsInversionallySymmetric, Is.False, "Major triad is inversionally asymmetric");
        });
    }

    [Test]
    public void PrimeForm_IsTranspositionInvariant_ForAllTwelveTranspositions()
    {
        Assert.Multiple(() =>
        {
            foreach (var pcs in PitchClassSet.Items)
            {
                var primeId = new TranspositionClass(pcs).PrimeForm.Id.Value;
                for (var i = 0; i < 12; i++)
                {
                    var transposed = pcs.Id.Transpose(i).ToPitchClassSet();
                    Assert.That(new TranspositionClass(transposed).PrimeForm.Id.Value, Is.EqualTo(primeId),
                        $"Transposition class not invariant under transposition for {pcs.Id.Value}");
                }
            }
        });
    }
}
