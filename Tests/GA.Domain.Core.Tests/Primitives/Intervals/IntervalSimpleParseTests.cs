namespace GA.Domain.Core.Tests.Primitives.Intervals;

using NUnit.Framework;
using GA.Domain.Core.Primitives.Intervals;

[TestFixture]
public class IntervalSimpleParseTests
{
    [TestCase("P1")]
    [TestCase("m2")]
    [TestCase("M3")]
    [TestCase("P5")]
    [TestCase("M7")]
    [TestCase("P8")]
    public void TryParse_Quality_RoundTrips(string input)
    {
        // Arrange & Act
        var success = Interval.Simple.TryParse(input, null, out var interval);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(interval.Name, Is.EqualTo(input));
    }

    [TestCase("1", "P1")]
    [TestCase("b2", "m2")]
    [TestCase("#1", "A1")]
    [TestCase("2", "M2")]
    public void TryParse_Accidental_MapsToQuality(string input, string expectedName)
    {
        // Arrange & Act
        var success = Interval.Simple.TryParse(input, null, out var interval);

        // Assert
        Assert.That(success, Is.True);
        Assert.That(interval.Name, Is.EqualTo(expectedName));
    }

    [TestCase("P11")] // Regression: the greedy prefix used to swallow "P1" and silently return P1
    [TestCase("11")]
    [TestCase("m9")]
    [TestCase("P9")]
    [TestCase("q2")] // Unparseable prefix used to fall back to a natural accidental (returned M2)
    [TestCase("zz3")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("P")]
    [TestCase("0")]
    [TestCase("9")]
    public void TryParse_InvalidInput_ReturnsFalse(string? input)
    {
        // Arrange & Act
        var success = Interval.Simple.TryParse(input, null, out _);

        // Assert
        Assert.That(success, Is.False);
    }

    [Test]
    public void Parse_CompoundIntervalString_Throws() =>
        Assert.That(() => Interval.Simple.Parse("P11", null), Throws.ArgumentException);
}
