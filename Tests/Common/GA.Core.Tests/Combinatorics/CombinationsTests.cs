namespace GA.Core.Tests.Combinatorics;

using GA.Core.Combinatorics;
using System.Numerics;

public class CombinationsTests
{
    [Test]
    public void GetIndex_Composes_MSB_First_Weights()
    {
        var elements = new[] { "a", "b", "c" };
        var comb = new Combinations<string>(elements);

        // Using MSB-first boolean masks (a,b,c):
        // 000 -> {}
        // 001 -> {c}
        // 010 -> {b}
        // 011 -> {b,c}
        // 100 -> {a}
        // 101 -> {a,c}
        // 110 -> {a,b}
        // 111 -> {a,b,c}

        Assert.That(comb.GetIndex(Array.Empty<string>()), Is.EqualTo(BigInteger.Zero));
        Assert.That(comb.GetIndex(new[] { "c" }), Is.EqualTo(new BigInteger(1)));
        Assert.That(comb.GetIndex(new[] { "b" }), Is.EqualTo(new BigInteger(2)));
        Assert.That(comb.GetIndex(new[] { "b", "c" }), Is.EqualTo(new BigInteger(3)));
        Assert.That(comb.GetIndex(new[] { "a" }), Is.EqualTo(new BigInteger(4)));
        Assert.That(comb.GetIndex(new[] { "a", "c" }), Is.EqualTo(new BigInteger(5)));
        Assert.That(comb.GetIndex(new[] { "a", "b" }), Is.EqualTo(new BigInteger(6)));
        Assert.That(comb.GetIndex(new[] { "a", "b", "c" }), Is.EqualTo(new BigInteger(7)));
    }

    [Test]
    public void Indexer_Enumerates_Correct_Subsets_Order()
    {
        var elements = new[] { 1, 2 };
        var comb = new Combinations<int>(elements);

        var all = comb.Select(v => v.ToArray()).ToArray();
        // 2 elements => 4 subsets
        Assert.That(all.Length, Is.EqualTo(4));
        // order should be: {}, {2}, {1}, {1,2}
        Assert.That(all[0], Is.EqualTo(Array.Empty<int>()));
        Assert.That(all[1], Is.EqualTo(new[] { 2 }));
        Assert.That(all[2], Is.EqualTo(new[] { 1 }));
        Assert.That(all[3], Is.EqualTo(new[] { 1, 2 }));
    }
}
