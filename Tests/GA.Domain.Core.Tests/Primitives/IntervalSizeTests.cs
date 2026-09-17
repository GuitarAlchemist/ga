namespace GA.Domain.Core.Tests.Primitives;

using GA.Domain.Core.Primitives.Intervals;
using NUnit.Framework;

/// <summary>
///     Parsing tests for <see cref="SimpleIntervalSize" /> and <see cref="CompoundIntervalSize" />.
/// </summary>
[TestFixture]
public class IntervalSizeTests
{
    [TestCase("1", 1)]
    [TestCase("5", 5)]
    [TestCase("8", 8)]
    public void SimpleIntervalSize_TryParse_Valid(string input, int expected)
    {
        var parsed = SimpleIntervalSize.TryParse(input, null, out var size);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(size.Value, Is.EqualTo(expected));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("x")]
    [TestCase("0")]
    [TestCase("9")]
    [TestCase("-3")]
    public void SimpleIntervalSize_TryParse_Invalid_ReturnsFalse(string? input)
    {
        bool parsed = true;

        Assert.DoesNotThrow(() => parsed = SimpleIntervalSize.TryParse(input, null, out _));
        Assert.That(parsed, Is.False);
    }

    [TestCase("9", 9)]
    [TestCase("16", 16)]
    public void CompoundIntervalSize_TryParse_Valid(string input, int expected)
    {
        var parsed = CompoundIntervalSize.TryParse(input, null, out var size);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(size.Value, Is.EqualTo(expected));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("x")]
    [TestCase("8")]
    [TestCase("17")]
    public void CompoundIntervalSize_TryParse_Invalid_ReturnsFalse(string? input)
    {
        bool parsed = true;

        Assert.DoesNotThrow(() => parsed = CompoundIntervalSize.TryParse(input, null, out _));
        Assert.That(parsed, Is.False);
    }

    [Test]
    public void Parse_Invalid_StillThrows() =>
        Assert.Throws<ArgumentException>(() => SimpleIntervalSize.Parse("x", null));
}
