namespace GA.Core.Tests.ValueObjects;

using GA.Core.ValueObjects;
using GA.Domain.Core.Theory.Atonal;

[TestFixture]
public class ValueObjectUtilsTests
{
    [TestCase(5, 0, 11, true)]
    [TestCase(12, 0, 11, false)]
    [TestCase(-1, 0, 11, false)]
    [TestCase(3, 3, 3, true)]
    [TestCase(4, 3, 3, false)]
    public void IsValueInRange_WithoutNormalization(int value, int min, int max, bool expected) =>
        Assert.That(ValueObjectUtils<PitchClass>.IsValueInRange(value, min, max), Is.EqualTo(expected));

    [Test]
    public void IsValueInRange_WithNormalization_AgreesWithEnsureValueRange()
    {
        var ranges = new[] { (Min: 0, Max: 11), (Min: 1, Max: 8), (Min: -3, Max: -1), (Min: 3, Max: 3) };

        Assert.Multiple(() =>
        {
            foreach (var (min, max) in ranges)
            {
                for (var value = -30; value <= 30; value++)
                {
                    var normalized = ValueObjectUtils<PitchClass>.EnsureValueRange(value, min, max, normalize: true);

                    Assert.That(normalized, Is.InRange(min, max), $"EnsureValueRange({value}, {min}, {max})");
                    Assert.That(
                        () => ValueObjectUtils<PitchClass>.IsValueInRange(value, min, max, normalize: true),
                        Is.True,
                        $"IsValueInRange({value}, {min}, {max}, normalize: true)");
                }
            }
        });
    }
}
