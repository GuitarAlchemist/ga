namespace GA.Domain.Core.Tests.Primitives.Intervals;

using NUnit.Framework;
using GA.Domain.Core.Primitives.Intervals;

[TestFixture]
public class SimpleIntervalSizeTests
{
    [TestCase(1, 1, 2)]
    [TestCase(3, 1, 4)]
    [TestCase(7, 1, 1)] // Wraps: the degree above the seventh is the octave-equivalent unison
    [TestCase(8, 1, 2)]
    [TestCase(3, 7, 3)] // A full diatonic octave is the identity
    [TestCase(1, -1, 7)] // Negative increments wrap as well
    [TestCase(1, 0, 1)]
    public void Addition_WrapsWithinTheSevenDiatonicDegrees(int value, int increment, int expected)
    {
        // Arrange
        var size = SimpleIntervalSize.FromValue(value);

        // Act
        var result = size + increment;

        // Assert
        Assert.That(result.Value, Is.EqualTo(expected));
    }

    [Test]
    public void Addition_LargeIncrement_DoesNotThrow() =>
        // Regression: `value + increment % 7` bound the modulo to the increment only, so Third + 7 was 10 (out of range)
        Assert.That(() => SimpleIntervalSize.Third + 7, Throws.Nothing);

    [Test]
    public void Increment_FromSeventh_WrapsToUnison()
    {
        // Arrange
        var size = SimpleIntervalSize.Seventh;

        // Act
        size++;

        // Assert
        Assert.That(size, Is.EqualTo(SimpleIntervalSize.Unison));
    }

    [TestCase("1", 1)]
    [TestCase("8", 8)]
    public void TryParse_ValidInput_ReturnsSuccess(string input, int expected)
    {
        // Arrange & Act
        var result = SimpleIntervalSize.TryParse(input, null, out var size);

        // Assert
        Assert.That(result, Is.True);
        Assert.That(size.Value, Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase(null)]
    [TestCase("P1")]
    [TestCase("abc")]
    [TestCase("0")]
    [TestCase("9")]
    [TestCase("-1")]
    public void TryParse_InvalidInput_ReturnsFalseAndDoesNotThrow(string? input)
    {
        // Regression: TryParse used to throw ArgumentException instead of returning false
        var result = false;
        Assert.That(() => result = SimpleIntervalSize.TryParse(input, null, out _), Throws.Nothing);
        Assert.That(result, Is.False);
    }

    [Test]
    public void Parse_InvalidInput_Throws() =>
        Assert.That(() => SimpleIntervalSize.Parse("9", null), Throws.ArgumentException);

    [Test]
    public void ToString_RoundTrips()
    {
        foreach (var size in SimpleIntervalSize.Items)
        {
            Assert.That(SimpleIntervalSize.Parse(size.ToString(), null), Is.EqualTo(size));
        }
    }
}
