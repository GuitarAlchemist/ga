namespace GA.Domain.Core.Tests.Theory.Atonal;

using GA.Core.ValueObjects;
using GA.Domain.Core.Theory.Atonal;
using NUnit.Framework;

/// <summary>
///     Hot paths flagged by the spareilleux/learn csharp-advanced course: they must stay correct and
///     stop allocating.
/// </summary>
[TestFixture]
public class AtonalHotPathTests
{
    [Test]
    public void PitchClassSubtraction_MatchesModularArithmetic_ForAllPairs()
    {
        for (var a = 0; a < 12; a++)
        {
            for (var b = 0; b < 12; b++)
            {
                Assert.That((PitchClass.FromValue(a) - PitchClass.FromValue(b)).Value, Is.EqualTo((a - b + 12) % 12));
            }
        }
    }

    [Test]
    public void ItemsSpan_ListsEveryIdWithoutCopying()
    {
        Assert.That(PitchClassSetId.ItemsSpan.Length, Is.EqualTo(PitchClassSetId.Items.Count).And.EqualTo(4096));
        Assert.That(PitchClassSetId.ItemsSpan[4095].Value, Is.EqualTo(4095));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var length = 0;
        for (var i = 0; i < 10; i++)
        {
            length += PitchClassSetId.ItemsSpan.Length;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(length, Is.EqualTo(40960));
        Assert.That(allocated, Is.LessThan(1024), "ItemsSpan copied the ids");
    }

    [Test]
    public void ValueObjectItems_IsOneCollection_AndValuesDoNotBox()
    {
        Assert.That(PitchClass.Items, Is.SameAs(PitchClass.Items));
        Assert.That(PitchClass.Values, Is.EqualTo(Enumerable.Range(0, 12)));
        Assert.That(ValueObjectUtils<PitchClass>.ItemsFrozenSet, Has.Count.EqualTo(12));

        _ = PitchClass.Items;
        _ = PitchClass.Values;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = 0;
        for (var i = 0; i < 100; i++)
        {
            count += PitchClass.Items.Count + PitchClass.Values.Count;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.That(count, Is.EqualTo(2400));
        Assert.That(allocated, Is.LessThan(1024), "Items or Values allocated on each read");
    }
}
