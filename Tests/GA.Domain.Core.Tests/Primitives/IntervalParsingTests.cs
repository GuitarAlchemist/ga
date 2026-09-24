namespace GA.Domain.Core.Tests.Primitives;

using GA.Domain.Core.Primitives.Intervals;

/// <summary>
///     An interval name is an optional quality (dd, d, m, P, M, A, AA) or accidental (bb, b, #, ##) followed by a size:
///     anything else before the size is not an interval.
/// </summary>
[TestFixture]
public class IntervalParsingTests
{
    [TestCase("13")]
    [TestCase("b13")]
    [TestCase("M13")]
    [TestCase("11")]
    [TestCase("add3")]
    [TestCase("?5")]
    public void Simple_TryParse_RejectsAnUnknownPrefix(string input) =>
        Assert.That(Interval.Simple.TryParse(input, null, out _), Is.False);

    [TestCase("19")]
    [TestCase("add9")]
    [TestCase("M19")]
    [TestCase("?11")]
    public void Compound_TryParse_RejectsAnUnknownPrefix(string input) =>
        Assert.That(Interval.Compound.TryParse(input, null, out _), Is.False);

    [TestCase("3", "M3")]
    [TestCase("b3", "m3")]
    [TestCase("#4", "A4")]
    [TestCase("P5", "P5")]
    [TestCase("bb7", "d7")]
    public void Simple_TryParse_AcceptsQualityOrAccidentalPrefixes(string input, string expected)
    {
        Assert.That(Interval.Simple.TryParse(input, null, out var interval), Is.True);
        Assert.That(interval.ToString(), Is.EqualTo(expected));
    }

    [TestCase("9", "M9")]
    [TestCase("b9", "m9")]
    [TestCase("#11", "A11")]
    [TestCase("b13", "m13")]
    [TestCase("M13", "M13")]
    public void Compound_TryParse_AcceptsQualityOrAccidentalPrefixes(string input, string expected)
    {
        Assert.That(Interval.Compound.TryParse(input, null, out var interval), Is.True);
        Assert.That(interval.ToString(), Is.EqualTo(expected));
    }

    [TestCase("13", "M13")]
    [TestCase("b13", "m13")]
    [TestCase("M13", "M13")]
    public void Diatonic_TryParse_DoesNotReadACompoundIntervalAsASimpleOne(string input, string expected)
    {
        Assert.That(Interval.Diatonic.TryParse(input, null, out var interval), Is.True);
        Assert.That(interval, Is.TypeOf<Interval.Compound>());
        Assert.That(interval.ToString(), Is.EqualTo(expected));
    }
}
