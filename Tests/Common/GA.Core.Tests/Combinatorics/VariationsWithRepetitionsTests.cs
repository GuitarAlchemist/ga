namespace GA.Core.Tests.Combinatorics;

using GA.Core.Combinatorics;
using System.Numerics;
using System.Linq;

public class VariationsWithRepetitionsTests
{
    [Test]
    public void Indexer_And_GetIndex_AreInverse_ForSmallAlphabet()
    {
        // alphabet: a,b,c; length: 2
        var v = new VariationsWithRepetitions<string>(new[] { "a", "b", "c" }, 2);

        // Expected lexicographic order (MSB first): aa, ab, ac, ba, bb, bc, ca, cb, cc
        var expected = new[]
        {
            new[] { "a", "a" },
            new[] { "a", "b" },
            new[] { "a", "c" },
            new[] { "b", "a" },
            new[] { "b", "b" },
            new[] { "b", "c" },
            new[] { "c", "a" },
            new[] { "c", "b" },
            new[] { "c", "c" }
        };

        for (var i = 0; i < expected.Length; i++)
        {
            var variation = v[new BigInteger(i)];
            Assert.That(variation.ToArray(), Is.EqualTo(expected[i]));

            var index = v.GetIndex(expected[i]);
            Assert.That(index, Is.EqualTo(new BigInteger(i)));
        }
    }

    [Test]
    public void Count_And_Enumerate_All()
    {
        var v = new VariationsWithRepetitions<int>(new[] { 1, 2 }, 3);
        // base = 2, length = 3 => 8
        Assert.That(v.Count, Is.EqualTo(new BigInteger(8)));

        var seen = v.Select(varn => varn.ToArray()).ToArray();
        Assert.That(seen.Length, Is.EqualTo(8));
        // First and last
        Assert.That(seen.First(), Is.EqualTo(new[] { 1, 1, 1 }));
        Assert.That(seen.Last(), Is.EqualTo(new[] { 2, 2, 2 }));
    }

    [Test]
    public void LengthZero_YieldsSingleEmptyVariation()
    {
        var v = new VariationsWithRepetitions<int>(new[] { 1, 2, 3 }, 0);
        Assert.That(v.Count, Is.EqualTo(BigInteger.One));

        var arrs = v.Select(varn => varn.ToArray()).ToArray();
        Assert.That(arrs.Length, Is.EqualTo(1));
        Assert.That(arrs[0].Length, Is.EqualTo(0));

        var idx = v.GetIndex(Array.Empty<int>());
        Assert.That(idx, Is.EqualTo(BigInteger.Zero));
    }

    [Test]
    public void EmptyAlphabet_WithPositiveLength_HasNoVariations_And_IndexerThrows()
    {
        var v = new VariationsWithRepetitions<int>(Array.Empty<int>(), 2);
        Assert.That(v.Count, Is.EqualTo(BigInteger.Zero));

        // Enumeration should be empty
        Assert.That(v.Any(), Is.False);

        // Accessing any index should throw
        Assert.Throws<InvalidOperationException>(() => { var _ = v[BigInteger.Zero]; });

        // GetIndex also throws because alphabet is empty and length > 0
        Assert.Throws<InvalidOperationException>(() => v.GetIndex(new[] { 0, 0 }));
    }

    [Test]
    public void GetIndex_Validates_Length_And_Membership()
    {
        var v = new VariationsWithRepetitions<char>(new[] { 'x', 'y' }, 2);

        // wrong length
        Assert.Throws<ArgumentException>(() => v.GetIndex(new[] { 'x' }));

        // member not in alphabet
        Assert.Throws<ArgumentException>(() => v.GetIndex(new[] { 'x', 'z' }));
    }
}
