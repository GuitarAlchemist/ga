namespace GA.Domain.Core.Tests.Theory.Tonal;

using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal.Scales;
using NUnit.Framework;

/// <summary>
///     Characterization tests for <see cref="Scale" /> — the tonal realization of a pitch-class set.
///     These pin down observable behaviour through the public API (note count, pitch-class content,
///     interval-class vector) so refactors of the internal rotation/indexer machinery stay honest.
/// </summary>
[TestFixture]
public class ScaleTests
{
    [Test]
    public void Major_HasSevenNotes()
    {
        Assert.That(Scale.Major.Count, Is.EqualTo(7));
    }

    [Test]
    public void Major_IntervalClassVector_IsCanonicalDiatonic()
    {
        // The major (diatonic) scale's interval-class vector is the famous <2 5 4 3 6 1>.
        Assert.That(Scale.Major.IntervalClassVector, Is.EqualTo(IntervalClassVector.Major));
    }

    [Test]
    public void RelativeMajorAndMinor_ShareSamePitchClassSet()
    {
        // A natural minor ("A B C D E F G") is the relative minor of C major: same pitch classes,
        // different tonal centre. Their unordered pitch-class content must be identical.
        Assert.That(Scale.NaturalMinor.PitchClassSet.Id, Is.EqualTo(Scale.Major.PitchClassSet.Id));
    }

    [Test]
    public void Major_IsModal()
    {
        // The diatonic scale generates a non-degenerate modal family (7 distinct rotations).
        Assert.That(Scale.Major.IsModal, Is.True);
    }

    [TestCase(nameof(Scale.MajorPentatonic), 5)]
    [TestCase(nameof(Scale.WholeTone), 6)]
    [TestCase(nameof(Scale.Blues), 6)]
    [TestCase(nameof(Scale.Augmented), 6)]
    [TestCase(nameof(Scale.HarmonicMinor), 7)]
    [TestCase(nameof(Scale.MelodicMinor), 7)]
    [TestCase(nameof(Scale.Diminished), 8)]
    [TestCase(nameof(Scale.BebopDominant), 8)]
    public void NamedScale_HasExpectedCardinality(string scaleName, int expectedCount)
    {
        var scale = (Scale)typeof(Scale).GetProperty(scaleName)!.GetValue(null)!;
        Assert.That(scale.Count, Is.EqualTo(expectedCount));
    }

    [Test]
    public void Constructor_FromNoteString_RoundTripsCardinality()
    {
        var scale = new Scale("C D E F G A B");
        Assert.That(scale.Count, Is.EqualTo(7));
    }
}
