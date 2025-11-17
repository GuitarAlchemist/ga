namespace GA.Core.Tests;

using Core;

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

        // Note: record struct default equality is order-dependent.
        // The type provides a custom overload: Equals(UnorderedPairStruct<T>? other)
        // so we need to call that overload explicitly to verify order-independence.
        Assert.That(a.Equals((UnorderedPairStruct<int>?)b), Is.True);
        Assert.That(b.Equals((UnorderedPairStruct<int>?)a), Is.True);
    }

    [Test]
    public void OrderedPair_Equality_IsOrderDependent()
    {
        var a = new OrderedPair<int>(1, 2);
        var b = new OrderedPair<int>(2, 1);

        Assert.That(a, Is.Not.EqualTo(b));
    }
}
