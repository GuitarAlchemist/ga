namespace GA.Domain.Core.Tests.Theory.Tonal;

using GA.Domain.Core.Theory.Atonal;
using GA.Domain.Core.Theory.Tonal.Scales;
using GA.Domain.Core.Primitives.Intervals;
using GA.Domain.Core.Primitives.Notes;
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
    public void Major_HasSevenNotes() => Assert.That(Scale.Major.Count, Is.EqualTo(7));

    [Test]
    public void Major_IntervalClassVector_IsCanonicalDiatonic() =>
        // The major (diatonic) scale's interval-class vector is the famous <2 5 4 3 6 1>.
        Assert.That(Scale.Major.IntervalClassVector, Is.EqualTo(IntervalClassVector.Major));

    [Test]
    public void RelativeMajorAndMinor_ShareSamePitchClassSet() =>
        // A natural minor ("A B C D E F G") is the relative minor of C major: same pitch classes,
        // different tonal centre. Their unordered pitch-class content must be identical.
        Assert.That(Scale.NaturalMinor.PitchClassSet.Id, Is.EqualTo(Scale.Major.PitchClassSet.Id));

    [Test]
    public void Major_IsModal() =>
        // The diatonic scale generates a non-degenerate modal family (7 distinct rotations).
        Assert.That(Scale.Major.IsModal, Is.True);

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

    [Test]
    public void Constructor_EmptyEnumerable_RejectsNotesArgument() =>
        Assert.That(() => new Scale(Enumerable.Empty<Note>()),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("notes"));

    [Test]
    public void Constructor_EmptyParams_RejectsNotesArgument() =>
        Assert.That(() => new Scale(),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("notes"));

    [Test]
    public void FromPitchClassSetId_EmptySet_RejectsEmptyRealization() =>
        Assert.That(() => Scale.FromPitchClassSetId(default),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("notes"));

    [Test]
    public void Constructor_NullEnumerable_RejectsNotesArgument() =>
        Assert.That(() => new Scale((IEnumerable<Note>)null!),
            Throws.ArgumentNullException.With.Property("ParamName").EqualTo("notes"));

    [Test]
    public void Constructor_SingleNote_HasUnisonInterval()
    {
        var scale = new Scale("C");
        Interval.Simple[] expectedIntervals = [Interval.Simple.P1];

        Assert.Multiple(() =>
        {
            Assert.That(scale.Count, Is.EqualTo(1));
            Assert.That(scale.PitchClassSet.Id.Value, Is.EqualTo(1));
            Assert.That(scale.Intervals, Is.EqualTo(expectedIntervals));
        });
    }

    [Test]
    public void Constructor_SnapshotsOneEnumeration_BeforeSourceChanges()
    {
        List<Note> source = [Note.Accidented.Parse("C", null), Note.Accidented.Parse("E", null), Note.Accidented.Parse("G", null)];
        var enumerations = 0;
        var scale = new Scale(EnumerateOnce());
        source.Clear();
        string[] expectedNotes = ["C", "E", "G"];
        Interval.Simple[] expectedIntervals = [Interval.Simple.P1, Interval.Simple.M3, Interval.Simple.P5];

        Assert.Multiple(() =>
        {
            Assert.That(scale.Select(note => note.ToString()), Is.EqualTo(expectedNotes));
            Assert.That(scale.PitchClassSet.Id.Value, Is.EqualTo(145));
            Assert.That(scale.Intervals, Is.EquivalentTo(expectedIntervals));
            Assert.That(enumerations, Is.EqualTo(1));
        });

        IEnumerable<Note> EnumerateOnce()
        {
            if (++enumerations > 1)
            {
                throw new InvalidOperationException("The source can only be enumerated once.");
            }

            foreach (var note in source)
            {
                yield return note;
            }
        }
    }

    [Test]
    public void Constructor_EnharmonicDuplicates_PreservesSpellingOrderAndMultiplicity()
    {
        var scale = new Scale("C# Db C#");
        Note[] expectedNotes =
        [
            new Note.Accidented(NaturalNote.C, Accidental.Sharp),
            new Note.Accidented(NaturalNote.D, Accidental.Flat),
            new Note.Accidented(NaturalNote.C, Accidental.Sharp)
        ];

        Assert.Multiple(() =>
        {
            Assert.That(scale, Is.EqualTo(expectedNotes));
            Assert.That(scale.Count, Is.EqualTo(3));
            Assert.That(scale.PitchClassSet.Id.Value, Is.EqualTo(2));
        });
    }

    [Test]
    public void RelativeMajorAndMinor_KeepDifferentTonicIntervals() =>
        Assert.Multiple(() =>
        {
            Assert.That(Scale.Major.Intervals, Does.Contain(Interval.Simple.M3));
            Assert.That(Scale.Major.Intervals, Does.Not.Contain(Interval.Simple.m3));
            Assert.That(Scale.NaturalMinor.Intervals, Does.Contain(Interval.Simple.m3));
            Assert.That(Scale.NaturalMinor.Intervals, Does.Not.Contain(Interval.Simple.M3));
        });
}
