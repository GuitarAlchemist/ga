namespace GA.Domain.Core.Tests.Instruments;

using System;
using GA.Domain.Core.Instruments;
using GA.Domain.Core.Instruments.Primitives;
using GA.Domain.Core.Primitives.Notes;
using NUnit.Framework;

/// <summary>
///     Characterization tests for <see cref="Tuning" /> — string count, open-string pitches (highest
///     string first), and out-of-range indexing.
/// </summary>
[TestFixture]
public class TuningTests
{
    [TestCase(6)]
    public void Default_IsSixStringGuitar(int expected) =>
        Assert.That(Tuning.Default.StringCount, Is.EqualTo(expected));

    [Test]
    public void WellKnownTunings_HaveExpectedStringCounts() =>
        Assert.Multiple(() =>
        {
            Assert.That(Tuning.Ukulele.StringCount, Is.EqualTo(4));
            Assert.That(Tuning.Bass.StringCount, Is.EqualTo(4));
            Assert.That(Tuning.Guitar7String.StringCount, Is.EqualTo(7));
        });

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

    // A tuning is written from one end of the neck to the other, usually from the bass side. A re-entrant
    // string (the 5-string banjo's short drone, the ukulele's high G) sits at one end but is not the lowest
    // pitch, so the first-vs-last pitch comparison alone can pick the wrong end.
    [TestCase("G4 D3 G3 B3 D4", "D4", "G4")] // 5-string banjo, open G: string 5 is the high drone
    [TestCase("G4 C3 G3 B3 D4", "D4", "G4")] // 5-string banjo, C tuning
    [TestCase("G4 C4 E4 A4", "A4", "G4")] // re-entrant ukulele
    [TestCase("E2 A2 D3 G3 B3 E4", "E4", "E2")] // written from the bass side
    [TestCase("E4 B3 G3 D3 A2 E2", "E4", "E2")] // written from the treble side
    public void Indexer_NumbersStringsFromTheTrebleSide(string written, string string1, string lastString)
    {
        var tuning = new Tuning(PitchCollection.Parse(written));

        Assert.Multiple(() =>
        {
            Assert.That(tuning[(Str)1].ToString(), Is.EqualTo(string1));
            Assert.That(tuning[(Str)tuning.StringCount].ToString(), Is.EqualTo(lastString));
        });
    }

    [Test]
    public void AsSpan_LengthMatchesStringCount() =>
        Assert.That(Tuning.Default.AsSpan().Length, Is.EqualTo(Tuning.Default.StringCount));

    [Test]
    // String 7 is a valid Str value but undefined for a 6-string tuning.
    public void Indexer_StringBeyondTuning_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => _ = Tuning.Default[(Str)7]);
}
