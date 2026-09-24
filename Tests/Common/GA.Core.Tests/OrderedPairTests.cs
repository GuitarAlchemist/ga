namespace GA.Core.Tests;

using Core.Combinatorics;

public class OrderedPairTests
{
    [Test]
    public void UnorderedPair_Equals_ShouldBeOrderIndependent()
    {
        var a = new UnorderedPair<int>(1, 2);
        var b = new UnorderedPair<int>(2, 1);

        Assert.That(a, Is.EqualTo(b));
        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public void UnorderedPair_GetHashCode_ShouldBeOrderIndependent()
    {
        var a = new UnorderedPair<int>(1, 2);
        var b = new UnorderedPair<int>(2, 1);

        Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void UnorderedPair_ToString_ShouldPrintTuple()
    {
        var a = new UnorderedPair<string>("A", "B");
        Assert.That(a.ToString(), Is.EqualTo("(A, B)"));
    }

    [Test]
    public void UnorderedPairStruct_Equals_ShouldBeOrderIndependent()
    {
        var a = new UnorderedPairStruct<int>(1, 2);
        var b = new UnorderedPairStruct<int>(2, 1);

        // The custom Equals now replaces the compiler-generated (order-dependent) one, so ==, Equals and
        // hash-based lookups all honour the unordered semantics.
        Assert.Multiple(() =>
        {
            Assert.That(a.Equals(b), Is.True);
            Assert.That(b.Equals(a), Is.True);
            Assert.That(a == b, Is.True);
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        });
    }

    [Test]
    public void UnorderedPairStruct_UsableAsDictionaryKey_RegardlessOfOrder()
    {
        var map = new Dictionary<UnorderedPairStruct<int>, string> { [new(1, 2)] = "first" };

        map[new(2, 1)] = "second";

        Assert.That(map, Has.Count.EqualTo(1));
        Assert.That(map[new(1, 2)], Is.EqualTo("second"));
    }

    [Test]
    public void UnorderedPairStruct_GetHashCode_DoesNotCollide_WithDifferentPairSummingToSameValue()
    {
        // (1,2) and (0,3) both sum to 3, which would collide under an additive hash combination.
        var a = new UnorderedPairStruct<int>(1, 2);
        var b = new UnorderedPairStruct<int>(0, 3);

        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }

    [Test]
    public void OrderedPair_Equality_IsOrderDependent()
    {
        var a = new OrderedPair<int>(1, 2);
        var b = new OrderedPair<int>(2, 1);

        Assert.That(a, Is.Not.EqualTo(b));
    }

    [Test]
    public void UnorderedPair_GetHashCode_DoesNotCollide_WithDifferentPairSummingToSameValue()
    {
        // (1,2) and (0,3) both sum to 3, which would collide under an additive hash combination.
        var a = new UnorderedPair<int>(1, 2);
        var b = new UnorderedPair<int>(0, 3);

        Assert.That(a.GetHashCode(), Is.Not.EqualTo(b.GetHashCode()));
    }
}
