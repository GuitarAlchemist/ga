namespace GA.Domain.Core.Tests.Theory.Atonal;

using GA.Domain.Core.Theory.Atonal;

[TestFixture]
public class PitchClassSetIdTests
{
    [Test]
    public void ItemsSpan_ListsEveryIdInOrder()
    {
        var span = PitchClassSetId.ItemsSpan;

        Assert.That(span.Length, Is.EqualTo(4096));
        for (var i = 0; i < span.Length; i++)
        {
            Assert.That(span[i].Value, Is.EqualTo(i));
        }

        Assert.That(span.ToArray(), Is.EqualTo(PitchClassSetId.Items));
    }

    [Test]
    public void ItemsSpan_DoesNotCopyTheIdsOnEachAccess()
    {
        var first = PitchClassSetId.ItemsSpan;

        var before = GC.GetAllocatedBytesForCurrentThread();
        var second = PitchClassSetId.ItemsSpan;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var sameArray = first == second;

        Assert.Multiple(() =>
        {
            Assert.That(sameArray, Is.True, "both accesses should view the same backing array");
            Assert.That(allocated, Is.Zero);
        });
    }
}
