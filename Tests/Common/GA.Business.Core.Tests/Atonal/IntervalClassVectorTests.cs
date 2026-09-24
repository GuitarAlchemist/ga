namespace GA.Business.Core.Tests.Atonal;

using NUnit.Framework;
using Domain.Core.Theory.Atonal;

[TestFixture]
public class IntervalClassVectorTests
{
    // Regression guard: IntervalClassVector.Major must be the *actual* major-scale ICV
    // <2 5 4 3 6 1>, packed base-12 to id 608761. A prior hardcoded base-10 literal
    // (254361) silently decoded to <1 0 3 2 4 9> once the id encoding became base-12.
    [Test]
    public void Major_HasCanonicalMajorScaleVector()
    {
        Assert.That(IntervalClassVector.Major.Vector.Values, Is.EqualTo(new[] { 2, 5, 4, 3, 6, 1 }));
    }

    [Test]
    public void Major_PacksToBase12Id_608761_NotDecimal254361()
    {
        Assert.That(IntervalClassVector.Major.Id.Value, Is.EqualTo(608761),
            "Major must encode base-12; 254361 is the stale base-10 literal that decodes to <1 0 3 2 4 9>.");
    }

    [Test]
    public void Major_RoundTripsFromCounts()
    {
        var fromCounts = new IntervalClassVector(new Dictionary<IntervalClass, int>
        {
            [IntervalClass.FromValue(1)] = 2,
            [IntervalClass.FromValue(2)] = 5,
            [IntervalClass.FromValue(3)] = 4,
            [IntervalClass.FromValue(4)] = 3,
            [IntervalClass.FromValue(5)] = 6,
            [IntervalClass.FromValue(6)] = 1,
        });

        Assert.That(fromCounts, Is.EqualTo(IntervalClassVector.Major));
    }

    // Base-12 (vs base-10) exists so counts up to 11 survive a round-trip — they occur for
    // sets of cardinality >= 11. Pins that guarantee and catches a base regression
    // (base-10 corrupts any count >= 10).
    [Test]
    public void CountsUpTo11_RoundTrip_UnderBase12()
    {
        var counts = new Dictionary<IntervalClass, int>
        {
            [IntervalClass.FromValue(1)] = 11,
            [IntervalClass.FromValue(2)] = 11,
            [IntervalClass.FromValue(3)] = 11,
            [IntervalClass.FromValue(4)] = 11,
            [IntervalClass.FromValue(5)] = 11,
            [IntervalClass.FromValue(6)] = 5,
        };

        var icv = new IntervalClassVector(counts);

        Assert.That(icv.Vector.Values, Is.EqualTo(new[] { 11, 11, 11, 11, 11, 5 }));
    }

    // A count of exactly 12 (only the full chromatic aggregate) overflows a base-12 digit. The id
    // keeps its packed value and decodes it as a special case (see IntervalClassVectorId remarks).
    [Test]
    public void ChromaticAggregate_Count12_RoundTrips()
    {
        var counts = new Dictionary<IntervalClass, int>
        {
            [IntervalClass.FromValue(1)] = 12,
            [IntervalClass.FromValue(2)] = 12,
            [IntervalClass.FromValue(3)] = 12,
            [IntervalClass.FromValue(4)] = 12,
            [IntervalClass.FromValue(5)] = 12,
            [IntervalClass.FromValue(6)] = 6,
        };

        var icv = new IntervalClassVector(counts);

        Assert.That(icv.Vector.Values, Is.EqualTo(new[] { 12, 12, 12, 12, 12, 6 }));
    }
}
