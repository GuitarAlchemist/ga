namespace GA.Core.Tests.Combinatorics;

using GA.Core.Combinatorics;

public class CartesianProductTests
{

    [Test]
    public void CartesianProduct_Order_IsLexicographic_MSB_First()
    {
        var items = new[] { "a", "b" };
        var cp = new TestCartesianProduct(items);

        var pairs = cp.ToArray();
        Assert.That(pairs.Length, Is.EqualTo(4));
        Assert.That(pairs[0].Item1, Is.EqualTo("a"));
        Assert.That(pairs[0].Item2, Is.EqualTo("a"));
        Assert.That(pairs[1].Item1, Is.EqualTo("a"));
        Assert.That(pairs[1].Item2, Is.EqualTo("b"));
        Assert.That(pairs[2].Item1, Is.EqualTo("b"));
        Assert.That(pairs[2].Item2, Is.EqualTo("a"));
        Assert.That(pairs[3].Item1, Is.EqualTo("b"));
        Assert.That(pairs[3].Item2, Is.EqualTo("b"));
    }

    [Test]
    public void CartesianProduct_WithSelector_Works()
    {
        var items = new[] { 1, 2 };
        var cp = new TestCartesianProduct2(items, pair => new OrderedPair<int>(pair.Item1, pair.Item2));
        var pairs = cp.ToArray();
        Assert.That(pairs.Select(p => (p.Item1, p.Item2)).ToArray(),
            Is.EqualTo(new[] { (1, 1), (1, 2), (2, 1), (2, 2) }));
    }

    private sealed class TestCartesianProduct(IEnumerable<string> items)
        : CartesianProduct<string, OrderedPair<string>>(items)
    {
    }

    private sealed class TestCartesianProduct2(IEnumerable<int> items, Func<OrderedPair<int>, OrderedPair<int>> selector)
        : CartesianProduct<int, OrderedPair<int>>(items, selector)
    {
    }
}
