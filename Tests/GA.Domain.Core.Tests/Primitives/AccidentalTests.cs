namespace GA.Domain.Core.Tests.Primitives;

using GA.Domain.Core.Primitives.Notes;
using NUnit.Framework;

/// <summary>
///     Parsing tests for <see cref="SharpAccidental" /> and <see cref="FlatAccidental" />.
/// </summary>
[TestFixture]
public class AccidentalTests
{
    [TestCase("#", 1)]
    [TestCase("♯", 1)]
    [TestCase("x", 2)]
    [TestCase("X", 2)]
    [TestCase("##", 2)]
    [TestCase("𝄪", 2)]
    public void SharpAccidental_TryParse_Valid(string input, int expectedValue)
    {
        var parsed = SharpAccidental.TryParse(input, null, out var accidental);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(accidental.Value, Is.EqualTo(expectedValue));
        });
    }

    [TestCase("")]
    [TestCase("b")]
    [TestCase("###")]
    [TestCase("#b")]
    [TestCase("C#")]
    public void SharpAccidental_TryParse_Invalid(string input) =>
        Assert.That(SharpAccidental.TryParse(input, null, out _), Is.False);

    [TestCase("b", -1)]
    [TestCase("B", -1)]
    [TestCase("♭", -1)]
    [TestCase("bb", -2)]
    [TestCase("𝄫", -2)]
    [TestCase("bbb", -3)]
    public void FlatAccidental_TryParse_Valid(string input, int expectedValue)
    {
        var parsed = FlatAccidental.TryParse(input, null, out var accidental);

        Assert.Multiple(() =>
        {
            Assert.That(parsed, Is.True);
            Assert.That(accidental.Value, Is.EqualTo(expectedValue));
        });
    }

    [TestCase("")]
    [TestCase("#")]
    [TestCase("x")]
    [TestCase("bbbb")]
    [TestCase("Eb")]
    public void FlatAccidental_TryParse_Invalid(string input) =>
        Assert.That(FlatAccidental.TryParse(input, null, out _), Is.False);

    [Test]
    public void Accidentals_RoundTripThroughToString() =>
        Assert.Multiple(() =>
        {
            foreach (var sharp in new[] { SharpAccidental.Sharp, SharpAccidental.DoubleSharp })
            {
                Assert.That(SharpAccidental.Parse(sharp.ToString()), Is.EqualTo(sharp));
            }

            foreach (var flat in new[] { FlatAccidental.Flat, FlatAccidental.DoubleFlat, FlatAccidental.TripleFlat })
            {
                Assert.That(FlatAccidental.Parse(flat.ToString()), Is.EqualTo(flat));
            }
        });
}
