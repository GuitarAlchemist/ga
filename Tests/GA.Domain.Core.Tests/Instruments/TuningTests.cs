namespace GA.Domain.Core.Tests.Instruments;

using System;
using GA.Domain.Core.Instruments;
using GA.Domain.Core.Instruments.Primitives;
using NUnit.Framework;

/// <summary>
///     Characterization tests for <see cref="Tuning" /> — string count, open-string pitches (highest
///     string first), and out-of-range indexing.
/// </summary>
[TestFixture]
public class TuningTests
{
    [TestCase(6)]
    public void Default_IsSixStringGuitar(int expected)
    {
        Assert.That(Tuning.Default.StringCount, Is.EqualTo(expected));
    }

    [Test]
    public void WellKnownTunings_HaveExpectedStringCounts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Tuning.Ukulele.StringCount, Is.EqualTo(4));
            Assert.That(Tuning.Bass.StringCount, Is.EqualTo(4));
            Assert.That(Tuning.Guitar7String.StringCount, Is.EqualTo(7));
        });
    }

    [Test]
    public void Indexer_String1IsHighestPitch_String6IsLowest()
    {
        // Str 1 is the highest-pitch string (high E4); Str 6 is the lowest (low E2). Both are pitch class E.
        var high = Tuning.Default[(Str)1];
        var low = Tuning.Default[(Str)6];

        Assert.Multiple(() =>
        {
            Assert.That(high.PitchClass.Value, Is.EqualTo(4));
            Assert.That(low.PitchClass.Value, Is.EqualTo(4));
            Assert.That(high > low, Is.True, "String 1 should sound higher than string 6");
        });
    }

    [Test]
    public void AsSpan_LengthMatchesStringCount()
    {
        Assert.That(Tuning.Default.AsSpan().Length, Is.EqualTo(Tuning.Default.StringCount));
    }

    [Test]
    public void Indexer_StringBeyondTuning_Throws()
    {
        // String 7 is a valid Str value but undefined for a 6-string tuning.
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = Tuning.Default[(Str)7]);
    }
}
