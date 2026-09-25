namespace GA.Core.Tests.ValueObjects;

using GA.Core.Abstractions;
using GA.Domain.Core.Instruments.Primitives;

// The learn site's csharp-advanced course found that EnsureValueInRange normalized with a range size
// one short (max - min instead of max - min + 1), then added 1: max + 1 came back as min + 1, and min
// itself was never reached. Fixed in #598; these pin the inclusive range.
[TestFixture]
public class RangeValueObjectTests
{
    [TestCase(7, 1, 6, 1)]
    [TestCase(0, 1, 6, 6)]
    [TestCase(13, 1, 6, 1)]
    [TestCase(12, 0, 11, 0)]
    [TestCase(-1, 0, 11, 11)]
    [TestCase(24, 0, 11, 0)]
    public void EnsureValueInRange_Normalize_WrapsOverTheInclusiveRange(int value, int min, int max, int expected) =>
        Assert.That(IRangeValueObject<Fret>.EnsureValueInRange(value, min, max, normalize: true), Is.EqualTo(expected));

    [Test]
    public void EnsureValueInRange_Normalize_IsValueModuloTheRangeSize()
    {
        var ranges = new[] { (Min: 0, Max: 11), (Min: 1, Max: 6), (Min: -3, Max: -1), (Min: 3, Max: 3) };

        Assert.Multiple(() =>
        {
            foreach (var (min, max) in ranges)
            {
                var count = max - min + 1;
                for (var value = -30; value <= 30; value++)
                {
                    var expected = min + ((value - min) % count + count) % count;
                    Assert.That(
                        IRangeValueObject<Fret>.EnsureValueInRange(value, min, max, normalize: true),
                        Is.EqualTo(expected),
                        $"EnsureValueInRange({value}, {min}, {max}, normalize: true)");
                }
            }
        });
    }

    [TestCase(0, 1, 6)]
    [TestCase(7, 1, 6)]
    public void EnsureValueInRange_WithoutNormalize_RejectsOutOfRange(int value, int min, int max) =>
        Assert.That(() => IRangeValueObject<Fret>.EnsureValueInRange(value, min, max), Throws.TypeOf<ArgumentOutOfRangeException>());
}
