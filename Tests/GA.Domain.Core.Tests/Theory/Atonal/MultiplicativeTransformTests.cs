namespace GA.Domain.Core.Tests.Theory.Atonal;

using System.Linq;
using NUnit.Framework;
using GA.Domain.Core.Theory.Atonal;

/// <summary>
///     Exhaustive regression tests for the M5 / M7 multiplicative transforms — the M operation that, with
///     transposition (T) and inversion (I), generates the affine group on ℤ₁₂.
/// </summary>
[TestFixture]
public class MultiplicativeTransformTests
{
    [Test]
    public void M5_IsBijection_OnAll4096Sets()
    {
        var images = PitchClassSetId.Items.Select(id => id.M5.Value).Distinct().Count();
        Assert.That(images, Is.EqualTo(PitchClassSetId.Items.Count), "M5 is not a bijection on the 4096 sets");
    }

    [Test]
    public void M5_IsInvolution()
    {
        Assert.Multiple(() =>
        {
            foreach (var id in PitchClassSetId.Items)
            {
                Assert.That(id.M5.M5.Value, Is.EqualTo(id.Value), $"M5 not involutive for {id.Value}");
            }
        });
    }

    [Test]
    public void M7_EqualsM5OfInversion()
    {
        Assert.Multiple(() =>
        {
            foreach (var id in PitchClassSetId.Items)
            {
                Assert.That(id.M7.Value, Is.EqualTo(id.Inverse.M5.Value), $"M7 != M5(Inverse) for {id.Value}");
            }
        });
    }

    [Test]
    public void M5_PreservesCardinality()
    {
        Assert.Multiple(() =>
        {
            foreach (var id in PitchClassSetId.Items)
            {
                Assert.That(id.M5.Cardinality, Is.EqualTo(id.Cardinality), $"M5 changed cardinality for {id.Value}");
            }
        });
    }

    [Test]
    public void Multiply_ByElevenEqualsInversion_ByOneIsIdentity()
    {
        Assert.Multiple(() =>
        {
            foreach (var id in PitchClassSetId.Items)
            {
                Assert.That(id.Multiply(1).Value, Is.EqualTo(id.Value), $"Multiply(1) is not identity for {id.Value}");
                Assert.That(id.Multiply(11).Value, Is.EqualTo(id.Inverse.Value), $"Multiply(11) != inversion for {id.Value}");
            }
        });
    }

    [Test]
    public void MRelated_OnSetClasses_IsInvolution()
    {
        Assert.Multiple(() =>
        {
            foreach (var sc in SetClass.Items)
            {
                Assert.That(sc.MRelated.MRelated, Is.EqualTo(sc), $"MRelated not involutive for {sc}");
            }
        });
    }

    [Test]
    public void MRelated_SelfRelatedCount_MatchesExhaustiveEnumeration()
    {
        var selfRelated = SetClass.Items.Count(sc => sc.IsMSelfRelated);
        TestContext.WriteLine($"M-self-related set classes: {selfRelated} / {SetClass.Items.Count}");
        Assert.That(selfRelated, Is.EqualTo(92), "M-self-related set-class count drifted");
    }

    [Test]
    public void MRelated_ChromaticTetrachord_MapsToQuartalSet()
    {
        // The classic M-relation: [0 1 2 3] (chromatic cluster) <-> [0 2 5 7] (stacked fourths/fifths).
        var chromatic = new SetClass(PitchClassSet.Parse("0123"));
        Assert.Multiple(() =>
        {
            Assert.That(chromatic.IsMSelfRelated, Is.False);
            Assert.That(chromatic.MRelated.PrimeForm.Id.Value,
                Is.EqualTo(new SetClass(PitchClassSet.Parse("0257")).PrimeForm.Id.Value));
        });
    }
}
